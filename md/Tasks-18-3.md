# Task 18-3 — `IPeriodizationService` + `GET /api/v1/trainingplans/{id}/weekly-targets`

## Surface
Backend only. A new `IPeriodizationService`/`PeriodizationService` in
`api/Bryk.Application/Training/Periodization/` that resolves the baseline from real athlete history,
calls 18-1's pure `WeeklyTargetCalculator`, and merges per-week planned + actual load; the
`WeeklyTargetsResponse` / `WeeklyTargetWeekDto` / `TargetBaselineSource` shapes; one additive
`[HttpGet("{id:guid}/weekly-targets")]` action on `TrainingPlansController`; **one** new
`Program.cs` `AddScoped` line; unit + integration tests. **No migration, no new package, no repository
change** (every read it needs already exists).

## Why
18-1's calculator is pure and unreachable: it takes a baseline it cannot compute. This task supplies
the I/O half — the ADR-0009 §1 fallback chain (trailing-4-week mean actual → the plan's own first-week
planned load → no targets) — and turns targets into an endpoint the panel (18-4) and the dashboard
(18-5) both read. Computing this server-side keeps one home for the ramp rule, exactly as
`ComplianceClassifier` + `CalendarService` did for compliance in Phase 16, and lets the client stay a
dumb renderer. The per-week actual merge is what makes the panel a *feedback* surface rather than a
prescription printout, and it reuses `AnalyticsService.GetWeeklyLoadAsync`'s aggregation verbatim so
Progress and the Periodization panel can never disagree about what a week's load was.

## Depends on
- **Task 18-1** — `WeeklyTargetCalculator.Compute`, `WeeklyTargetInput`, `WeeklyTargetDto`. Hard
  dependency: this task does not re-implement any ramp math.
- **Task 18-2** — shares `api/Bryk.API/Controllers/TrainingPlansController.cs`. Land 18-2 first; do not
  edit that file from two sessions.
- **ADR-0009 §1** (baseline chain + "no baseline → no targets"), **§4** (compute-on-read).
- **ADR-0007 §1** — `A` = trailing-4-week mean actual load; the same window, not a second one.
- **ADR-0005 §3** — `EffectiveLoad` = `LoadOverride ?? ComputedLoad` (completed) and
  `PlannedLoad ?? ComputedLoad` (planned, via `LoadCalculator.ComputePlannedLoad`).

## Required reading
- `api/Bryk.Application/Analytics/AnalyticsService.cs:57–107` (`GetWeeklyLoadAsync`) — **the
  reference implementation** for both aggregations: planned via
  `GetPlannedWorkoutsInRangeWithStructureAsync` + `GetWithSportProfilesAsync` + `GetZonesAsync` +
  `LoadCalculator.ComputePlannedLoad`, actual via `IWorkoutRepository.GetByAthleteInRangeAsync` with
  `LoadOverride ?? ComputedLoad ?? 0m`, both keyed by `WeekStart(...)`. Copy the shape, including the
  `Math.Round(..., 2)` on each emitted figure. Also `:186` for the Monday-anchored `WeekStart` helper.
- `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs` (from 18-1) — the input
  record's field order and the "empty list when baseline is null/≤ 0" contract.
- `api/Bryk.Application/Training/TrainingPlanService.cs:155–164` — `LoadOwnedPlanAsync`, the
  ownership → `KeyNotFoundException` → 404 pattern this service duplicates (services do not call other
  services; the five-line guard is copied deliberately, not refactored into a shared helper).
- `api/Bryk.Application/Calendar/CalendarService.cs` — the closest precedent for a read-only,
  multi-repository service with primary-ctor DI and a merged day/week-keyed response.
- `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs:24` (`GetByAthleteInRangeAsync`, inclusive bounds,
  no-tracking) and `ITrainingPlanRepository.cs:38` (`GetPlannedWorkoutsInRangeWithStructureAsync`) —
  the two reads to reuse **as-is**.
- `api/Bryk.Domain/Interfaces/IEventRepository.cs:14` — `GetByIdAsync`, for resolving the linked
  event's `EventDate`.
- `api/Bryk.API/Program.cs:100–120` — the manual `AddScoped` list (no assembly scan for services);
  note the fully-qualified style used at L117–120 for types outside the file's `using` set.
- `api/Bryk.API/Controllers/TrainingPlansController.cs` — the controller to extend (ctor gains one
  dependency); XML `<summary>` on every action.
