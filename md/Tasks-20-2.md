# Task 20-2 — wellness DTOs, validators, summary math, service and controller

## Surface
Backend only. One new Application slice under `api/Bryk.Application/Wellness/` (two request DTOs, one
response file, two validators, one pure summary calculator, a service interface + implementation), one
thin controller (`api/Bryk.API/Controllers/WellnessController.cs`) with three actions, **one**
`AddScoped` line in `Program.cs`, and xUnit coverage in both test projects. **No migration, no entity
change, no `ApplicationDbContext` edit, no new package, no UI.**

## Why
Task 20-1 created a table nothing can reach. This task is the whole HTTP contract Phase 20's frontend is
written against, and it carries the two behaviours the ROADMAP's success criteria name: **re-submitting
a day updates rather than duplicates**, and **out-of-range or future dates are rejected with field
messages**. Both are service-side guarantees rather than database ones — the unique index is real but
untestable on the InMemory provider the integration suite runs on (ADR-0011 §2), so the upsert must be a
read-then-update in `WellnessService` and the test that proves it must count rows through the API, not
catch a constraint violation. The `summary` endpoint exists so the dashboard fills four tiles in **one**
call: 7-day averages, deltas versus the prior 7, *and* a short daily series — because `Sparkline` renders
only at two or more points and a second round trip per tile is the wrong shape for a dashboard that
already makes several.

## Depends on
- **Task 20-1** — `DailyWellness`, `IDailyWellnessRepository` (four members, complete surface — do not
  extend it), the `{AthleteId, Date}` unique index, and the `AddDailyWellness` migration.
- **ADR-0011 §1** — the service never writes to `Athlete`; **§2** — the upsert is the service's job, PUT
  replaces the whole day, there is no DELETE.
- **ADR-0006** — nothing here feeds the PMC.
- **Task 20-3 / 20-4** consume these shapes. Nothing in this task may edit their files.

## Required reading
- `ROADMAP.md:557–577` — Phase 20's endpoint list and the validation ranges (SleepHours 0–16,
  SleepQuality 1–5, RestingHr 25–120, WeightKg 30–250, Soreness 1–10, HrvMs 10–250). These are the
  numbers; do not round or widen them.
- `md/decisions/0011-wellness-metrics.md` (Task 20-1) — §1, §2 and the *Conventions this ADR follows*
  subsection.
- `api/Bryk.API/Program.cs:32–33` — **the trap.** `SuppressModelStateInvalidFilter = true` turns the
  automatic model-state 400 **off**: a route or query value that fails to bind does **not** produce a
  400, the parameter silently receives `default(T)` and **the action still executes**. For a `{date}`
  route parameter that means an unguarded endpoint would happily upsert `0001-01-01`. Read this before
  writing the controller. `:35` — the validator assembly scan (no manual registration needed);
  `:110–126` — the services `AddScoped` block, `IActivityFileService` last at L126.
- `api/Bryk.Application/Analytics/AnalyticsRangeRequest.cs` +
  `Analytics/Validators/AnalyticsRangeRequestValidator.cs` — the range contract to mirror
  member-for-member: nullable bounds so the validator can require both explicitly, `from ≤ to`, span
  ≤ 400 days, `to` not in the future, and `DateOnly.FromDateTime(DateTime.UtcNow)` as the single source
  of "today".
- `api/Bryk.Application/ActivityFiles/Validators/ActivityFileUploadRequestValidator.cs:16–28` — the
  **field-prefix message convention**: `ValidateOrThrowAsync` (`Common/Validation/ValidationExtensions.cs:16–27`)
  collects `ErrorMessage` only and drops the property name, so any message that needs to name a field
  must say so itself (`"Content: The uploaded file is empty."`).
- `api/Bryk.Application/Goals/GoalService.cs` — the service shape: primary constructor
  `(ICurrentUserService, IValidator<T>, IXRepository, IUnitOfWork)`; `currentUser.GetCurrentAthleteId()`;
  `DateOnly.FromDateTime(DateTime.UtcNow)` for today (L19); `await validator.ValidateOrThrowAsync(...)`
  first (L39); exactly **one** `unitOfWork.SaveChangesAsync(ct)` per operation (L51); `Map` as a
  `private static` at the bottom.
- `api/Bryk.API/Controllers/GoalsController.cs` — the controller shape: `[ApiController]`,
  `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]")]`, primary ctor taking the
  service, `IActionResult` returns, XML `<summary>` on every action, no try/catch.
