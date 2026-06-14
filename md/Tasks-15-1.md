# Task 15-1 — Weekly-load + peaks calculators, DTOs, endpoints

## Surface
Backend only. Two new pure calculators in `Bryk.Application/Analytics/` (`WeeklyLoadCalculator`,
`PeaksCalculator`) mirroring `PmcCalculator`/`LoadCalculator`; their DTOs; a `WeeklyLoadRequest` +
validator; three additive `AnalyticsService` methods' first two (`GetWeeklyLoadAsync`,
`GetPeaksAsync`); two additive `AnalyticsController` actions (`weekly-load`, `peaks`); two additive
repo reads; integration + unit tests. **No migration, no new package.**

## Why
Phase 15's Load chart (15-4) and peaks grid (15-5) consume these. Weekly load is `ThisWeekService`'s
computation generalised to N ISO weeks plus the optimal band that **Phase 18's ramp model anchors on**.
Peaks are honest session-level personal records (compute-on-read, no table).

## Depends on
- **ADR-0007** §1 (optimal band), §2 (peaks records), §3 (weekly shape), §6 (endpoints).
- **ADR-0005** §1–6 (`EffectiveLoad`, `LoadCalculator`, executed-`Workout` shape).
- **Task 14-2** — the `AnalyticsService`/`AnalyticsController` pattern this extends.

## Required reading
- `api/Bryk.Application/Analytics/AnalyticsService.cs` + `AnalyticsController.cs` — the exact pattern to
  extend (primary-ctor DI, `ValidateOrThrowAsync`, thin controller, `[FromQuery]`).
- `api/Bryk.Application/Training/ThisWeekService.cs` — **the weekly planned-load template**: Monday-week
  computation (`CurrentWeek`), `GetPlannedWorkoutsInRangeWithStructureAsync` + `GetWithSportProfilesAsync`
  + `GetZonesAsync`, `LoadCalculator.ComputePlannedLoad`, `EffectiveLoad = PlannedLoad ?? computed`.
- `api/Bryk.Application/Training/Load/LoadCalculator.cs` — `ComputePlannedLoad(pw, profile, sportZones)`.
- `api/Bryk.Infrastructure/Repositories/WorkoutRepository.cs` + `TrainingPlanRepository.cs` — add the two
  reads alongside the existing ones; mirror their `AsNoTracking`/`AsSplitQuery`/`Include` style.
- `api/Bryk.Domain/Entities/Workout.cs`, `WorkoutStepResult.cs` — session + per-step actual fields.
- `api/Bryk.Application/Analytics/Validators/AnalyticsRangeRequestValidator.cs` — the validator style.
- `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs` — the integration harness to extend.
- `api/Bryk.Application.Tests/Analytics/` (the 14-1 calculator tests) — the pure-calculator test style.

## Acceptance criteria

### DTOs (`Bryk.Application/Analytics/`)
- `WeeklyLoadWeekDto { DateOnly WeekStart; decimal PlannedLoad; decimal ActualLoad; decimal RollingAverage; }`.
- `OptimalBandDto { decimal Lower; decimal Upper; }`.
- `WeeklyLoadResponse { IReadOnlyList<WeeklyLoadWeekDto> Weeks; OptimalBandDto? OptimalBand; }`.
- `PeakKind` enum (1-based: `Load=1, Duration=2, Distance=3, Pace=4, Power=5`).
- `PeakRecordDto { PeakKind Kind; Sport Sport; decimal Value; DateOnly AchievedDate; Guid AchievedWorkoutId; bool IsRecent; decimal? PreviousValue; }`.
- `PeaksResponse { IReadOnlyList<PeakRecordDto> Records; }`.

### `WeeklyLoadCalculator` (pure, static — ADR-0007 §1, §3)
- `Compute(IReadOnlyList<decimal> actualLoads)` → `(decimal[] RollingAverages, OptimalBandDto? Band)`:
  - `RollingAverages[i] = round(mean(actualLoads[max(0,i-3)..i]), 2)` — trailing 4-week mean.
  - `Band` = `{ Lower = round(0.8 × A, 2), Upper = round(1.3 × A, 2) }` where `A = RollingAverages[^1]`;
    **null** when `actualLoads` is empty or `A == 0` (fresh athlete — honesty rule, no `[0,0]` band).
