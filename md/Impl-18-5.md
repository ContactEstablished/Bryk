# Impl 18-5 — Build order: Dashboard tie-in — This Week target vs actual

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-18-5.md`. **Decision lock:** ADR-0008 §1 (the 5-bucket compliance
bands — `[0.8, 1.2]` green / `[0.5, 0.8) ∪ (1.2, ∞)` yellow / `< 0.5` red / planned-0 ⇒ ratio 1 —
reused verbatim, not refactored into a shared module) + ADR-0009 §1 (trailing-4-week baseline chain;
"no usable baseline ⇒ no targets" honesty rule), both from Task 18-3.
**Scope:** Backend then frontend — the API must be green end-to-end before the UI consumes it. No
migration, no new package, no new endpoint, no new component.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to
the next. Commit once at the end with the message in `Tasks-18-5.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`. `dotnet build api/Bryk.sln` green. `cd ui; pnpm run build` green.
- Record this task's starting test counts as the working baseline: `dotnet test api/Bryk.sln` and
  `cd ui; pnpm exec vitest run --no-file-parallelism`. Tasks 18-1 … 18-4 will already have raised both
  suites above the phase-start figures quoted in `Tasks-18-5.md` (**201** xUnit, **229 / 53 files**
  Vitest) — note the *actual* current numbers here; Step 13 must show both counts strictly higher, zero
  failures, and the known-warning count still ≤ 16.
- Confirm the hard dependency (Task 18-3) and the shared-file dependency (Task 18-4) actually landed —
  `ThisWeekService` will not compile without them. If any of the following is missing or shaped
  differently than described, **STOP**: that task is not done and must land first.
  - `api/Bryk.Application/Training/Periodization/IPeriodizationService.cs` —
    `Task<WeeklyTargetsResponse> GetWeeklyTargetsAsync(Guid planId, CancellationToken ct = default)`.
  - `api/Bryk.Application/Training/Periodization/WeeklyTargetsResponse.cs`, `WeeklyTargetWeekDto.cs`
    (`WeekStart`, `TargetLoad`, `IsRecoveryWeek`, `IsTaperWeek`, `PlannedLoad`, `ActualLoad`),
    `TargetBaselineSource.cs`.
  - `api/Bryk.API/Program.cs` registers `Bryk.Application.Training.Periodization.IPeriodizationService`
    → `PeriodizationService` (added by 18-3, after the `ICalendarService` line — confirm no further
    `Program.cs` edit is needed by this task).
  - `ui/src/types/training.ts` already carries 18-4's additive block (`TrainingPlanUpdateRequest`,
    `TargetBaselineSource`, `WeeklyTargetWeek`, `WeeklyTargetsResponse`) — confirms the file is safe to
    extend again in Step 7 without clobbering 18-4's edit.
- Re-read `md/Tasks-18-5.md` in full.
- Open in editor (backend): `api/Bryk.Application/Training/ThisWeekResponse.cs`, `ThisWeekService.cs`,
  `api/Bryk.Application/Analytics/AnalyticsService.cs:86–93` (the actual-load aggregation this step
  reuses), `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs:23` (`GetByAthleteIdAsync`),
  `IWorkoutRepository.cs:24` (`GetByAthleteInRangeAsync`),
  `api/Bryk.Application/Calendar/ComplianceClassifier.cs`,
  `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs`,
  `api/Bryk.API.Tests/Training/ThisWeekControllerTests.cs`.
- Open (frontend): `ui/src/types/training.ts`, `ui/src/lib/progressRing.ts` (the pure-helper-with-its-
  own-spec precedent), `ui/src/components/dashboard/ThisWeekCard.vue`,
  `ui/src/components/common/DeltaChip.vue`, `ui/src/style.css:147–149`,
  `ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts`.
- Confirm current shapes (already verified during spec-writing): `ThisWeekService`'s primary ctor is
  `(ICurrentUserService currentUser, ITrainingPlanRepository planRepo, IAthleteRepository athleteRepo,
  IZoneService zoneService)`; its private `CurrentWeek()` takes no parameters and computes
  `DateOnly.FromDateTime(DateTime.UtcNow)` internally; `IWorkoutRepository` is registered at
  `Program.cs:106`, `ITrainingPlanRepository` at `Program.cs:104` (both already available to the wider
  ctor — no DI change needed). `TrainingController` (`api/Bryk.API/Controllers/TrainingController.cs`)
  calls `thisWeekService.GetThisWeekAsync(cancellationToken)` and returns `Ok(result)` unconditionally —
  its signature does not change and it is not touched by this task.

## Step 1 — `ThisWeekResponse`: additive `TargetLoad` / `ActualLoad`

**File:** `api/Bryk.Application/Training/ThisWeekResponse.cs` — replace the file with:

```csharp
namespace Bryk.Application.Training;

// Read model for the dashboard This Week card: the Mon–Sun (UTC) week window plus the athlete's
// planned workouts within it, flattened across all plans and ordered by date. Reuses
// PlannedWorkoutResponse (Task 9-3), whose TrainingPlanId tells the card which plan a session belongs to.
public class ThisWeekResponse
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    // Σ of each workout's effective load over the week (ADR-0005 §3). Null effective loads count as 0.
    public decimal WeeklyLoad { get; set; }
    // The week's load target from the athlete's active plan (ADR-0009). Null when no plan covers today,
    // or when the plan has no usable baseline — the card then renders exactly as it did before Phase 18.
    public decimal? TargetLoad { get; set; }
    // Σ EffectiveLoad (LoadOverride ?? ComputedLoad) of the athlete's completed workouts in the week.
    public decimal ActualLoad { get; set; }
    public IReadOnlyList<PlannedWorkoutResponse> PlannedWorkouts { get; set; } = new List<PlannedWorkoutResponse>();
}
```

`WeekStart`, `WeekEnd`, `WeeklyLoad`, `PlannedWorkouts` keep their exact names/types/meaning —
`WeeklyLoad` still means *planned*. This is additive; `TargetLoad` defaults to `null` and `ActualLoad`
defaults to `0m`, so the type still compiles cleanly before `ThisWeekService` populates them in Step 2.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 2 — `ThisWeekService`: wider ctor, actual load, plan selection, target lookup

**File:** `api/Bryk.Application/Training/ThisWeekService.cs` — replace the file with:

```csharp
using Bryk.Application.Common;
using Bryk.Application.Training.Load;
using Bryk.Application.Training.Periodization;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;