- `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs` — **the stub style** for
  service-level unit tests (`StubCurrentUserService`, a filtering `StubTrainingPlanRepository`,
  `StubAthleteRepository`, `StubZoneService`, all `private sealed class`, unused members throwing
  `NotImplementedException`).
- `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` — the integration harness; and
  `api/Bryk.Application/Training/Workouts/LogWorkoutRequestValidator.cs:11–13` — `CompletedDate` may
  not be in the future, which constrains how integration tests seed history.

## Acceptance criteria

### DTOs (`api/Bryk.Application/Training/Periodization/`)
- `TargetBaselineSource.cs`:
  ```csharp
  public enum TargetBaselineSource { None = 0, TrailingActual = 1, FirstWeekPlanned = 2 }
  ```
  Serialized as a string by the global `JsonStringEnumConverter`.
- `WeeklyTargetWeekDto.cs` — the per-week merge shape:
  ```csharp
  public class WeeklyTargetWeekDto
  {
      public DateOnly WeekStart { get; set; }
      public decimal TargetLoad { get; set; }
      public bool IsRecoveryWeek { get; set; }
      public bool IsTaperWeek { get; set; }
      public decimal PlannedLoad { get; set; }   // Σ effective planned load of THIS plan's sessions in the week
      public decimal ActualLoad { get; set; }    // Σ effective actual load of the athlete's completions in the week
  }
  ```