- `api/Bryk.API/Controllers/AnalyticsController.cs:19–26` — the only `DateOnly` parameter style in the
  codebase today: `[FromQuery] DateOnly? from`. **There is no `DateOnly` route parameter anywhere in the
  repo**; this task introduces the first one, which is why the route constraint below is mandatory.
- `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs:33–55` — the error contract:
  `Bryk.Application.Exceptions.ValidationException` → **400** with `{status, error, errors[], traceId}`;
  `KeyNotFoundException` → 404; `InvalidOperationException` → 409. **Frozen for Phase 20.**
- `api/Bryk.Application/Analytics/WeeklyLoadCalculator.cs` — the pure-calculator precedent
  (`static`, no I/O, `Math.Round(x, 2)`); the codebase rounds monetary-ish decimals to **2 places**
  everywhere (`PmcCalculator.cs:39–40`, `AcwrCalculator.cs:39`, `ThisWeekService.cs:31`).
- `api/Bryk.Application.Tests/Goals/GoalServiceTests.cs` — the unit-test harness: `private sealed class
  StubCurrentUserService` (L136), `StubUnitOfWork` with a `SaveCount` (L141), `StubGoalRepository` with
  `Added`/`Updated`/`ToReturn` (L151). Mirror it; do not extract a shared stub library.
- `api/Bryk.API.Tests/Goals/GoalsControllerTests.cs:17–20` — the integration harness:
  `new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }`,
  a fresh `BrykWebApplicationFactory` per test.
- `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs:11–23` — **no unique-index enforcement**;
  `:31–32` — `TestAthleteId`.

## Acceptance criteria

### 1. `api/Bryk.Application/Wellness/WellnessEntryRequest.cs` (new)

```csharp
public class WellnessEntryRequest
{
    // Populated by the service from the {date} route segment before validation — the route always
    // wins over anything a client puts in the body. Present on the DTO so one validator can carry both
    // the date rules and the metric rules (see WellnessEntryRequestValidator).
    public DateOnly Date { get; set; }

    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }
}
```

### 2. `api/Bryk.Application/Wellness/WellnessRangeRequest.cs` (new)

```csharp
// Range contract for GET /wellness. Nullable so the validator can require both ends explicitly —
// the controller binds optional query params (mirrors Analytics/AnalyticsRangeRequest.cs).
public class WellnessRangeRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
```

### 3. `api/Bryk.Application/Wellness/WellnessResponses.cs` (new — all four response shapes in one file, the `ActivityFileResponses.cs` precedent)