namespace Bryk.Application.Training;

public class ThisWeekService(
    ICurrentUserService currentUser,
    ITrainingPlanRepository planRepo,
    IAthleteRepository athleteRepo,
    IZoneService zoneService,
    IWorkoutRepository workoutRepo,
    IPeriodizationService periodization) : IThisWeekService
{
    public async Task<ThisWeekResponse> GetThisWeekAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        // Computed once (Task 18-5) and threaded into CurrentWeek and the plan-selection check below,
        // rather than each reading DateTime.UtcNow independently.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (weekStart, weekEnd) = CurrentWeek(today);

        // Load the week's workouts WITH structure, plus the athlete's profiles + effective zones once,
        // so the per-workout load computation is a single set of round-trips (ADR-0005 §3 read-cost note).
        var workouts = await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, weekStart, weekEnd, ct);
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var zones = await zoneService.GetZonesAsync(ct);

        var planned = workouts.Select(w => Map(w, athlete, zones)).ToList();
        var weeklyLoad = Math.Round(planned.Sum(p => p.EffectiveLoad ?? 0m), 2);

        // Actual load (Task 18-5): Σ EffectiveLoad (LoadOverride ?? ComputedLoad) of the athlete's
        // completed workouts in the week — the dashboard's only actual-load source until now.
        var completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, weekStart, weekEnd, ct);
        var actualLoad = Math.Round(completed.Sum(w => w.LoadOverride ?? w.ComputedLoad ?? 0m), 2);

        // Active-plan selection (ADR-0009 / Phase 18 decision): the plan whose window contains today,
        // ties broken by the latest StartDate (the most recently begun plan wins an overlap). No plan
        // covering today -> TargetLoad stays null and the periodization service is never called.
        var plans = await planRepo.GetByAthleteIdAsync(athleteId, ct);
        var active = plans
            .Where(p => p.StartDate <= today && today <= p.EndDate)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefault();

        decimal? targetLoad = null;
        if (active is not null)
        {
            // Reuses IPeriodizationService's ramp math rather than duplicating it here — an extra
            // plan/workout read on every dashboard call, accepted for v1 over a second copy of the
            // baseline chain (Tasks-18-5). A plan whose targets are empty (no baseline) or whose window
            // does not include the current ISO week yields null — never a value interpolated, clamped,
            // or borrowed from WeeklyLoad.
            var targets = await periodization.GetWeeklyTargetsAsync(active.Id, ct);
            targetLoad = targets.Weeks.FirstOrDefault(w => w.WeekStart == weekStart)?.TargetLoad;
        }

        return new ThisWeekResponse
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            WeeklyLoad = weeklyLoad,
            TargetLoad = targetLoad,
            ActualLoad = actualLoad,
            PlannedWorkouts = planned
        };
    }

    // Monday-based week in UTC, matching how the domain treats DateOnly elsewhere
    // (e.g. EventDtoValidator uses DateOnly.FromDateTime(DateTime.UtcNow) as "today").
    // ((int)DayOfWeek + 6) % 7 maps Mon→0 … Sun→6, so subtracting it lands on Monday.
    // `today` is passed in (Task 18-5) rather than read here, so the caller computes
    // DateOnly.FromDateTime(DateTime.UtcNow) exactly once and reuses it for plan selection — the
    // Monday math itself is unchanged.
    private static (DateOnly Start, DateOnly End) CurrentWeek(DateOnly today)
    {
        var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (start, start.AddDays(6));
    }

    private static PlannedWorkoutResponse Map(PlannedWorkout pw, Athlete? athlete, ZonesResponse zones)
    {
        var profile = athlete?.SportProfiles.FirstOrDefault(p => p.Sport == pw.Sport);
        var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == pw.Sport);
        var computed = LoadCalculator.ComputePlannedLoad(pw, profile, sportZones);

        return new PlannedWorkoutResponse
        {
            Id = pw.Id,
            TrainingPlanId = pw.TrainingPlanId,
            Sport = pw.Sport,
            ScheduledDate = pw.ScheduledDate,
            Title = pw.Title,
            Description = pw.Description,
            PlannedDurationMinutes = pw.PlannedDurationMinutes,
            PlannedLoad = pw.PlannedLoad,
            ComputedLoad = computed,
            IsLoadOverride = pw.PlannedLoad is not null,
            EffectiveLoad = pw.PlannedLoad ?? computed
            // Blocks intentionally omitted — This Week shows the load number, not the structure.
        };
    }
}
```

Note what did **not** change: `Map`, the planned-workout read, and the Monday-math *formula* inside
`CurrentWeek` are byte-identical to before — only its parameter list changed so `today` is computed
once. The service still performs no writes: no `IUnitOfWork`, no staging, nothing beyond the five reads
above. Do not touch `WeeklyLoadCalculator.cs` or `ComplianceClassifier.cs` — this step reads
`ComplianceClassifier` only as a reference for Step 8's client thresholds, never imports it.

**Verify:** `dotnet build api/Bryk.sln` green. This will **fail** until Step 3 widens the test stubs
(the four existing `ThisWeekServiceTests` construct `ThisWeekService` directly and will not compile
against the new 6-parameter ctor) — that is expected; proceed straight to Step 3 before trying to build
the test project in isolation. `dotnet build api/Bryk.sln` (the `Bryk.Application` project alone) should
still be green since only the test project references the old ctor shape.

## Step 3 — Widen the `ThisWeekServiceTests` harness (its own step; no new test cases yet)

**File:** `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs`. This step only widens the
stubs/factory so the four pre-existing `[Fact]`s keep passing against the new ctor. New test *cases*
land in Step 4.

Add the using:

```csharp
using Bryk.Application.Training.Periodization;
```

Replace the `NewService` factory (currently a single `params PlannedWorkout[] workouts` method) with two
overloads — the existing call sites (`NewService(Workout(...))`, `NewService(a, b)`, `NewService()`)
keep compiling unchanged because the single-array overload is preserved verbatim and simply delegates:

```csharp
private static ThisWeekService NewService(params PlannedWorkout[] workouts) =>
    NewService(workouts, plans: [], completions: [], periodization: new StubPeriodizationService());

