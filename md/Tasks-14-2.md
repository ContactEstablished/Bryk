# Task 14-2 — `AnalyticsService` + `AnalyticsController` (daily-load + pmc endpoints)

## Surface
Backend only. The I/O layer over 14-1's pure calculators: `IAnalyticsService`/`AnalyticsService`
(athlete resolution, the bounded window read, group-by-date + zero-fill, delegate to the calculators,
build the `current` summary), a shared `AnalyticsRangeRequest` + validator, a new additive
`IWorkoutRepository.GetFirstWorkoutDateAsync`, a new `AnalyticsController` with two GET endpoints, two
DI registrations, and integration tests.

## Why
This is the surface the dashboard (14-3), Phase 15 (Progress charts), and Phase 18 (ATP baseline)
consume. The `pmc` endpoint returns the series **plus** a `current` summary so the dashboard needs a
single call. Compute-on-read (ADR-0006 §1) means no migration and no snapshot table.

## Depends on
- **Task 14-1** — the calculators + `DailyLoadDto` / `PmcPointDto` / `PmcSummaryDto` / `PmcResponse`.
- **ADR-0006** §1–3, §6–7 — compute-on-read, the seeding window, series assembly, `current` nullability,
  endpoints + validation.
- **Task 13-2** pagination/date-range conventions (this reuses the date-range read style, not paging).

## Required reading
- `api/Bryk.Application/Training/ThisWeekService.cs` — the closest sibling: `ICurrentUserService`
  resolution, `DateOnly.FromDateTime(DateTime.UtcNow)` as "today", a read-only service mapping to a
  response. Copy its constructor/DI shape.
- `api/Bryk.Application/Training/Workouts/WorkoutService.cs` — the `ValidateOrThrowAsync` usage and the
  `EffectiveLoad = LoadOverride ?? ComputedLoad` derivation (`Map`).
- `api/Bryk.Infrastructure/Repositories/WorkoutRepository.cs` — `GetByAthleteInRangeAsync` (reuse for the
  window read) + add the `MIN(CompletedDate)` first-workout query alongside it.
- `api/Bryk.API/Controllers/WorkoutsController.cs` — thin-controller + `[FromQuery] DateOnly?` binding.
- `api/Bryk.API/Program.cs` — where services + repos register (add `IAnalyticsService`).
- `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs` + `Fixtures/BrykWebApplicationFactory.cs` — the
  integration-test harness (`PostAsJsonAsync` to seed workouts, `GetFromJsonAsync` to read).
- `api/Bryk.Application/Common/Validation/` (the `ValidateOrThrowAsync` extension) and an existing
  validator for the FluentValidation style.

## Acceptance criteria

### Repository (additive)
- `IWorkoutRepository.GetFirstWorkoutDateAsync(Guid athleteId, CancellationToken ct)` →
  `Task<DateOnly?>`: `AsNoTracking`, `Where(AthleteId)`, `Min(CompletedDate)` (or `null` when none). One
  cheap query. Implement in `WorkoutRepository`. **No migration; no change to existing methods.**

### Request + validation
- `AnalyticsRangeRequest { DateOnly? From; DateOnly? To; }` (in `Bryk.Application/Analytics/`).
- `AnalyticsRangeRequestValidator`:
  - `From` `NotNull`; `To` `NotNull`.
  - When both present: `From <= To`; `(To.DayNumber − From.DayNumber) <= 400`; `To <= today`
    (`today = DateOnly.FromDateTime(DateTime.UtcNow)` — matches the EventDto validators / ThisWeekService).
  - Clear messages ("from and to are required", "range cannot exceed 400 days", "to cannot be in the future",
    "from must be on or before to").
- Validate via `await validator.ValidateOrThrowAsync(request, ct)` in the service (→ `ValidationException`
  → 400). Do **not** use FluentValidation's `ValidateAndThrowAsync`.

### Service
`IAnalyticsService` with two methods; `AnalyticsService` (primary-constructor DI:
`ICurrentUserService`, `IValidator<AnalyticsRangeRequest>`, `IWorkoutRepository`):
- `Task<IReadOnlyList<DailyLoadDto>> GetDailyLoadAsync(DateOnly? from, DateOnly? to, CancellationToken ct)`.
- `Task<PmcResponse> GetPmcAsync(DateOnly? from, DateOnly? to, CancellationToken ct)`.