- Unit-tested: known series → exact rolling averages; band = `[0.8A, 1.3A]`; `< 4` weeks averages over
  what exists; all-zero actuals → band null; single week → rolling average = that week, band from it.

### `PeaksCalculator` (pure, static — ADR-0007 §2)
- Input: `IReadOnlyList<PeakWorkoutSummary>` (a small record the service builds) +
  `DateOnly today` (passed in — calculators never call `DateTime.UtcNow`).
  - `PeakWorkoutSummary { Guid WorkoutId; DateOnly Date; Sport Sport; decimal? Load; int? DurationSeconds; int? DistanceMeters; decimal? AvgPaceSecPerUnit; decimal? AvgPowerWatts; }`.
- `Compute(summaries, today)` → `IReadOnlyList<PeakRecordDto>`. For each `PeakKind` with ≥ 1 qualifying
  sample, emit the best record:
  - **Load** = max `Load`; **Duration** = max `DurationSeconds`; **Distance** = max `DistanceMeters`
    (sources with a value); **Pace** = **min** `AvgPaceSecPerUnit` (fastest); **Power** = max `AvgPowerWatts`.
  - `Value` rounded to 2 places; `Sport`/`AchievedDate`/`AchievedWorkoutId` from the winning summary.
  - `IsRecent = AchievedDate >= today.AddDays(-89)` (within 90 days inclusive).
  - `PreviousValue` = the **second-best** value of that kind across distinct samples (second-max, or
    second-min for Pace); **null** when only one sample qualifies. Rounded to 2.
  - A kind with no qualifying sample is **absent** (not a 0 record).
- Unit-tested: best/second-best selection per kind; pace picks the smallest; `IsRecent` boundary at 90
  days; single-sample → `PreviousValue` null; empty input → empty list.

### Repository reads (additive — no migration)
- `IWorkoutRepository.GetByAthleteWithStepResultsAsync(Guid athleteId, Sport? sport, CancellationToken ct)`
  → all the athlete's workouts (optional `sport` filter) **with `StepResults`** included, `AsNoTracking`,
  `AsSplitQuery`, newest-first. (Peaks needs step results only for the bike session-power derivation.)
- `ITrainingPlanRepository.GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct)`
  → planned workouts whose `Id` ∈ `ids`, with `Blocks` (ordered) → `Steps` (ordered) included,
  `AsNoTracking`, `AsSplitQuery`. (Used by 15-2; add it here so the repo change is one commit. Empty `ids`
  → empty list, no query.)

### `WeeklyLoadRequest` + validator
- `WeeklyLoadRequest { int Weeks; }` (in `Bryk.Application/Analytics/`).
- `WeeklyLoadRequestValidator`: `Weeks` `InclusiveBetween(1, 26)` with a clear message
  ("weeks must be between 1 and 26"). Validate via `ValidateOrThrowAsync`.

### `AnalyticsService` (extend; do not break 14-2)
Add ctor deps mirroring `ThisWeekService`: `ITrainingPlanRepository`, `IAthleteRepository`, `IZoneService`,
`IValidator<WeeklyLoadRequest>` (keep the existing `ICurrentUserService`, `IValidator<AnalyticsRangeRequest>`,
`IWorkoutRepository`). Extend `IAnalyticsService` with:
- `Task<WeeklyLoadResponse> GetWeeklyLoadAsync(int? weeks, CancellationToken ct)`:
  1. `var w = weeks ?? 8;` build `WeeklyLoadRequest { Weeks = w }`, `ValidateOrThrowAsync` (out-of-range
     → 400; absent → default 8). Resolve `athleteId`.
  2. Compute the N Monday-anchored ISO weeks ending with the current week (reuse `ThisWeekService`'s
     `CurrentWeek` Monday math; the span is `[currentWeekMonday − (N−1)·7, currentWeekMonday + 6]`).
  3. `planned = GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, spanStart, spanEnd)` +
     `athlete = GetWithSportProfilesAsync` + `zones = GetZonesAsync`. Per planned workout:
     `EffectiveLoad = PlannedLoad ?? LoadCalculator.ComputePlannedLoad(pw, profile, sportZones)`; group by
     its ISO-week Monday, sum (null → 0).
  4. `actual = GetByAthleteInRangeAsync(athleteId, spanStart, spanEnd)`; per workout
     `EffectiveLoad = LoadOverride ?? ComputedLoad ?? 0`; group by ISO-week Monday, sum.
  5. Build the ordered (oldest→newest) per-week planned/actual sums; `WeeklyLoadCalculator.Compute` →
     rolling averages + band; assemble `WeeklyLoadResponse` (round planned/actual to 2).