private static ThisWeekService NewService(
    IEnumerable<PlannedWorkout> workouts,
    IEnumerable<TrainingPlan> plans,
    IEnumerable<Workout> completions,
    StubPeriodizationService periodization) =>
    new(new StubCurrentUserService(AthleteId),
        new StubTrainingPlanRepository(workouts, plans),
        new StubAthleteRepository(),
        new StubZoneService(),
        new StubWorkoutRepository(completions),
        periodization);

// Mirrors ThisWeekService.CurrentWeek's Monday math, so fixtures can be built against the exact week
// the service will compute without depending on the production formula directly.
private static DateOnly ThisMonday() => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

private static TrainingPlan Plan(DateOnly start, DateOnly end, Guid? athleteId = null) => new()
{
    Id = Guid.NewGuid(),
    AthleteId = athleteId ?? AthleteId,
    Name = "Plan",
    Methodology = MethodologyChoice.Polarized,
    StartDate = start,
    EndDate = end
};

// A completed Workout fixture, distinct from the file's existing `Workout(...)` helper (which builds a
// *planned* workout) — named Completion to keep the two unambiguous at call sites.
private static Workout Completion(DateOnly date, decimal? loadOverride = null, decimal? computedLoad = null, Guid? athleteId = null) => new()
{
    Id = Guid.NewGuid(),
    AthleteId = athleteId ?? AthleteId,
    Sport = Sport.Run,
    CompletedDate = date,
    LoadOverride = loadOverride,
    ComputedLoad = computedLoad
};
```

Widen `StubTrainingPlanRepository` to take plans and stop throwing from `GetByAthleteIdAsync` (this is
the L112 fix the Tasks doc calls out):

```csharp
private sealed class StubTrainingPlanRepository(IEnumerable<PlannedWorkout> workouts, IEnumerable<TrainingPlan> plans) : ITrainingPlanRepository
{
    private readonly List<PlannedWorkout> _workouts = workouts.ToList();
    private readonly List<TrainingPlan> _plans = plans.ToList();

    public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PlannedWorkout>>(
            _workouts
                .Where(w => w.AthleteId == athleteId && w.ScheduledDate >= start && w.ScheduledDate <= end)
                .OrderBy(w => w.ScheduledDate)
                .ThenBy(w => w.Sport)
                .ToList());

    public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TrainingPlan>>(
            _plans.Where(p => p.AthleteId == athleteId).OrderBy(p => p.StartDate).ToList());

    public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default) => throw new NotImplementedException();

    public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(TrainingPlan entity, CancellationToken ct = default) => throw new NotImplementedException();
    public void Update(TrainingPlan entity) => throw new NotImplementedException();
    public void Delete(TrainingPlan entity) => throw new NotImplementedException();
    public Task AddPlannedWorkoutAsync(PlannedWorkout plannedWorkout, CancellationToken ct = default) => throw new NotImplementedException();
    public void UpdatePlannedWorkout(PlannedWorkout plannedWorkout) => throw new NotImplementedException();
    public void RemovePlannedWorkout(PlannedWorkout plannedWorkout) => throw new NotImplementedException();

    public Task<PlannedWorkout?> GetPlannedWorkoutWithStructureAsync(Guid plannedWorkoutId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddWorkoutBlockAsync(WorkoutBlock block, CancellationToken ct = default) => throw new NotImplementedException();
    public void UpdateWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
    public void RemoveWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
    public Task AddWorkoutStepAsync(WorkoutStep step, CancellationToken ct = default) => throw new NotImplementedException();
    public void UpdateWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
    public void RemoveWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
}
```

Add the two new stubs (same `private sealed class`, unused-members-throw style as every existing stub
in this file):

```csharp
private sealed class StubWorkoutRepository(IEnumerable<Workout> completions) : IWorkoutRepository
{
    private readonly List<Workout> _completions = completions.ToList();

    public Task<IReadOnlyList<Workout>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Workout>>(
            _completions.Where(w => w.AthleteId == athleteId && w.CompletedDate >= start && w.CompletedDate <= end).ToList());