```csharp
public class WellnessEntryResponse
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }
}

// One metric's 7-day picture. Average is over the days that CARRY a value — a missing day is missing,
// not a zero. Delta is Average - PriorAverage, null unless both windows have data.
public class WellnessMetricSummaryDto
{
    public decimal? Average { get; set; }
    public decimal? PriorAverage { get; set; }
    public decimal? Delta { get; set; }
    public int DaysWithData { get; set; }
}

// One entered day, metrics only (no id, no notes) — the sparkline series.
public class WellnessDailyPointDto
{
    public DateOnly Date { get; set; }
    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
}

public class WellnessSummaryResponse
{
    public DateOnly To { get; set; }        // today (UTC)
    public DateOnly From { get; set; }      // To.AddDays(-6)  — the current 7-day window
    public DateOnly PriorFrom { get; set; } // To.AddDays(-13) — start of the prior window and of Days
    public WellnessMetricSummaryDto SleepHours { get; set; } = new();
    public WellnessMetricSummaryDto SleepQuality { get; set; } = new();
    public WellnessMetricSummaryDto RestingHr { get; set; } = new();
    public WellnessMetricSummaryDto WeightKg { get; set; } = new();
    public WellnessMetricSummaryDto Soreness { get; set; } = new();
    public WellnessMetricSummaryDto HrvMs { get; set; } = new();
    public IReadOnlyList<WellnessDailyPointDto> Days { get; set; } = [];
    public bool HasAnyEntries { get; set; }
}
```
- `Days` is **sparse** and **ascending** over `[PriorFrom, To]` — 14 days of context, so a sparkline has
  points even for an athlete who logs a few times a week, and so the tiles need exactly one request
  (ADR-0011 §2's "one call for tiles" intent from the ROADMAP).
- `HasAnyEntries` is what Task 20-4's Resting HR fallback keys on: `false` means "this athlete has never
  logged wellness", which is different from "logged, but not this metric".
- Averages are `decimal?` for every metric, including the integer ones — the mean of two heart rates is
  not an integer.

### 4. `api/Bryk.Application/Wellness/Validators/WellnessEntryRequestValidator.cs` (new)

`AbstractValidator<WellnessEntryRequest>`. Every message carries its field prefix, per the
`ActivityFileUploadRequestValidator` convention. Bounds are **inclusive** (`InclusiveBetween`, the form
`TrainingPlanUpdateRequestValidator.cs:21` and `LogWorkoutRequestValidator.cs:16` use), each under a
`.When(x => x.Prop.HasValue)` guard so a null metric is simply absent:

| Rule | Bound | Message |
|---|---|---|
| `Date` not `default` | `d != default` | `"Date: A valid date is required (yyyy-MM-dd)."` |
| `Date` not future | `d <= DateOnly.FromDateTime(DateTime.UtcNow)` | `"Date: A wellness entry cannot be in the future."` |
| `SleepHours` | `0m … 16m` | `"SleepHours: Sleep must be between 0 and 16 hours."` |
| `SleepQuality` | `1 … 5` | `"SleepQuality: Sleep quality must be between 1 and 5."` |
| `RestingHr` | `25 … 120` | `"RestingHr: Resting HR must be between 25 and 120 bpm."` |
| `WeightKg` | `30m … 250m` | `"WeightKg: Weight must be between 30 and 250 kg."` |
| `Soreness` | `1 … 10` | `"Soreness: Soreness must be between 1 and 10."` |
| `HrvMs` | `10 … 250` | `"HrvMs: HRV must be between 10 and 250 ms."` |
| `Notes` | `MaximumLength(1000)` | `"Notes: Notes must be 1000 characters or fewer."` |
| at least one metric | see below | `"Entry: At least one metric is required."` |

- The future-date rule is guarded with `.When(x => x.Date != default)` so a `default(DateOnly)` produces
  **one** message (the "valid date" one), not two.
- **At least one metric**: `RuleFor(x => x).Must(HasAtLeastOneMetric)` where the predicate is true when
  any of `SleepHours`, `SleepQuality`, `RestingHr`, `WeightKg`, `Soreness`, `HrvMs` has a value.
  **`Notes` does not count** — a row carrying only prose contributes to no tile and no average, and the
  ROADMAP's rule is "≥1 metric present". Put that sentence in a comment on the predicate.
- Class-level XML `<summary>` stating: these bounds are the ROADMAP's Phase 20 numbers; the `Date` rules
  exist because `SuppressModelStateInvalidFilter` (`Program.cs:32–33`) means a route segment that fails
  to bind arrives as `default(DateOnly)` with the action still running — the route constraint is the
  first line of defence and this is the second.

### 5. `api/Bryk.Application/Wellness/Validators/WellnessRangeRequestValidator.cs` (new)

A member-for-member mirror of `AnalyticsRangeRequestValidator`: `From` and `To` both `NotNull`
(`"from is required."` / `"to is required."`), then under `When(both have values)`: `from ≤ to`
(`"from must be on or before to."`), span ≤ **400** days (`"range cannot exceed 400 days."`), and `to`
not in the future (`"to cannot be in the future."`). Same `private const int MaxRangeDays = 400;`, same
`DateOnly.FromDateTime(DateTime.UtcNow)` source of today. Deliberate consistency — do not invent a
different range policy for wellness.

### 6. `api/Bryk.Application/Wellness/WellnessSummaryCalculator.cs` (new — pure)

```csharp
public static class WellnessSummaryCalculator
{
    public static WellnessSummaryResponse Compute(IReadOnlyList<DailyWellness> entries, DateOnly today);
}
```
- `static`, no I/O, no clock read, no repository — `today` is passed in, exactly as the analytics
  calculators take their inputs. This is what makes the arithmetic unit-testable in
  `Bryk.Application.Tests` with pinned numbers and no stubs.
- Windows: `To = today`, `From = today.AddDays(-6)` (7 days inclusive), `PriorFrom = today.AddDays(-13)`,
  prior window = `[PriorFrom, today.AddDays(-7)]` (7 days inclusive, non-overlapping).
- Per metric: `Average` = arithmetic mean of the **non-null** values in the current window,
  `Math.Round(value, 2)`; `PriorAverage` the same over the prior window; `Delta` =
  `Math.Round(Average - PriorAverage, 2)` when **both** are non-null, else `null`; `DaysWithData` =
  count of days in the **current** window carrying a value for that metric. A window with no values
  yields `Average = null` (never `0`).
- `Days` = every entry with `Date` in `[PriorFrom, To]`, ascending by date, projected to
  `WellnessDailyPointDto`. Entries outside that span are ignored even if the caller passes them.
- `HasAnyEntries` = `entries.Count > 0` (the caller loads exactly the 14-day window, so this answers
  "has this athlete logged recently"; Task 20-4's fallback is keyed on it).
- Integer metrics are averaged as `decimal` (`(decimal)value`), never integer-divided.

### 7. `api/Bryk.Application/Wellness/IWellnessService.cs` + `WellnessService.cs` (new)

```csharp
public interface IWellnessService
{
    Task<WellnessEntryResponse> UpsertAsync(DateOnly date, WellnessEntryRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<WellnessEntryResponse>> GetRangeAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<WellnessSummaryResponse> GetSummaryAsync(CancellationToken ct = default);
}
```

```csharp
public class WellnessService(
    ICurrentUserService currentUser,
    IValidator<WellnessEntryRequest> validator,
    IValidator<WellnessRangeRequest> rangeValidator,
    IDailyWellnessRepository wellnessRepo,
    IUnitOfWork unitOfWork) : IWellnessService
```

`UpsertAsync` — the load-bearing method:
1. `request.Date = date;` — the route segment wins over the body, unconditionally. Comment it.
2. `await validator.ValidateOrThrowAsync(request, ct);` — **first**, before any repository call, so an
   invalid request never touches the database.
3. `var athleteId = currentUser.GetCurrentAthleteId();`
4. `var existing = await wellnessRepo.GetByAthleteAndDateTrackedAsync(athleteId, date, ct);`
5. `existing is null` → build a new `DailyWellness { Id = Guid.NewGuid(), AthleteId = athleteId,
   Date = date, …metrics… }` and `await wellnessRepo.AddAsync(entity, ct)`.
   Otherwise → assign **all seven** fields onto `existing` (including nulls — PUT replaces the whole day,
   ADR-0011 §2) and call `wellnessRepo.Update(existing)`.
6. **One** `await unitOfWork.SaveChangesAsync(ct);` covering both branches.
7. `return Map(entity);` via a `private static WellnessEntryResponse Map(DailyWellness w)`.
- Never set `CreatedAt`/`UpdatedAt` (the interceptor owns them).
- Never read or write `Athlete` — this service takes no `IAthleteRepository` (ADR-0011 §1).
- Add a comment above step 4 stating that **this read-then-write is the idempotency guarantee**: the
  `{AthleteId, Date}` unique index backs it in SQL Server but is unenforced by the InMemory test
  provider, so the service must not rely on the database rejecting a duplicate.

`GetRangeAsync` — `await rangeValidator.ValidateOrThrowAsync(new WellnessRangeRequest { From = from,
To = to }, ct)`, then `GetByAthleteInRangeAsync(athleteId, from!.Value, to!.Value, ct)` mapped to
`WellnessEntryResponse`. Sparse output, ascending (the repository already orders).

`GetSummaryAsync` — no parameters. `today = DateOnly.FromDateTime(DateTime.UtcNow)`, load
`GetByAthleteInRangeAsync(athleteId, today.AddDays(-13), today, ct)`, return
`WellnessSummaryCalculator.Compute(entries, today)`. No validation (nothing to validate), no write.

### 8. `api/Bryk.API/Controllers/WellnessController.cs` (new — thin)

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WellnessController(IWellnessService wellnessService) : ControllerBase
```

- **`[HttpPut("{date:datetime}")] PutAsync(DateOnly date, [FromBody] WellnessEntryRequest request, CancellationToken cancellationToken)`
  → `Ok(result)`.** The `:datetime` route constraint is **mandatory**, not decorative: with
  `SuppressModelStateInvalidFilter = true` (`Program.cs:32–33`) a non-date segment would otherwise bind
  `default(DateOnly)` and run the action. With the constraint, `PUT /api/v1/wellness/not-a-date` fails
  to match the route and returns **404** before any binding happens. A segment that satisfies the
  constraint but still fails `DateOnly` binding falls through to the validator's `default` guard and
  returns 400. Both layers are required; neither alone is sufficient. Document that in the action's XML
  `<summary>`.
- **200, not 201**, for both create and update: the URL is client-chosen and the call is idempotent, so
  the response is identical whichever branch ran. Say so in the `<summary>`.
- **`[HttpGet] GetRangeAsync([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)`
  → `Ok(result)`** — sparse list, both bounds required by the validator (400 otherwise).
- **`[HttpGet("summary")] GetSummaryAsync(CancellationToken cancellationToken)` → `Ok(result)`.** No
  parameters; the window is always the 7 days ending today, with the prior 7 for deltas.
- XML `<summary>` on every action naming the status codes it can produce. No try/catch, no manual
  `ModelState` inspection, no `[FromRoute]` attribute needed (route-name binding is conventional here,
  as in `GoalsController`).
- Route order: `summary` is a literal segment on the `GET` verb and `{date:datetime}` is on `PUT`, so
  there is no ambiguity — do not add an `[HttpGet("{date}")]` action "for symmetry".

### 9. `api/Bryk.API/Program.cs` (edit — exactly one line)

Append to the services block, after `IActivityFileService` (L126):
```csharp
builder.Services.AddScoped<Bryk.Application.Wellness.IWellnessService, Bryk.Application.Wellness.WellnessService>();
```
- Fully-qualified, matching the surrounding entries (L118–126).
- **No validator registrations** — `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()`
  (L35) finds both new validators automatically.
- The repository line is Task 20-1's; do not touch it.

## Non-goals
- **No migration, no entity change, no `ApplicationDbContext` edit.** `DailyWellness.cs`,
  `IDailyWellnessRepository.cs`, `DailyWellnessRepository.cs` and `ApplicationDbContext.cs` are Task
  20-1's and are read-only here. If this task seems to need a column, an index or a fifth repository
  method — **STOP and ask** (Sr. Dev gate); do not widen the contract unilaterally.
- **No write to `Athlete`.** `WellnessService` must not take `IAthleteRepository`, and
  `api/Bryk.Domain/Entities/Athlete.cs`, `OnboardingService.cs` and `ProfileService.cs` must not appear
  in `git diff` (ADR-0011 §1). If a tile "needs" the athlete's onboarding value, that is Task 20-4's
  read-only fallback, not a write here.
- **No DELETE endpoint, no PATCH, no bulk/backfill endpoint, no pagination.** PUT replaces the day; that
  is the whole write surface (ADR-0011 §2).
- **No HRV/readiness input to the PMC.** `PmcCalculator.cs`, `AcwrCalculator.cs`, `LoadCalculator.cs`,
  `TimeInZoneCalculator.cs` and `AnalyticsService.cs` must not appear in `git diff` (ADR-0011 §3). No
  readiness score, no "train / rest today" recommendation.
- **No `ExceptionHandlingMiddleware` change and no ProblemDetails rework** — validation failures reuse
  `Bryk.Application.Exceptions.ValidationException` → 400. A middleware change is cross-cutting:
  **STOP and ask**. Phase 21 owns the error contract.
- **Do not use FluentValidation's `ValidateAndThrowAsync`** — it throws the wrong exception type and the
  middleware ignores it, producing a 500.
- **No new NuGet or npm package** (**STOP and ask**). Everything here is `FluentValidation` and the BCL.
- **No global request-pipeline configuration** — no `FormOptions`, no Kestrel limits, no
  `ApiBehaviorOptions` change. In particular **do not** turn `SuppressModelStateInvalidFilter` back off
  to "fix" date binding: that is a cross-cutting change affecting every existing endpoint —
  **STOP and ask**. Guard the parameter instead.
- Do not write files owned by siblings: `api/Bryk.Domain/**` and
  `api/Bryk.Infrastructure/**` (20-1), anything under `ui/` (20-3, 20-4).
- **No auth code** — Phase 12 stays deferred and approval-gated.
- No device/health sync, no readiness scores, no hydration/nutrition/menstruation fields, no logging
  reminders.
- **Do not fix** the two pre-existing nullable warnings in
  `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

### `api/Bryk.Application.Tests/Wellness/WellnessEntryRequestValidatorTests.cs` (new)
Boundary values are pinned exactly; use `[Theory]`/`[InlineData]` where the table repeats.
- `Date_Default_IsRejectedWithADateMessage` — `new WellnessEntryRequest { Date = default, RestingHr = 50 }`
  → invalid, and `Errors` contains exactly one message, starting `"Date:"` (the future rule must not
  also fire).
- `Date_Today_IsAccepted` / `Date_Yesterday_IsAccepted` / `Date_Tomorrow_IsRejected` — anchored on
  `DateOnly.FromDateTime(DateTime.UtcNow)`; the rejection message starts `"Date:"`.
- `SleepHours_BoundsAreInclusive` — `0m` and `16m` valid; `-0.01m` and `16.01m` invalid.
- `SleepQuality_BoundsAreInclusive` — `1` and `5` valid; `0` and `6` invalid.
- `RestingHr_BoundsAreInclusive` — `25` and `120` valid; `24` and `121` invalid.
- `WeightKg_BoundsAreInclusive` — `30m` and `250m` valid; `29.99m` and `250.01m` invalid.
- `Soreness_BoundsAreInclusive` — `1` and `10` valid; `0` and `11` invalid.
- `HrvMs_BoundsAreInclusive` — `10` and `250` valid; `9` and `251` invalid.
- `SingleMetric_IsAccepted` — only `Soreness = 4` set → valid (partial entries are the norm).
- `AllMetricsNull_IsRejected` — only `Date` set → invalid with a message starting `"Entry:"`.
- `NotesOnly_IsRejected` — `Notes = "felt rough"` with every metric null → invalid with the same
  `"Entry:"` message (notes is not a metric).
- `Notes_Over1000Characters_IsRejected` — `new string('x', 1001)` → message starts `"Notes:"`; 1000 is
  accepted.

### `api/Bryk.Application.Tests/Wellness/WellnessSummaryCalculatorTests.cs` (new — pure, no stubs)
Anchor every case on a fixed `today = new DateOnly(2026, 7, 26)` so the numbers are literal.
- `Compute_NoEntries_ReturnsNullAveragesAndHasAnyEntriesFalse` — every metric's `Average`,
  `PriorAverage` and `Delta` null, `DaysWithData == 0`, `HasAnyEntries == false`, `Days` empty.
- `Compute_WindowBoundsAreTodayMinusSixAndTodayMinusThirteen` — `To == 2026-07-26`,
  `From == 2026-07-20`, `PriorFrom == 2026-07-13`.
- `Compute_AveragesOnlyTheDaysThatCarryAValue` — three days in the current window with
  `SleepHours = 8m`, `null`, `7m` → `SleepHours.Average == 7.5m` (not `5m`) and `DaysWithData == 2`.
- `Compute_RoundsAveragesToTwoDecimals` — `7m, 7m, 8m` → `7.33m`.
- `Compute_DeltaIsCurrentMinusPrior` — current window mean `7.5m`, prior window mean `7m` →
  `Delta == 0.5m`.
- `Compute_DeltaIsNullWhenThePriorWindowHasNoData` — entries only in the current window → `Average`
  non-null, `PriorAverage` null, `Delta` null.
- `Compute_IntegerMetricsAverageAsDecimal` — `RestingHr` 48 and 49 → `Average == 48.5m`.
- `Compute_DaysAreSparseAndAscending` — entries on `PriorFrom` and `To` only → `Days.Count == 2`,
  `Days[0].Date == 2026-07-13`, `Days[1].Date == 2026-07-26`.
- `Compute_IgnoresEntriesOutsideTheFourteenDayWindow` — an entry on `2026-07-12` contributes to no
  average and does not appear in `Days`.
- `Compute_TodayIsIncludedInTheCurrentWindow` — an entry dated `today` counts toward `Average` (the
  off-by-one guard on an inclusive window).

### `api/Bryk.Application.Tests/Wellness/WellnessServiceTests.cs` (new)
Stubs modelled on `GoalServiceTests.cs:136–151`: `StubCurrentUserService`, `StubUnitOfWork` with a
`SaveCount`, and a `StubDailyWellnessRepository` exposing `ToReturn`, `Added`, `Updated`, plus the
`(from, to)` captured by the range read.
- `UpsertAsync_WhenTheDayHasNoRow_AddsForTheCurrentAthleteAndSavesOnce` — `repo.Added` non-null,
  `AthleteId` = the current athlete, `Date` = the argument, metrics copied, `uow.SaveCount == 1`,
  `repo.Updated` null.
- `UpsertAsync_WhenTheDayAlreadyHasARow_MutatesItAndDoesNotAdd` — **the idempotency fact.**
  `repo.Added.Should().BeNull()`, the existing instance's fields carry the new values,
  `repo.Updated.Should().BeSameAs(existing)`, `uow.SaveCount == 1`.
- `UpsertAsync_ClearsAMetricOmittedFromTheRequest` — existing row has `RestingHr = 50`; the request
  carries only `SleepHours = 7m` → `existing.RestingHr.Should().BeNull()` (PUT replaces the day).
- `UpsertAsync_UsesTheRouteDateNotTheBodyDate` — request body's `Date` set to `today.AddDays(-5)`, the
  `date` argument `today.AddDays(-1)` → the persisted `Date` is `today.AddDays(-1)`.
- `UpsertAsync_FutureDate_ThrowsValidationExceptionAndPersistsNothing` —
  `act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>()`, `repo.Added`
  null, `uow.SaveCount == 0`.
- `UpsertAsync_AllNullMetrics_ThrowsValidationExceptionAndPersistsNothing` — same assertions.
- `UpsertAsync_DoesNotResolveAnAthleteRepository` — structural: assert
  `typeof(WellnessService).GetConstructors().Single().GetParameters()` contains no
  `IAthleteRepository` (ADR-0011 §1, cheap and permanent).
- `GetRangeAsync_MissingBounds_ThrowsValidationException` — `(null, null)` → throws, and the repository
  was never called.
- `GetRangeAsync_FromAfterTo_ThrowsValidationException`.
- `GetSummaryAsync_LoadsExactlyFourteenDaysEndingToday` — assert the stub captured
  `from == today.AddDays(-13)` and `to == today`.

### `api/Bryk.API.Tests/Wellness/WellnessControllerTests.cs` (new folder)
Fresh `BrykWebApplicationFactory` per test; `JsonOptions` copied from `GoalsControllerTests.cs:17–20`;
dates derived from `DateOnly.FromDateTime(DateTime.UtcNow)` and formatted `"yyyy-MM-dd"`.
- `Put_CreatesTheDayAndReturnsOkWithTheStoredValues` — `PUT /api/v1/wellness/{yesterday}` with
  `{ sleepHours: 7.5, restingHr: 48, soreness: 3 }` → **200**, body `date` = yesterday,
  `sleepHours == 7.5m`, `restingHr == 48`, `id` non-empty.
- `Put_Twice_UpdatesInPlaceAndLeavesExactlyOneRow` — **the headline integration fact.** PUT
  `{ sleepHours: 7 }`, then PUT `{ sleepHours: 8, restingHr: 47 }` to the **same** date; then
  `GET /api/v1/wellness?from={d}&to={d}` returns exactly **1** entry with `sleepHours == 8m` and
  `restingHr == 47`; additionally resolve `ApplicationDbContext` from a scope and assert
  `DailyWellness.Count(w => w.Date == d)` is **1**. Do **not** assert that a duplicate insert throws —
  InMemory enforces no unique index (`BrykWebApplicationFactory.cs:11–23`).
- `Put_MalformedDateSegment_Returns404` — `PUT /api/v1/wellness/not-a-date` → **404**, because the
  `:datetime` route constraint rejects the segment before binding. Pinned precisely because
  `SuppressModelStateInvalidFilter` means an unconstrained route would have bound `0001-01-01` and run
  the action.
- `Put_MinValueDateSegment_Returns400WithADateMessage` — `PUT /api/v1/wellness/0001-01-01` (a
  well-formed date that satisfies the constraint) → **400** whose `errors[0]` starts `"Date:"` — the
  validator's `default` guard, the second layer.
- `Put_FutureDate_Returns400WithADateMessage` — tomorrow → 400, `errors[0]` starts `"Date:"`.
- `Put_OutOfRangeMetric_Returns400WithTheFieldName` — `{ restingHr: 200 }` → 400 and `errors` contains a
  message starting `"RestingHr:"` (the ROADMAP's "field messages" criterion).
- `Put_EmptyBody_Returns400WithTheEntryMessage` — `{}` → 400, `errors` contains `"Entry:"`.
- `Put_DoesNotModifyTheAthleteRow` — seed an `Athlete` (`Id = TestAthleteId`, `RestingHr = 55`,
  `WeightKg = 70m`) through a scope, PUT wellness with `{ restingHr: 44, weightKg: 68.5 }`, re-read the
  `Athlete` in a fresh scope → `RestingHr == 55` and `WeightKg == 70m` (ADR-0011 §1).
- `Get_Range_ReturnsOnlyDaysWithEntriesAscending` — PUT three days, GET a range covering two of them →
  exactly 2 items, ascending by `date`.
- `Get_Range_MissingBounds_Returns400` and `Get_Range_FromAfterTo_Returns400`.
- `Get_Summary_WithNoEntries_ReturnsNullAveragesAndHasAnyEntriesFalse` — `hasAnyEntries == false`,
  `sleepHours.average` null, `days` empty. (A fresh factory means a fresh athlete.)
- `Get_Summary_ReturnsTheSevenDayAverageAndTheDailySeries` — PUT today `{ sleepHours: 8 }` and yesterday
  `{ sleepHours: 7 }` → `sleepHours.average == 7.5m`, `sleepHours.daysWithData == 2`, `days.Count == 2`
  ascending, `hasAnyEntries == true`.
- `Get_Summary_DeltaIsNullWithNoPriorWeekData` — the same two entries → `sleepHours.delta` null.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must **rise** from wherever Task 20-1 left it (the **343** baseline plus 20-1's seven repository
facts) by roughly forty more — the bulk in `Bryk.Application.Tests` (validator + calculator + service)
and about fifteen in `Bryk.API.Tests` — with zero failures. Vitest must stay at **exactly 288 / 61
files**: this task touches no UI. Warnings must stay at **16** on a clean (`--no-incremental`) compile;
an incremental build reports 14. A new warning from a file this task adds is a **STOP and ask**.

## Review checklist
- [ ] `PUT` carries **both** guards: the `{date:datetime}` route constraint **and** the validator's
      `default(DateOnly)` rule, each with its own pinned test (404 and 400 respectively).
- [ ] Every validation message names its field (`"SleepHours: …"`), because `ValidateOrThrowAsync`
      drops property names.
- [ ] Bounds are inclusive and exactly the ROADMAP's numbers, with both edges pinned in tests.
- [ ] An all-null body is a 400 (`"Entry: …"`); notes alone does not qualify as a metric.
- [ ] The upsert is a read-then-update in the **service**, commits **once**, and its idempotency is
      proven by a test that counts rows — no test asserts a duplicate insert throws.
- [ ] `WellnessService`'s constructor takes no `IAthleteRepository`; `git diff --stat` shows no change
      to `Athlete.cs`, `OnboardingService.cs`, `ProfileService.cs`, any analytics calculator,
      `ExceptionHandlingMiddleware.cs`, or anything under `ui/`, `api/Bryk.Domain/`,
      `api/Bryk.Infrastructure/`.
- [ ] `WellnessSummaryCalculator` is `static` and pure — it takes `today` as a parameter and reads no
      clock, no repository and no configuration.
- [ ] `Program.cs` diff is exactly one `AddScoped` line; no validator registration was added.
- [ ] The controller is thin: no try/catch, no `ModelState` inspection, `IActionResult` returns, XML
      `<summary>` on all three actions.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
```
feat: wellness endpoints - idempotent per-day upsert, range and 7-day summary

PUT /api/v1/wellness/{date} is the whole write surface: one idempotent
per-day upsert that replaces the day, so re-submitting updates rather than
duplicating. That guarantee lives in the service as a read-then-update, not in
the database - the {AthleteId, Date} unique index is real, but the InMemory
provider the integration suite runs on enforces no unique index, so the test
that proves idempotency PUTs twice and counts rows instead of catching a
constraint violation.

The date parameter is guarded twice on purpose. SuppressModelStateInvalidFilter
is on app-wide, so a route segment that fails to bind does not produce a 400 -
it silently arrives as default(DateOnly) and the action still runs. The
:datetime route constraint turns a non-date segment into a 404 before binding,
and the validator rejects default(DateOnly) and any future date with a
field-prefixed message. Every metric bound is the ROADMAP's number, inclusive,
and only applies when the metric is present; an all-null body is a 400 rather
than an empty row, and notes alone does not count as a metric.

GET /wellness?from=&to= mirrors the analytics range contract exactly (both
bounds required, from <= to, 400-day span, no future to). GET /wellness/summary
carries 7-day averages, deltas versus the prior 7 and a sparse 14-day daily
series in one call, because the dashboard fills four tiles from it and
Sparkline needs at least two points. The summary math is a pure static
calculator taking today as a parameter, so the averages, the rounding and the
window edges are pinned with literal numbers. Nothing here reads or writes
Athlete: the service takes no IAthleteRepository, and a test asserts it
(ADR-0011 1).
```