- `Task<PeaksResponse> GetPeaksAsync(Sport? sport, CancellationToken ct)`:
  1. Resolve `athleteId`. `workouts = GetByAthleteWithStepResultsAsync(athleteId, sport)`.
  2. Per workout build a `PeakWorkoutSummary`: `Load = LoadOverride ?? ComputedLoad`;
     `DurationSeconds`/`DistanceMeters` from the session; **Pace** (run/swim only, both fields present) =
     `ActualDurationSeconds ÷ (ActualDistanceMeters/1000)` for run, `÷ (…/100)` for swim (sec per km / per
     100 m); **Power** (bike only) = duration-weighted mean of `StepResults.AvgPower` over results that
     captured power (`Σ(avgPower×dur) ÷ Σ dur`), else null.
  3. `PeaksCalculator.Compute(summaries, DateOnly.FromDateTime(DateTime.UtcNow))` → `Records`.

### Controller (additive actions on `AnalyticsController`)
- `GET weekly-load` → `[HttpGet("weekly-load")]`, `[FromQuery] int? weeks`, `Ok(WeeklyLoadResponse)`.
- `GET peaks` → `[HttpGet("peaks")]`, `[FromQuery] Sport? sport`, `Ok(PeaksResponse)`.
- XML `<summary>` on each (note: weeks 1–26 default 8; peaks all-time session-level, `sport` optional).
  No try/catch.

### Tests
- **Unit** (`Bryk.Application.Tests/Analytics/`): `WeeklyLoadCalculatorTests`, `PeaksCalculatorTests` per
  the bullets above.
- **Integration** (`Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs`, extend): seed via `POST /workouts`
  + plan/planned-workout endpoints.
  - `weekly-load`: `weeks=0`/`weeks=27` → 400; default (no param) returns 8 weeks; a seeded completed
    workout shows in its week's `actualLoad`; `optimalBand` null for a fresh athlete, present (`[0.8A,1.3A]`)
    once there's actual load.
  - `peaks`: with seeded workouts, the Load/Duration records point at the right workout + date; `sport`
    filter restricts kinds; fresh athlete → empty `records`; a recently-dated record has `isRecent=true`.
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- No migration, no snapshot/records table, no new package. If a read seems too slow — **STOP and ask**.
- Don't change the 14-2 `daily-load`/`pmc` behaviour or existing repo methods (only *add* the two reads).
- Don't accept an athlete id from query/body — always `ICurrentUserService`.
- Don't put the rolling-average/band or record-selection math in the service — it lives in the calculators.
- Peaks are **session-level only** — no per-sample duration-curve peaks (Phase 19+).

## Suggested commit
```
feat: weekly-load + peaks analytics (calculators, endpoints, tests)

Pure WeeklyLoadCalculator (4-week rolling average + [0.8,1.3]×trailing
optimal band, ADR-0007 §1) and PeaksCalculator (session-level Load/
Duration/Distance/Pace/Power records, best + second-best, 90-day recency).
AnalyticsService gains GetWeeklyLoadAsync (ThisWeekService's planned-load
computation over N ISO weeks vs completed EffectiveLoad) and GetPeaksAsync;
additive GET /analytics/weekly-load and /peaks. Two additive repo reads;
no migration. xUnit pins the band, rolling average, and record selection.
```