    public Task<Workout?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Workout?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Workout>> GetByAthleteFilteredAsync(Guid athleteId, DateOnly? from, DateOnly? to, Sport? sport, int skip, int take, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DateOnly?> GetFirstWorkoutDateAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Workout>> GetByAthleteWithStepResultsAsync(Guid athleteId, Sport? sport, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(Workout workout, CancellationToken ct = default) => throw new NotImplementedException();
    public void Update(Workout workout) => throw new NotImplementedException();
    public void Delete(Workout workout) => throw new NotImplementedException();
}

private sealed class StubPeriodizationService : IPeriodizationService
{
    public List<Guid> CalledPlanIds { get; } = [];
    public WeeklyTargetsResponse Response { get; set; } = new();

    public Task<WeeklyTargetsResponse> GetWeeklyTargetsAsync(Guid planId, CancellationToken ct = default)
    {
        CalledPlanIds.Add(planId);
        return Task.FromResult(Response);
    }
}
```

**Verify:** `dotnet test api/Bryk.sln` — build green, and the four pre-existing
`ThisWeekServiceTests` facts (`GetThisWeekAsync_ReturnsWorkoutScheduledThisWeek_WithMondaySundayRange`,
`GetThisWeekAsync_ExcludesWorkoutsInAdjacentWeeks`, `GetThisWeekAsync_NoPlannedWorkouts_ReturnsEmptyListWithWeekRange`,
`GetThisWeekAsync_WeeklyLoad_SumsEffectiveLoad`) still pass unmodified. No new test yet.

## Step 4 — New `ThisWeekServiceTests` cases (7 facts)

**File:** `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs` — add below the four existing
facts:

```csharp
[Fact]
public async Task GetThisWeekAsync_NoPlanCoversToday_TargetLoadIsNull()
{
    var periodization = new StubPeriodizationService();
    TrainingPlan[] plans =
    [
        Plan(Today.AddDays(-30), Today.AddDays(-1)),  // ended yesterday
        Plan(Today.AddDays(1), Today.AddDays(30))     // starts tomorrow
    ];
    var service = NewService([], plans, [], periodization);

    var result = await service.GetThisWeekAsync();

    result.TargetLoad.Should().BeNull();
    periodization.CalledPlanIds.Should().BeEmpty();
}

[Fact]
public async Task GetThisWeekAsync_PlanCoveringToday_ReturnsThisWeeksTarget()
{
    var plan = Plan(Today.AddDays(-7), Today.AddDays(21));
    var periodization = new StubPeriodizationService
    {
        Response = new WeeklyTargetsResponse
        {
            Weeks = [new WeeklyTargetWeekDto { WeekStart = ThisMonday(), TargetLoad = 320.00m }]
        }
    };
    var service = NewService([], [plan], [], periodization);

    var result = await service.GetThisWeekAsync();

    result.TargetLoad.Should().Be(320.00m);
}

[Fact]
public async Task GetThisWeekAsync_OverlappingPlans_PicksTheLatestStartDate()
{
    var earlier = Plan(Today.AddDays(-60), Today.AddDays(60));
    var later = Plan(Today.AddDays(-10), Today.AddDays(10));
    var periodization = new StubPeriodizationService();
    var service = NewService([], [earlier, later], [], periodization);

    await service.GetThisWeekAsync();

    periodization.CalledPlanIds.Should().ContainSingle().Which.Should().Be(later.Id);
}

[Fact]
public async Task GetThisWeekAsync_PlanWithNoTargets_TargetLoadIsNull()
{
    var plannedSession = Workout(Today, "Session");
    plannedSession.PlannedLoad = 100m; // proves there is no fallback to WeeklyLoad
    var plan = Plan(Today.AddDays(-7), Today.AddDays(21));
    var periodization = new StubPeriodizationService { Response = new WeeklyTargetsResponse { Weeks = [] } };
    var service = NewService([plannedSession], [plan], [], periodization);

    var result = await service.GetThisWeekAsync();

    result.WeeklyLoad.Should().Be(100m);
    result.TargetLoad.Should().BeNull();
}

[Fact]
public async Task GetThisWeekAsync_TargetsMissingTheCurrentWeek_TargetLoadIsNull()
{
    var plan = Plan(Today.AddDays(-7), Today.AddDays(21));
    var periodization = new StubPeriodizationService
    {
        Response = new WeeklyTargetsResponse
        {
            Weeks = [new WeeklyTargetWeekDto { WeekStart = ThisMonday().AddDays(-14), TargetLoad = 999m }]
        }
    };
    var service = NewService([], [plan], [], periodization);

    var result = await service.GetThisWeekAsync();

    result.TargetLoad.Should().BeNull();
}

[Fact]
public async Task GetThisWeekAsync_ActualLoad_SumsEffectiveLoadOfTheWeeksCompletions()
{
    var inWeekOverride = Completion(Today, loadOverride: 40m);
    var inWeekComputed = Completion(Today, computedLoad: 25m);
    // -10 days is always before this week's Monday (mirrors the adjacent-week test's rationale above).
    var outOfWeek = Completion(Today.AddDays(-10), loadOverride: 999m);
    var service = NewService([], [], [inWeekOverride, inWeekComputed, outOfWeek], new StubPeriodizationService());

    var result = await service.GetThisWeekAsync();

    result.ActualLoad.Should().Be(65.00m);
}

[Fact]
public async Task GetThisWeekAsync_NoCompletions_ActualLoadIsZero()
{
    var service = NewService();

    var result = await service.GetThisWeekAsync();

    result.ActualLoad.Should().Be(0m);
}
```

**Verify:** `dotnet test api/Bryk.sln` — the 7 new facts pass, the 4 pre-existing facts still pass,
nothing else broke.

## Step 5 — Integration tests: extend `ThisWeekControllerTests`

**File:** `api/Bryk.API.Tests/Training/ThisWeekControllerTests.cs`. Add the using:

```csharp
using Bryk.Application.Training.Workouts;
```

Append two facts:

```csharp
[Fact]
public async Task GetThisWeek_FreshAthlete_ReturnsNullTargetAndZeroActual()
{
    await using var factory = new BrykWebApplicationFactory();
    var client = factory.CreateClient();

    var response = await client.GetAsync("/api/v1/training/this-week");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var thisWeek = await response.Content.ReadFromJsonAsync<ThisWeekResponse>(JsonOptions);
    thisWeek.Should().NotBeNull();
    thisWeek!.TargetLoad.Should().BeNull();
    thisWeek.ActualLoad.Should().Be(0m);
}

[Fact]
public async Task GetThisWeek_WithAnActivePlanAndHistory_ReturnsATarget()
{
    await using var factory = new BrykWebApplicationFactory();
    var client = factory.CreateClient();

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

    // Trailing 4-week baseline (ADR-0009 §1): four completions of 200 TSS each inside
    // [thisMonday-28, thisMonday-1] — all in the past, since CompletedDate may not be in the future.
    for (var i = 1; i <= 4; i++)
    {
        await client.PostAsJsonAsync("/api/v1/workouts", new LogWorkoutRequest
        {
            Sport = Sport.Bike,
            CompletedDate = thisMonday.AddDays(-7 * i),
            ActualDurationSeconds = 3600,
            LoadOverride = 200m
        });
    }

    // One completion inside the current week, so ActualLoad reflects it.
    await client.PostAsJsonAsync("/api/v1/workouts", new LogWorkoutRequest
    {
        Sport = Sport.Run,
        CompletedDate = today,
        ActualDurationSeconds = 1800,
        LoadOverride = 50m
    });

    // Plan starts this week's Monday, so its window contains today and its first target week aligns
    // with ThisWeekService's own weekStart.
    var plan = new TrainingPlanRequest
    {
        Name = "Active Block",
        Methodology = MethodologyChoice.Polarized,
        StartDate = thisMonday,
        EndDate = thisMonday.AddDays(27)
    };
    (await client.PostAsJsonAsync("/api/v1/trainingplans", plan)).StatusCode.Should().Be(HttpStatusCode.Created);

    var response = await client.GetAsync("/api/v1/training/this-week");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var thisWeek = await response.Content.ReadFromJsonAsync<ThisWeekResponse>(JsonOptions);
    thisWeek.Should().NotBeNull();
    // Week 0's target equals the resolved baseline exactly (no ramp has applied yet) — deterministic
    // given the trailing mean above, so this pins an exact value rather than just "non-null".
    thisWeek!.TargetLoad.Should().Be(200.00m);
    thisWeek.ActualLoad.Should().Be(50.00m);
}
```

**Verify:** `dotnet test api/Bryk.sln` — both new facts pass, and the two pre-existing
`ThisWeekControllerTests` facts (`GetThisWeek_ReturnsInWeekPlannedWorkout`,
`GetThisWeek_NoPlans_Returns200WithEmptyList`) still pass. Note: the pre-existing
`GetThisWeek_ReturnsInWeekPlannedWorkout` seeds a plan whose window also contains today
(`StartDate = today.AddDays(-7)`), so it now exercises the real `PeriodizationService` too — it makes no
assertion on `TargetLoad`/`ActualLoad`, so this is a pass-through smoke check, not a behavior change.

## Step 6 — Backend full verification (API green before touching the UI)

- `dotnet build api/Bryk.sln` — 0 errors, warning count unchanged from Step 0's recorded baseline.
- `dotnet test api/Bryk.sln` — all green: the 7 new `ThisWeekServiceTests` facts, the 2 new
  `ThisWeekControllerTests` facts, and every pre-existing test (including the 4 widened-stub
  `ThisWeekServiceTests` facts) pass. Total count is at least 9 higher than Step 0's recorded xUnit
  baseline.
- `git diff --stat` sanity check so far — only
  `api/Bryk.Application/Training/ThisWeekResponse.cs`, `ThisWeekService.cs`,
  `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs`,
  `api/Bryk.API.Tests/Training/ThisWeekControllerTests.cs` should show as changed. No `Program.cs`, no
  controller, no migration, no `.csproj`. If anything else appears, **STOP** — that is scope creep.

Do not proceed to the frontend until this step is fully green.

## Step 7 — `ui/src/types/training.ts`: additive `targetLoad` / `actualLoad`

**File:** `ui/src/types/training.ts`. Replace the `ThisWeekResponse` interface (currently at
lines 29–35) with:

```ts
// Mirrors Bryk.Application.Training.ThisWeekResponse.
export interface ThisWeekResponse {
  weekStart: string
  weekEnd: string
  weeklyLoad?: number
  targetLoad?: number | null
  actualLoad?: number
  plannedWorkouts: PlannedWorkoutResponse[]
}
```

Both new fields are optional, matching the existing `weeklyLoad?: number` — the three existing
`ThisWeekCard.spec.ts` fixtures (which omit them entirely) still type-check unchanged. Do not touch any
other interface in the file (18-4's `TrainingPlanUpdateRequest` / `WeeklyTargetsResponse` block stays as
landed).

**Verify:** `pnpm run build` green (`vue-tsc -b` — a stray type error here fails the whole build, which
is the point).

## Step 8 — New pure helper `ui/src/lib/weeklyTarget.ts`

**New file** `ui/src/lib/weeklyTarget.ts`:

```ts
export type TargetState = 'good' | 'warn' | 'bad'

export interface TargetProgress {
  ratio: number
  state: TargetState
  dir: 'up' | 'down' | 'flat'
  deltaLabel: string
  widthPct: number
}

// ADR-0008 §1's compliance bands, copied verbatim (Tasks-18-5) — ComplianceClassifier.cs stays the
// server-side source of truth; this is the client's own char-for-char mirror, not a shared module.
const GREEN_LOWER = 0.8
const GREEN_UPPER = 1.2
const YELLOW_LOWER = 0.5

// Pure: no Vue, no Date, no imports. actual/target -> the bar's fraction, band, direction and label.
export function buildTargetProgress(actual: number, target: number): TargetProgress {
  // Degenerate div-by-zero guard, same as ComplianceClassifier.Ratio: planned/target 0 -> ratio 1.
  const ratio = target === 0 ? 1 : actual / target

  const state: TargetState =
    ratio >= GREEN_LOWER && ratio <= GREEN_UPPER ? 'good' : ratio >= YELLOW_LOWER ? 'warn' : 'bad'

  // DeltaChip colours `up` green and `down` red — it reports the *direction of the delta*, not the
  // compliance band; `state` (and the bar's colour) carries the honest band. Do not "fix" DeltaChip to
  // match `state` — they are deliberately different signals rendered side by side.
  const dir: TargetProgress['dir'] = ratio > GREEN_UPPER ? 'up' : ratio < GREEN_LOWER ? 'down' : 'flat'

  const d = Math.round(actual - target)
  const deltaLabel = `${d > 0 ? '+' : ''}${d} TSS`

  const widthPct = Math.round(Math.min(100, Math.max(0, ratio * 100)))

  return { ratio, state, dir, deltaLabel, widthPct }
}
```

**Verify:** `pnpm run build` green (nothing imports this yet, but it must still type-check standalone).

## Step 9 — `ui/src/lib/__tests__/weeklyTarget.spec.ts` (pin every boundary)

Transcribed from `Tasks-18-5.md` — every row must be pinned exactly:

| actual | target | state | dir | deltaLabel | widthPct |
|---|---|---|---|---|---|
| 80 | 100 | `good` | `flat` | `-20 TSS` | 80 |
| 79 | 100 | `warn` | `down` | `-21 TSS` | 79 |
| 100 | 100 | `good` | `flat` | `0 TSS` | 100 |
| 120 | 100 | `good` | `flat` | `+20 TSS` | 100 |
| 121 | 100 | `warn` | `up` | `+21 TSS` | 100 |
| 50 | 100 | `warn` | `down` | `-50 TSS` | 50 |
| 49 | 100 | `bad` | `down` | `-51 TSS` | 49 |
| 0 | 0 | `good` | `flat` | `0 TSS` | 100 |

(the last row pins the div-by-zero guard: ratio 1 ⇒ good, full width.)

**New file** `ui/src/lib/__tests__/weeklyTarget.spec.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { buildTargetProgress } from '@/lib/weeklyTarget'

describe('buildTargetProgress', () => {
  it('actual 80 / target 100 -> good, flat, -20 TSS, 80% (green floor inclusive)', () => {
    const p = buildTargetProgress(80, 100)
    expect(p.state).toBe('good')
    expect(p.dir).toBe('flat')
    expect(p.deltaLabel).toBe('-20 TSS')
    expect(p.widthPct).toBe(80)
  })

  it('actual 79 / target 100 -> warn, down, -21 TSS, 79% (just under the green floor)', () => {
    const p = buildTargetProgress(79, 100)
    expect(p.state).toBe('warn')
    expect(p.dir).toBe('down')
    expect(p.deltaLabel).toBe('-21 TSS')
    expect(p.widthPct).toBe(79)
  })

  it('actual 100 / target 100 -> good, flat, 0 TSS, 100%', () => {
    const p = buildTargetProgress(100, 100)
    expect(p.state).toBe('good')
    expect(p.dir).toBe('flat')
    expect(p.deltaLabel).toBe('0 TSS')
    expect(p.widthPct).toBe(100)
  })

  it('actual 120 / target 100 -> good, flat, +20 TSS, 100% (green ceiling inclusive)', () => {
    const p = buildTargetProgress(120, 100)
    expect(p.state).toBe('good')
    expect(p.dir).toBe('flat')
    expect(p.deltaLabel).toBe('+20 TSS')
    expect(p.widthPct).toBe(100)
  })

  it('actual 121 / target 100 -> warn, up, +21 TSS, 100% (just over the green ceiling)', () => {
    const p = buildTargetProgress(121, 100)
    expect(p.state).toBe('warn')
    expect(p.dir).toBe('up')
    expect(p.deltaLabel).toBe('+21 TSS')
    expect(p.widthPct).toBe(100)
  })

  it('actual 50 / target 100 -> warn, down, -50 TSS, 50% (yellow floor inclusive)', () => {
    const p = buildTargetProgress(50, 100)
    expect(p.state).toBe('warn')
    expect(p.dir).toBe('down')
    expect(p.deltaLabel).toBe('-50 TSS')
    expect(p.widthPct).toBe(50)
  })

  it('actual 49 / target 100 -> bad, down, -51 TSS, 49% (just under the yellow floor)', () => {
    const p = buildTargetProgress(49, 100)
    expect(p.state).toBe('bad')
    expect(p.dir).toBe('down')
    expect(p.deltaLabel).toBe('-51 TSS')
    expect(p.widthPct).toBe(49)
  })

  it('actual 0 / target 0 -> good, flat, 0 TSS, 100% (div-by-zero guard: ratio defaults to 1)', () => {
    const p = buildTargetProgress(0, 0)
    expect(p.state).toBe('good')
    expect(p.dir).toBe('flat')
    expect(p.deltaLabel).toBe('0 TSS')
    expect(p.widthPct).toBe(100)
  })
})
```

**Verify:** `pnpm exec vitest run ui/src/lib/__tests__/weeklyTarget.spec.ts --no-file-parallelism` — all
8 pass.

## Step 10 — `ThisWeekCard.vue`: the bar + `DeltaChip`

**File:** `ui/src/components/dashboard/ThisWeekCard.vue`.

Script additions — widen the import block and add a `targetProgress` computed after the existing
`sessions` computed:

```ts
import { computed, onMounted } from 'vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import { buildTargetProgress } from '@/lib/weeklyTarget'
import { useTrainingStore } from '@/stores/training'
```

```ts
const sessions = computed(() => store.thisWeek?.plannedWorkouts ?? [])

// Task 18-5: null when there's no active plan or no usable target — the card then renders exactly as
// it did before Phase 18 (no placeholder bar, no dash row).
const targetProgress = computed(() => {
  const tw = store.thisWeek
  if (!tw || tw.targetLoad == null) return null
  return buildTargetProgress(tw.actualLoad ?? 0, tw.targetLoad)
})
```

Template addition — insert as the **first** child of `<div class="p-6">`, before the `<!-- Loading -->`
paragraph:

```html
<div class="p-6">
  <!-- Target vs actual (Task 18-5); absent entirely when there's no active plan/target — no
       placeholder bar, no "—", no dash row (Phase 17 parity). -->
  <div v-if="targetProgress" class="mb-4 flex flex-col gap-1.5">
    <div class="flex items-center justify-between">
      <span class="font-mono text-[11px] text-muted-foreground">
        {{ store.thisWeek?.actualLoad ?? 0 }} / {{ store.thisWeek?.targetLoad ?? 0 }} TSS
      </span>
      <DeltaChip :dir="targetProgress.dir">{{ targetProgress.deltaLabel }}</DeltaChip>
    </div>
    <div
      class="h-1.5 rounded-full bg-muted overflow-hidden"
      role="progressbar"
      :aria-valuenow="targetProgress.widthPct"
      aria-valuemin="0"
      aria-valuemax="100"
      :aria-label="`Weekly load: ${store.thisWeek?.actualLoad ?? 0} of ${store.thisWeek?.targetLoad ?? 0} TSS`"
    >
      <div
        class="h-full rounded-full"
        :class="targetProgress.state === 'good' ? 'bg-good' : targetProgress.state === 'warn' ? 'bg-warn' : 'bg-bad'"
        :style="{ width: targetProgress.widthPct + '%' }"
      />
    </div>
  </div>

  <!-- Loading (this week not yet fetched) -->
  <p v-if="!store.thisWeek" class="text-sm text-muted-foreground">Loading…</p>
  ...
```

(`...` = the rest of the `p-6` body — the `<ul>` session list and the empty-state paragraph — is
**unchanged**.) The header (`… TSS planned` at line 62) is also unchanged: planned ≠ target, so that
string is not repurposed. No new import beyond `DeltaChip` and `buildTargetProgress`; `DeltaChip.vue`
itself is not edited — its `up`=green / `down`=red mapping is reused exactly as it reports the delta's
direction, while the bar's `bg-good`/`bg-warn`/`bg-bad` carries the honest band colour.

**Verify:** `pnpm run build` green.

## Step 11 — Extend `ThisWeekCard.spec.ts`

**File:** `ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts`. Add the import:

```ts
import DeltaChip from '@/components/common/DeltaChip.vue'
```

Append inside the existing `describe('ThisWeekCard', ...)` block, **after** the three existing `it`
blocks — do not modify those three:

```ts
  it('renders the target-vs-actual bar when a target is present', () => {
    const thisWeek: ThisWeekResponse = {
      weekStart: '2099-01-05',
      weekEnd: '2099-01-11',
      plannedWorkouts: [],
      targetLoad: 300,
      actualLoad: 240,
    }
    const wrapper = mountCard(thisWeek)

    expect(wrapper.text()).toContain('240 / 300 TSS')
    expect(wrapper.text()).toContain('-60 TSS')

    const bar = wrapper.find('[role="progressbar"]')
    expect(bar.exists()).toBe(true)
    expect(bar.attributes('aria-valuenow')).toBe('80')
    const fill = bar.find('div')
    expect(fill.classes()).toContain('bg-good')
    expect(fill.attributes('style')).toContain('width: 80%')

    wrapper.unmount()
  })

  it('flips the bar state when the athlete falls behind', () => {
    const thisWeek: ThisWeekResponse = {
      weekStart: '2099-01-05',
      weekEnd: '2099-01-11',
      plannedWorkouts: [],
      targetLoad: 300,
      actualLoad: 100,
    }
    const wrapper = mountCard(thisWeek)

    const bar = wrapper.find('[role="progressbar"]')
    const fill = bar.find('div')
    expect(fill.classes()).toContain('bg-bad')
    expect(wrapper.findComponent(DeltaChip).props('dir')).toBe('down')

    wrapper.unmount()
  })

  it('renders no target-vs-actual bar when targetLoad is null (Phase 17 DOM unchanged)', () => {
    const fixtures: (ThisWeekResponse | null)[] = [
      null, // loading
      { weekStart: '2099-01-05', weekEnd: '2099-01-11', plannedWorkouts: [] }, // empty, no target fields
      {
        weekStart: '2099-01-05',
        weekEnd: '2099-01-11',
        plannedWorkouts: [makeWorkout({ id: '1', title: 'Easy Run', sport: 'Run', scheduledDate: '2099-01-06' })],
      }, // populated, no target fields
    ]

    for (const thisWeek of fixtures) {
      const wrapper = mountCard(thisWeek)

      expect(wrapper.find('[role="progressbar"]').exists()).toBe(false)
      expect(wrapper.findComponent(DeltaChip).exists()).toBe(false)

      wrapper.unmount()
    }
  })
```

The three **existing** tests (`renders this week's planned workouts...`, `renders the static empty
state...`, `shows Loading… before data is present`) stay exactly as they are — untouched, still passing,
still the DOM-parity proof for the null-target case (their fixtures never set `targetLoad`/`actualLoad`
at all).

**Verify:**
`pnpm exec vitest run ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts --no-file-parallelism`
— all 6 tests (3 pre-existing + 3 new) pass.

## Step 12 — Frontend full verification

- `pnpm run build` (from `ui/`) — `vue-tsc -b && vite build` green.
- `pnpm exec vitest run --no-file-parallelism` (from `ui/`) — full suite, not just the new files (confirms
  no regression elsewhere, e.g. `MetricTile.vue`'s own `DeltaChip` usage). Re-run once before debugging a
  worker crash reporting all tests passed (known transient fork quirk).
- `git diff --stat` sanity check — only `ui/src/types/training.ts`,
  `ui/src/components/dashboard/ThisWeekCard.vue`,
  `ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts` (edits) plus new
  `ui/src/lib/weeklyTarget.ts` and `ui/src/lib/__tests__/weeklyTarget.spec.ts` should show. Confirm
  `ui/src/components/common/DeltaChip.vue` is **absent** from the diff — if it appears, revert that
  hunk; it must not be touched.

## Step 13 — Final verification, manual smoke, and commit

- `dotnet build api/Bryk.sln` — 0 errors, warnings unchanged from Step 0's recorded baseline (≤ 16).
- `dotnet test api/Bryk.sln` — all green, xUnit count strictly higher than Step 0's recorded baseline
  (9 new: 7 `ThisWeekServiceTests` + 2 `ThisWeekControllerTests`).
- `cd ui; pnpm run build` — green.
- `cd ui; pnpm exec vitest run --no-file-parallelism` — all green, count strictly higher than Step 0's
  recorded baseline (11 new: 8 `weeklyTarget.spec.ts` + 3 `ThisWeekCard.spec.ts`).
- `git diff --stat` — the full expected set and nothing else:
  - `api/Bryk.Application/Training/ThisWeekResponse.cs`, `ThisWeekService.cs`
  - `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs`
  - `api/Bryk.API.Tests/Training/ThisWeekControllerTests.cs`
  - `ui/src/types/training.ts`
  - `ui/src/lib/weeklyTarget.ts` (new), `ui/src/lib/__tests__/weeklyTarget.spec.ts` (new)
  - `ui/src/components/dashboard/ThisWeekCard.vue`,
    `ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts`
  - No `Program.cs`, no controller, no migration, no `.csproj`/`package.json`, no
    `ComplianceClassifier.cs`, `WeeklyLoadCalculator.cs`, `LoadChart.vue`, `lib/charts/load.ts`, or
    `DeltaChip.vue`. If any of these appear, **STOP** — that is scope creep beyond `Tasks-18-5.md`.

**Manual smoke (the ROADMAP success criterion — do this, don't infer it).** Start the dev stack: API via
`dotnet run` from `api/Bryk.API` (`https://localhost:60129`), UI via `pnpm dev` from `ui/`, against the
dev seed:
1. Open the dashboard. Note the This Week card's target-vs-actual bar colour (or its absence, if the
   seeded athlete has no active plan/baseline) and, if present, the `DeltaChip`'s direction and label.
   Record both.
2. Log a workout (via the UI or `POST /api/v1/workouts`) dated in the current week, with a load large
   enough to push the week's actual across a band boundary (e.g. from `warn`/`bad` into `good`, or vice
   versa — use the pinned boundary table in Step 9 to pick a value).