Shared private pipeline:
1. Build + validate `AnalyticsRangeRequest`. Resolve `athleteId` via `ICurrentUserService`.
2. `firstWorkoutDate = await repo.GetFirstWorkoutDateAsync(athleteId, ct)`.
3. `computeFrom = firstWorkoutDate is null ? from : Max(Min(firstWorkoutDate, from), from.AddDays(-180))`
   (ADR-0006 §2; use `DateOnly` `DayNumber`/comparison for min/max).
4. `workouts = await repo.GetByAthleteInRangeAsync(athleteId, computeFrom, to, ct)`.
5. Group by `CompletedDate`, sum `LoadOverride ?? ComputedLoad ?? 0` → `Dictionary<DateOnly, decimal>`.
6. Materialise the contiguous zero-filled `DailyLoadDto` series over `[computeFrom, to]` (iterate day by
   day; `dict.GetValueOrDefault(date, 0)`).
- **daily-load** returns the series sliced to `[from, to]`.
- **pmc**: `PmcCalculator.Compute(fullSeries)` → slice the point list to `[from, to]` for `Series`;
  `Current` = the `PmcPointDto` at `to` mapped to `PmcSummaryDto` **with** `Acwr =
  AcwrCalculator.Compute(fullSeries, to, firstWorkoutDate)` — **unless** `firstWorkoutDate is null` or the
  athlete has no workout with `CompletedDate <= to`, in which case `Current = null` (ADR-0006 §6).
  - (`firstWorkoutDate <= to` is the "has history through `to`" test; with the range validator `to <= today`
    and a non-null first workout, this is the natural fresh-vs-not split.)

### Controller
- `AnalyticsController : ControllerBase`, `[ApiController]`, `[ApiVersion("1.0")]`,
  `[Route("api/v{version:apiVersion}/[controller]")]`, primary-constructor `IAnalyticsService`.
- `GET daily-load` → `[HttpGet("daily-load")]`, `[FromQuery] DateOnly? from, [FromQuery] DateOnly? to`,
  returns `Ok(IReadOnlyList<DailyLoadDto>)`. XML `<summary>`.
- `GET pmc` → `[HttpGet("pmc")]`, same params, returns `Ok(PmcResponse)`. XML `<summary>` noting the
  `current` summary + its nullability.
- No try/catch; validation/`KeyNotFound` flow through the existing middleware.

### DI
- `Program.cs`: `builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();` (the validator is
  already auto-registered by `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()`).

### Tests (`Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs`)
Use `BrykWebApplicationFactory`; seed workouts via `POST /api/v1/workouts` (the in-memory DB factory).
Cover:
- **Validation:** missing `from`/`to` → 400; `to` in the future → 400; range > 400 days → 400;
  `from > to` → 400.
- **daily-load zero-fill:** log two workouts a few days apart in-range; assert the response has a
  contiguous day-per-date series with the gap days at load 0 and the workout days summing `EffectiveLoad`.
- **LoadOverride respected:** a workout with `loadOverride` set contributes the override, not the computed
  load, to its day.
- **pmc current + ACWR insufficiency:** with < 28 days of history, `current.acwr` is null but
  `current.tsb` is present; (optionally) a longer seeded span yields a non-null `acwr`.
- **fresh athlete:** with no workouts, `pmc` returns `current == null` and a zero-filled series (all loads 0).
- **pmc shape:** `series` length == requested inclusive day count; `current.date == to`.
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- No migration, no `DailyLoadSnapshot` table, no new NuGet package. If perf seems to need a snapshot —
  **STOP and ask** (ADR-0006 §1).
- Don't change `WorkoutResponse`, `WorkoutService`, or the existing `IWorkoutRepository` methods (only
  *add* `GetFirstWorkoutDateAsync`).
- Don't add per-sport splitting, weekly aggregation, or peaks — those are Phase 15.
- Don't accept an athlete id from the query/body — always `ICurrentUserService`.
- Don't put the EWMA/ACWR math in the service — it lives in 14-1's calculators; the service only assembles
  the zero-filled series and slices.

## Suggested commit
```
feat: analytics service + daily-load/pmc endpoints (compute-on-read PMC)

AnalyticsService groups workouts by CompletedDate, sums EffectiveLoad,
zero-fills, seeds the EWMA over a bounded 180-day lookback, and delegates
to PmcCalculator/AcwrCalculator. New AnalyticsController:
GET /api/v1/analytics/daily-load and /pmc (series + today's current
summary, null for a fresh athlete). Range validation: required, from<=to,
<=400 days, no future to. Additive GetFirstWorkoutDateAsync; no migration.
```