- `WeeklyTargetsResponse.cs`:
  ```csharp
  public class WeeklyTargetsResponse
  {
      public Guid PlanId { get; set; }
      public DateOnly StartDate { get; set; }
      public DateOnly EndDate { get; set; }
      public decimal? Baseline { get; set; }
      public TargetBaselineSource BaselineSource { get; set; }
      public IReadOnlyList<WeeklyTargetWeekDto> Weeks { get; set; } = new List<WeeklyTargetWeekDto>();
  }
  ```
  `Baseline` is echoed so the UI can explain the ramp honestly ("ramping from 200 TSS/wk, your last
  4-week average") instead of showing an unexplained curve. `BaselineSource = None` ⇒ `Baseline` is
  null ⇒ `Weeks` is **empty** (ADR-0009 §1 honesty rule — no fabricated ramp, and no "target 0" rows).
- **Asymmetry to document in a code comment on `WeeklyTargetWeekDto`:** `PlannedLoad` is scoped to this
  plan's own planned workouts; `ActualLoad` is athlete-wide for the week, because a completed `Workout`
  carries no plan attribution (ADR-0005 / ADR-0007 treat actual load athlete-wide). Do not invent an
  attribution rule.

### `IPeriodizationService` (new interface, same folder)
```csharp
/// <summary>
/// Compute-on-read weekly load targets for a training plan (ADR-0009). Athlete identity comes from
/// <see cref="Common.ICurrentUserService"/>. Throws
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (→ 404) when the plan is missing or
/// belongs to another athlete.
/// </summary>
public interface IPeriodizationService
{
    Task<WeeklyTargetsResponse> GetWeeklyTargetsAsync(Guid planId, CancellationToken ct = default);
}
```

### `PeriodizationService` (new, primary-ctor DI)
Ctor: `(ICurrentUserService currentUser, ITrainingPlanRepository planRepo, IWorkoutRepository
workoutRepo, IEventRepository eventRepo, IAthleteRepository athleteRepo, IZoneService zoneService)` —
all six are already registered in `Program.cs`.

`GetWeeklyTargetsAsync(planId, ct)` in this order:
1. `athleteId = currentUser.GetCurrentAthleteId();`
   `plan = await planRepo.GetByIdAsync(planId, ct);`
   `if (plan is null || plan.AthleteId != athleteId) throw new KeyNotFoundException();`
2. Week window: `firstWeekStart = WeekStart(plan.StartDate)`, `lastWeekEnd = WeekStart(plan.EndDate).AddDays(6)`
   (private static Monday helper, same expression as `AnalyticsService.cs:186`).
3. **Baseline (ADR-0009 §1 chain), anchored on the plan's first week — not on today.** Anchoring on
   the plan start makes the target series stable for the plan's whole life; a today-anchored baseline
   would silently reshape every target every Monday. Add that sentence as a comment.
   - a. `trailingStart = firstWeekStart.AddDays(-28)`, `trailingEnd = firstWeekStart.AddDays(-1)`.
     `completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, trailingStart, trailingEnd, ct);`
     `trailingMean = Math.Round(completed.Sum(w => w.LoadOverride ?? w.ComputedLoad ?? 0m) / 4m, 2);`
     — a **fixed divisor of 4**: empty weeks contribute 0 (the ROADMAP math convention "zeros are
     load-bearing; never skip them"). If `trailingMean > 0m` → `(baseline, source) = (trailingMean, TrailingActual)`.
   - b. Else, first-week planned: from the plan-week planned read (step 4), sum the effective planned
     load of sessions in `[firstWeekStart, firstWeekStart.AddDays(6)]`; if `> 0m` →
     `(baseline, source) = (thatSum, FirstWeekPlanned)`.
   - c. Else `(baseline, source) = (null, None)`.
4. Planned per week (this plan only):
   `planned = await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, firstWeekStart, lastWeekEnd, ct);`
   then **filter `pw.TrainingPlanId == plan.Id`** (the repo read is athlete-wide across all plans —
   without this filter another plan's sessions would leak into this plan's weeks).
   `athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct); zones = await zoneService.GetZonesAsync(ct);`
   Per session: `effective = pw.PlannedLoad ?? LoadCalculator.ComputePlannedLoad(pw, profile, sportZones) ?? 0m`,
   accumulated into `plannedByWeek[WeekStart(pw.ScheduledDate)]` — verbatim `GetWeeklyLoadAsync` logic.
5. Actual per week (athlete-wide):
   `actuals = await workoutRepo.GetByAthleteInRangeAsync(athleteId, firstWeekStart, lastWeekEnd, ct);`
   accumulated into `actualByWeek[WeekStart(w.CompletedDate)] += w.LoadOverride ?? w.ComputedLoad ?? 0m`.
6. Event date: `DateOnly? eventDate = null;` when `plan.EventId is { } eventId`, read
   `eventRepo.GetByIdAsync(eventId, ct)` and use its `EventDate` **only if** `ev.AthleteId == athleteId`
   (defensive; the FK is `SetNull` and 18-2 already validates ownership on write).
7. `targets = WeeklyTargetCalculator.Compute(new WeeklyTargetInput(plan.StartDate, plan.EndDate,
   baseline, plan.BuildWeeks, plan.RecoveryWeeks, plan.RecoveryWeekPercentage, eventDate));`
8. Project each `WeeklyTargetDto` → `WeeklyTargetWeekDto`, filling
   `PlannedLoad = Math.Round(plannedByWeek.GetValueOrDefault(ws, 0m), 2)` and
   `ActualLoad = Math.Round(actualByWeek.GetValueOrDefault(ws, 0m), 2)`. Return the response with
   `PlanId`, the plan's `StartDate`/`EndDate`, `Baseline`, `BaselineSource`, `Weeks`.
   When `targets` is empty (`None` baseline) `Weeks` is empty — do **not** synthesise rows to carry the
   planned/actual figures; the Progress page already owns that view.
- No writes: no `IUnitOfWork`, no staging, no `SaveChangesAsync` anywhere in this service.

### Controller (`TrainingPlansController.cs` — additive)
- Ctor gains `IPeriodizationService periodizationService` (third parameter).
- Action, placed after the 18-2 `UpdateAsync`:
  ```csharp
  /// <summary>
  /// Returns the plan's computed weekly load targets (ADR-0009: trailing-4-week baseline, +7 %/build-week
  /// ramp, build:recovery cadence, two-week taper into a linked event) merged with the plan's planned
  /// load and the athlete's actual load per ISO week. Targets are computed on read — nothing is stored.
  /// An athlete with no usable baseline gets an empty week list. 404 if the plan is missing or foreign.
  /// </summary>
  [HttpGet("{id:guid}/weekly-targets")]
  public async Task<IActionResult> GetWeeklyTargetsAsync(Guid id, CancellationToken cancellationToken)
  {
      WeeklyTargetsResponse result = await periodizationService.GetWeeklyTargetsAsync(id, cancellationToken);
      return Ok(result);
  }
  ```
- No `[FromQuery]` parameters — the plan window *is* the range. No try/catch. Athlete never from route.

### DI (`api/Bryk.API/Program.cs`)
Exactly one added line, after the `ICalendarService` registration (L120), matching the neighbouring
fully-qualified style:
```csharp
builder.Services.AddScoped<Bryk.Application.Training.Periodization.IPeriodizationService, Bryk.Application.Training.Periodization.PeriodizationService>();
```

## Non-goals
- **No migration.** No `WeeklyTarget` table, no persisted override, no snapshot (ADR-0009 §4). If a read
  looks too slow — **STOP and ask**; do not add a cache or a table.
- **No new NuGet or npm package.**
- **Do not** add a repository method. Every read exists; if one seems to be missing, re-read
  `IWorkoutRepository` / `ITrainingPlanRepository` before proposing anything.
- **Do not modify** `AnalyticsService`, `WeeklyLoadCalculator.cs`, `ComplianceClassifier.cs`,
  `LoadChart.vue`, or `lib/charts/load.ts`.
- **Do not modify** `TrainingPlanRequest`/`TrainingPlanRequestValidator`, nor 18-2's
  `TrainingPlanUpdateRequest`/validator, nor 18-1's `WeeklyTargetCalculator` (if the calculator seems
  wrong, fix it in 18-1's file **with a new pinned test**, and say so in the commit body).
- **Do not** call `ITrainingPlanService` (or any other application service) from `PeriodizationService`
  — repositories only.
- **Do not** attribute actual load to a plan, split targets per sport, or generate planned *workouts*
  from targets. No multi-event season ATP, no coach overrides.
- **Do not** accept `from`/`to`/`weeks` query parameters — a plan's window is fixed and authoritative
  (ADR-0008 §2).
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.
- **No auth code** — Phase 12 stays deferred and approval-gated.

## Test expectations

**Unit — `api/Bryk.Application.Tests/Training/Periodization/PeriodizationServiceTests.cs` (new).**
Stubs in the `ThisWeekServiceTests` style: `StubCurrentUserService`, a `StubTrainingPlanRepository`
that returns a configured plan from `GetByIdAsync` and range-filters planned workouts, a
`StubWorkoutRepository` that range-filters a configured completion list, a `StubEventRepository`, a
`StubAthleteRepository` returning null, and a `StubZoneService` returning `new ZonesResponse()`.
All plan windows use fixed dates (`2026-01-05` Monday etc.) so nothing depends on the clock.
- `GetWeeklyTargetsAsync_ForeignPlan_ThrowsKeyNotFound` (and `_MissingPlan_ThrowsKeyNotFound`).
- `GetWeeklyTargetsAsync_TrailingFourWeeksOfActuals_UsesTrailingActualBaseline` — four completions of
  `200m` each in `[firstWeekStart − 28, firstWeekStart − 1]` → `Baseline == 200.00m`,
  `BaselineSource == TrailingActual`, `Weeks[0].TargetLoad == 200.00m`.
- `GetWeeklyTargetsAsync_TrailingWindowExcludesTheWeekBeforeItAndThePlanItself` — a completion on
  `firstWeekStart − 29` and one on `firstWeekStart` are both ignored by the baseline (pins the
  inclusive `[−28, −1]` bounds); with only those two, the baseline falls through to the next rule.
- `GetWeeklyTargetsAsync_PartialHistory_DividesByFourNotByWeeksPresent` — a single `200m` completion in
  the window → `Baseline == 50.00m` (zeros are load-bearing).
- `GetWeeklyTargetsAsync_NoHistory_FallsBackToFirstWeekPlannedLoad` — no completions, two planned
  sessions of `60m` + `40m` in the plan's first week → `Baseline == 100.00m`,
  `BaselineSource == FirstWeekPlanned`.
- `GetWeeklyTargetsAsync_NoHistoryAndNoPlannedWork_ReturnsNoTargets` → `Baseline == null`,
  `BaselineSource == None`, `Weeks.Should().BeEmpty()`.
- `GetWeeklyTargetsAsync_MergesPlannedAndActualPerWeek` — planned `120m` in week 1 and a completion of
  `90m` in week 1 → that week's `PlannedLoad == 120.00m`, `ActualLoad == 90.00m`, while its
  `TargetLoad` still comes from the ramp (assert all three, and that a week with neither reports
  `0.00`/`0.00`).
- `GetWeeklyTargetsAsync_IgnoresPlannedWorkoutsFromAnotherPlan` — the stub returns a planned workout
  with a different `TrainingPlanId` inside the window → it contributes **0** to `PlannedLoad` (pins the
  `TrainingPlanId` filter).
- `GetWeeklyTargetsAsync_LinkedInWindowEvent_ProducesTaperWeeks` — plan `2026-01-05 → 2026-03-29`,
  `EventId` resolving to `2026-03-28` → the last two weeks have `IsTaperWeek == true`; without the
  event link, none do.
- `GetWeeklyTargetsAsync_EventOwnedByAnotherAthlete_IsIgnored` — no taper weeks.
- `GetWeeklyTargetsAsync_ThreeBuildOneRecoverySixtyPercent_MatchesTheAdrVector` — plan
  `2026-01-05 → 2026-03-29`, cadence `3 : 1 @ 60.0m`, event `2026-03-28`, trailing actuals summing to
  `800m` (⇒ baseline `200.00m`) → the 12 `TargetLoad` values are **identical** to Task 18-1's pinned
  vector `[200.00, 214.00, 228.98, 137.39, 245.01, 262.16, 280.51, 168.31, 300.15, 321.16, 257.73,
  171.82]`. This is the end-to-end proof that the service feeds the calculator correctly.

**Integration — `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` (extend, or a new
`WeeklyTargetsControllerTests.cs` in the same folder if the file is getting long).** Seed through the
public API. Because `CompletedDate` may not be in the future, seed the plan with
`StartDate = today` so its trailing window lies in the past.
- `WeeklyTargets_MissingPlan_Returns404` / `WeeklyTargets_ForeignPlan_Returns404` (reuse the
  foreign-athlete seeding block).
- `WeeklyTargets_FreshAthlete_Returns200WithNoTargets` — a plan with no history and no planned load →
  200, `baselineSource == "None"`, `baseline == null`, `weeks` empty. **Not** a 404 and **not** a row of
  zeros.
- `WeeklyTargets_WithTrailingActuals_ReturnsRampingTargets` — `POST /workouts` four completions with
  `loadOverride = 200` on days `−28 … −1` relative to the plan's first Monday, then a 4-week plan → 200
  with `baselineSource == "TrailingActual"`, 4 weeks, strictly increasing `targetLoad`, and
  `weeks[0].targetLoad` equal to the echoed `baseline`.
- `WeeklyTargets_MergesTheAthletesActualLoad` — a completion inside the plan window shows up in that
  week's `actualLoad`.
- `WeeklyTargets_AfterPlanPutSetsCadence_TheDipAppears` (depends on 18-2) — PUT `buildWeeks = 3`,
  `recoveryWeeks = 1`, `recoveryWeekPercentage = 60` on the seeded plan, then GET weekly-targets →
  `weeks[3].isRecoveryWeek == true` and `weeks[3].targetLoad < weeks[2].targetLoad`. This is the
  ROADMAP success criterion end-to-end.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit rises from the post-18-2 count with zero failures; Vitest stays at **229 / 53 files**. Warning
count must not grow past the known 16.

## Review checklist
- [ ] `PeriodizationService` performs **no** writes and injects no `IUnitOfWork`.
- [ ] The baseline window is `[firstWeekStart − 28, firstWeekStart − 1]` with divisor **4**, anchored on
      the plan's first week (not today), and the anchoring rationale is in a comment.
- [ ] The fallback chain order and the "no baseline ⇒ empty `Weeks`" rule match ADR-0009 §1 exactly.
- [ ] Planned load is filtered to `TrainingPlanId == plan.Id`; actual load is athlete-wide and the
      asymmetry is documented on the DTO.
- [ ] All ramp math lives in `WeeklyTargetCalculator`; the service contains no multiplier constants.
- [ ] Ownership failure is `KeyNotFoundException` → 404 (never a 200 with an empty body).
- [ ] Exactly one new `Program.cs` line; no other `Program.cs` diff.
- [ ] The service-level 12-week vector matches Task 18-1's pinned vector value for value.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
```
feat: weekly-targets endpoint (baseline resolution + planned/actual merge)

GET /api/v1/trainingplans/{id}/weekly-targets computes a plan's ISO-week
load targets on read (ADR-0009 4 - no table, no migration) and merges them
with the plan's planned load and the athlete's actual load per week.

PeriodizationService owns the I/O half of the ramp model: the ADR-0009 1
baseline chain (trailing 4-week mean actual load over
[planStart-28, planStart-1] with a fixed divisor of 4, else the plan's own
first-week planned load, else no targets at all), the linked-event date for
the taper, and the two aggregations lifted verbatim from
AnalyticsService.GetWeeklyLoadAsync so Progress and the plan panel can never
disagree about a week's load. The baseline is anchored on the plan's first
week rather than today, so a plan's targets do not silently reshape every
Monday. All ramp math stays in the pure calculator.

Planned load is scoped to the plan; actual load is athlete-wide, because a
completed workout carries no plan attribution - documented on the DTO rather
than papered over. Fresh athletes get 200 with an empty week list and
baselineSource None, never a fabricated ramp. xUnit pins the baseline chain,
the trailing-window bounds, the cross-plan filter, the taper trigger, and
reproduces Task 18-1's 12-week vector end to end through the service.
```