3. Reload the dashboard. Confirm the bar's colour and the `DeltaChip`'s direction/label both changed
   consistently with the new actual load, and that the label row's `{actual} / {target} TSS` text
   updated. Confirm zero console errors.
4. Record the observed before/after values (bar colour, `DeltaChip` direction, actual/target numbers) in
   the phase handoff — this is the concrete evidence for the ROADMAP's "This Week shows target vs actual
   flipping state on log" criterion.

Commit with the message from `Tasks-18-5.md`:

```
feat: This Week target vs actual (ADR-0008 bands on the dashboard)

ThisWeekResponse gains TargetLoad (nullable) and ActualLoad, closing the
card's long-standing gap: it had no actual-load source at all, so logging a
workout changed nothing on the dashboard. ThisWeekService now sums the
week's completed EffectiveLoad and resolves the week's target through the
Phase 18 periodization service, selecting the plan whose window contains
today (ties to the latest StartDate). No plan, no baseline, or a plan that
does not cover the current ISO week all yield a null target - never a
target faked from the planned sum - and the card then renders exactly as it
did before.

The card grows a target-vs-actual bar plus a DeltaChip. Its state comes from
a pure buildTargetProgress helper that reuses ADR-0008 1's compliance bands
verbatim ([0.8, 1.2] good, [0.5, 0.8) and (1.2, inf) warn, below 0.5 bad,
zero target guarded to ratio 1), so a dashboard week and a calendar day are
graded by one rule. The bar is a labelled progressbar, not colour alone.

No migration, no new endpoint, no new package; DeltaChip and
ComplianceClassifier are reused unchanged. xUnit pins plan selection,
overlap, the missing-week and empty-target cases and the actual-load sum;
Vitest pins every band boundary and that a null target leaves the card's
markup untouched.
```
