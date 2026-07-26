# Impl 18-3 — Build order: `IPeriodizationService` + weekly-targets endpoint

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-18-3.md`.
**Decision lock:** ADR-0009 §1 (baseline chain: trailing-4-week mean actual load → the plan's own
first-week planned load → no targets at all) + §4 (compute-on-read, no `WeeklyTarget` table, no
migration).
**Scope:** Backend only. No migration, no new package.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to
the next. Commit once at the end with the message in `Tasks-18-3.md`.

## Step 0 — Pre-flight

- `git status` clean on the working branch. Baseline `dotnet build api/Bryk.sln` green.
- **Hard dependency gate — do not proceed unless both are true** (this task is sequenced after both and
  cannot be built in isolation):
  - Task 18-1 landed: `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs` and
    `WeeklyTargetDto.cs` exist, and `md/decisions/0009-periodization-ramp-model.md` exists with
    `**Status:** Accepted`.
  - Task 18-2 landed: `api/Bryk.API/Controllers/TrainingPlansController.cs` already has an
    `[HttpPut("{id:guid}")]` `UpdateAsync` action, and `api/Bryk.Application/Training/TrainingPlanUpdateRequest.cs`
    exists.
  - If either is missing, **STOP** — flag it and do not proceed.
- Re-read `md/Tasks-18-3.md` in full. Open in editor:
  `api/Bryk.Application/Analytics/AnalyticsService.cs` (lines 57–107 `GetWeeklyLoadAsync`, line 186 the
  `WeekStart` helper),
  `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs` (18-1 — input record field
  order, the "empty list when baseline is null/≤0" contract),
  `api/Bryk.Application/Training/TrainingPlanService.cs` (lines 155–164, `LoadOwnedPlanAsync`),
  `api/Bryk.Application/Calendar/CalendarService.cs` (read-only multi-repository service precedent),
  `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs`, `ITrainingPlanRepository.cs`, `IEventRepository.cs`,
  `api/Bryk.API/Program.cs` (lines 100–120, the manual `AddScoped` list),
  `api/Bryk.API/Controllers/TrainingPlansController.cs`,
  `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs` (the stub style to mirror),
  `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` (integration harness + foreign-athlete
  seeding block),
  `api/Bryk.Application/Training/Workouts/LogWorkoutRequestValidator.cs` (lines 11–13, `CompletedDate`
  cannot be in the future).
- Confirm current shapes (already verified during spec-writing):
  - `TrainingPlansController`'s ctor is `(ITrainingPlanService trainingPlanService, IStructuredWorkoutService structuredWorkoutService)`
    — Task 18-2 does not touch the controller's ctor (only `TrainingPlanService`'s ctor grew), so it
    still has exactly two parameters going into this task; Step 5 below makes it three.
  - `Program.cs:120` is
    `builder.Services.AddScoped<Bryk.Application.Calendar.ICalendarService, Bryk.Application.Calendar.CalendarService>();`
    — the new line goes immediately after it, in the same fully-qualified style.
  - `IWorkoutRepository.GetByAthleteInRangeAsync`, `ITrainingPlanRepository.GetPlannedWorkoutsInRangeWithStructureAsync`,
    `IEventRepository.GetByIdAsync`, `IAthleteRepository.GetWithSportProfilesAsync`,
    `IZoneService.GetZonesAsync` all exist today and are already registered — no repository change and
    no DI change beyond the one `IPeriodizationService` line.
  - `TrainingPlan` entity fields: `Id, AthleteId, Name, EventId, StartDate, EndDate, Methodology,
    BuildWeeks, RecoveryWeeks, RecoveryWeekPercentage` (`api/Bryk.Domain/Entities/TrainingPlan.cs`).
- Record the current `dotnet test api/Bryk.sln` pass count — this task's "post-18-2 baseline". Step 8
  must rise above it by exactly **18** (12 unit facts + 6 integration facts), zero failures.

## Step 1 — DTOs: `TargetBaselineSource`, `WeeklyTargetWeekDto`, `WeeklyTargetsResponse`

New files under `api/Bryk.Application/Training/Periodization/` (the folder already exists from 18-1).

**New file** `api/Bryk.Application/Training/Periodization/TargetBaselineSource.cs`:
```csharp
namespace Bryk.Application.Training.Periodization;

public enum TargetBaselineSource { None = 0, TrailingActual = 1, FirstWeekPlanned = 2 }
```

**New file** `api/Bryk.Application/Training/Periodization/WeeklyTargetWeekDto.cs`:
```csharp
namespace Bryk.Application.Training.Periodization;

// Per-week merge shape returned by IPeriodizationService.GetWeeklyTargetsAsync.
//
// Asymmetry (deliberate, not a bug): PlannedLoad is scoped to THIS plan's own planned workouts
// (filtered on TrainingPlanId); ActualLoad is athlete-wide for the week, because a completed
// Workout carries no plan attribution (ADR-0005 / ADR-0007 treat actual load athlete-wide). Do
// not invent an attribution rule to make the two symmetric.
public class WeeklyTargetWeekDto
{
    public DateOnly WeekStart { get; set; }
    public decimal TargetLoad { get; set; }
    public bool IsRecoveryWeek { get; set; }
    public bool IsTaperWeek { get; set; }
    public decimal PlannedLoad { get; set; }
    public decimal ActualLoad { get; set; }
}
```

**New file** `api/Bryk.Application/Training/Periodization/WeeklyTargetsResponse.cs`:
```csharp
namespace Bryk.Application.Training.Periodization;

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

`TargetBaselineSource` serializes as a string via the global `JsonStringEnumConverter`
(`Program.cs:27–28`) — no per-DTO attribute needed. `BaselineSource = None` ⇒ `Baseline` is null ⇒
`Weeks` is empty; this falls out of `WeeklyTargetCalculator.Compute` returning `[]` for a null/≤0
baseline (18-1), not from special-casing here — verified end-to-end in Step 6.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 2 — `IPeriodizationService`

**New file** `api/Bryk.Application/Training/Periodization/IPeriodizationService.cs`:
```csharp
namespace Bryk.Application.Training.Periodization;

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

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 3 — `PeriodizationService`

**New file** `api/Bryk.Application/Training/Periodization/PeriodizationService.cs`. Primary-ctor DI, the
six dependencies `Tasks-18-3.md` specifies, in that order. No `IUnitOfWork`, no staging, no
`SaveChangesAsync` — this service performs zero writes.

```csharp
using Bryk.Application.Common;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Interfaces;

namespace Bryk.Application.Training.Periodization;

public class PeriodizationService(
    ICurrentUserService currentUser,
    ITrainingPlanRepository planRepo,
    IWorkoutRepository workoutRepo,
    IEventRepository eventRepo,
    IAthleteRepository athleteRepo,
    IZoneService zoneService) : IPeriodizationService
{
    // Trailing baseline window (ADR-0009 §1): exactly 4 ISO weeks ending the day before the plan's
    // first week, fixed divisor — empty weeks are load-bearing zeros, never skipped.
    private const int TrailingWindowDays = 28;
    private const int TrailingWeekDivisor = 4;

    public async Task<WeeklyTargetsResponse> GetWeeklyTargetsAsync(Guid planId, CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var plan = await planRepo.GetByIdAsync(planId, ct);
        if (plan is null || plan.AthleteId != athleteId)
        {
            throw new KeyNotFoundException();
        }

        var firstWeekStart = WeekStart(plan.StartDate);
        var lastWeekEnd = WeekStart(plan.EndDate).AddDays(6);

        // Planned per week — THIS plan only. GetPlannedWorkoutsInRangeWithStructureAsync is athlete-wide
        // across all of the athlete's plans, so filter on TrainingPlanId or another plan's sessions leak
        // into this plan's weeks. Aggregation shape lifted verbatim from AnalyticsService.GetWeeklyLoadAsync.
        var planned = (await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, firstWeekStart, lastWeekEnd, ct))
            .Where(pw => pw.TrainingPlanId == plan.Id)
            .ToList();
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var zones = await zoneService.GetZonesAsync(ct);

        var plannedByWeek = new Dictionary<DateOnly, decimal>();
        foreach (var pw in planned)
        {
            var profile = athlete?.SportProfiles.FirstOrDefault(p => p.Sport == pw.Sport);
            var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == pw.Sport);
            var effective = pw.PlannedLoad ?? LoadCalculator.ComputePlannedLoad(pw, profile, sportZones) ?? 0m;
            var weekStart = WeekStart(pw.ScheduledDate);
            plannedByWeek[weekStart] = plannedByWeek.GetValueOrDefault(weekStart, 0m) + effective;
        }

        // Actual per week — athlete-wide by design (a completed Workout carries no plan attribution;
        // see the comment on WeeklyTargetWeekDto). Same aggregation shape as AnalyticsService.
        var actuals = await workoutRepo.GetByAthleteInRangeAsync(athleteId, firstWeekStart, lastWeekEnd, ct);
        var actualByWeek = new Dictionary<DateOnly, decimal>();
        foreach (var w in actuals)
        {
            var weekStart = WeekStart(w.CompletedDate);
            actualByWeek[weekStart] = actualByWeek.GetValueOrDefault(weekStart, 0m) + (w.LoadOverride ?? w.ComputedLoad ?? 0m);
        }

        // Baseline (ADR-0009 §1), anchored on the plan's FIRST WEEK — never on today. Anchoring on the
        // plan start keeps the target series stable for the plan's whole life; a today-anchored baseline
        // would silently reshape every target every Monday.
        var trailingStart = firstWeekStart.AddDays(-TrailingWindowDays);
        var trailingEnd = firstWeekStart.AddDays(-1);
        var trailingCompleted = await workoutRepo.GetByAthleteInRangeAsync(athleteId, trailingStart, trailingEnd, ct);
        var trailingMean = Math.Round(trailingCompleted.Sum(w => w.LoadOverride ?? w.ComputedLoad ?? 0m) / TrailingWeekDivisor, 2);

        decimal? baseline;
        TargetBaselineSource baselineSource;
        if (trailingMean > 0m)
        {
            baseline = trailingMean;
            baselineSource = TargetBaselineSource.TrailingActual;
        }
        else
        {
            var firstWeekPlanned = plannedByWeek.GetValueOrDefault(firstWeekStart, 0m);
            if (firstWeekPlanned > 0m)
            {
                baseline = firstWeekPlanned;
                baselineSource = TargetBaselineSource.FirstWeekPlanned;
            }
            else
            {
                baseline = null;
                baselineSource = TargetBaselineSource.None;
            }
        }

        // Linked event date — defensive ownership check (the FK is SetNull and 18-2 already validates
        // ownership on write; this guards a stale/foreign EventId some other path might produce).
        DateOnly? eventDate = null;
        if (plan.EventId is { } eventId)
        {
            var ev = await eventRepo.GetByIdAsync(eventId, ct);
            if (ev is not null && ev.AthleteId == athleteId)
            {
                eventDate = ev.EventDate;
            }
        }

        var targets = WeeklyTargetCalculator.Compute(new WeeklyTargetInput(
            plan.StartDate, plan.EndDate, baseline, plan.BuildWeeks, plan.RecoveryWeeks, plan.RecoveryWeekPercentage, eventDate));

        var weeks = targets.Select(t => new WeeklyTargetWeekDto
        {
            WeekStart = t.WeekStart,
            TargetLoad = t.TargetLoad,
            IsRecoveryWeek = t.IsRecoveryWeek,
            IsTaperWeek = t.IsTaperWeek,
            PlannedLoad = Math.Round(plannedByWeek.GetValueOrDefault(t.WeekStart, 0m), 2),
            ActualLoad = Math.Round(actualByWeek.GetValueOrDefault(t.WeekStart, 0m), 2)
        }).ToList();

        return new WeeklyTargetsResponse
        {
            PlanId = plan.Id,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            Baseline = baseline,
            BaselineSource = baselineSource,
            Weeks = weeks
        };
    }

    // Monday-anchored ISO week start — same expression as AnalyticsService.cs:186 / ThisWeekService.cs:44.
    // Duplicated deliberately (ADR-0009's stated convention); not refactored into a shared helper.
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
```

When `targets` is empty (null/≤0 baseline), `weeks` is empty too — no row synthesis, matching acceptance
criterion 8. No multiplier constants live here; every ramp/recovery/taper number stays inside
`WeeklyTargetCalculator` (18-1).

**Verify:** `dotnet build api/Bryk.sln` green. (Still unreferenced by DI/controller — that's Steps 4–5.)

## Step 4 — DI: one `Program.cs` line

**File:** `api/Bryk.API/Program.cs` — insert immediately after line 120 (`ICalendarService`
registration), in the same fully-qualified style as its neighbours:
```csharp
builder.Services.AddScoped<Bryk.Application.Training.Periodization.IPeriodizationService, Bryk.Application.Training.Periodization.PeriodizationService>();
```
This is the **only** `Program.cs` change in this task. Confirm `git diff api/Bryk.API/Program.cs` shows
exactly one added line before moving on.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 5 — Controller: ctor + `GetWeeklyTargetsAsync` action

**File:** `api/Bryk.API/Controllers/TrainingPlansController.cs`.

Add a `using` for the new namespace (not covered by the file's existing `using Bryk.Application.Training;`):
```csharp
using Bryk.Application.Training.Periodization;
```

Ctor gains a third parameter:
```csharp
public class TrainingPlansController(
    ITrainingPlanService trainingPlanService,
    IStructuredWorkoutService structuredWorkoutService,
    IPeriodizationService periodizationService) : ControllerBase
```

Add the action immediately after the `UpdateAsync` action Task 18-2 added (itself directly after
`GetByIdAsync`), and before `AddPlannedWorkoutAsync`:
```csharp
/// <summary>
/// Returns the plan's computed weekly load targets (ADR-0009: trailing-4-week baseline, +7 %/build-week
/// ramp, build:recovery cadence, two-week taper into a linked in-window event) merged with the plan's
/// planned load and the athlete's actual load per ISO week. Targets are computed on read — nothing is
/// stored. An athlete with no usable baseline gets an empty week list. 404 if the plan is missing or
/// foreign.
/// </summary>
[HttpGet("{id:guid}/weekly-targets")]
public async Task<IActionResult> GetWeeklyTargetsAsync(Guid id, CancellationToken cancellationToken)
{
    WeeklyTargetsResponse result = await periodizationService.GetWeeklyTargetsAsync(id, cancellationToken);
    return Ok(result);
}
```
No `[FromQuery]` parameters (the plan window *is* the range — ADR-0008 §2), no try/catch (the global
middleware maps `KeyNotFoundException` → 404). Athlete id never comes from the route.

**Verify:** `dotnet build api/Bryk.sln` green. Then `dotnet test api/Bryk.sln` — full suite green. This
is the first point the DI container is exercised with the new three-parameter ctor (every
`BrykWebApplicationFactory`-based integration test builds the container at startup), so a registration
mismatch surfaces here immediately, before any new tests exist to explain it.

## Step 6 — Unit tests: `PeriodizationServiceTests.cs`

**New file** `api/Bryk.Application.Tests/Training/Periodization/PeriodizationServiceTests.cs`. Stub
style mirrors `ThisWeekServiceTests`: `private sealed class` stubs, unused members
`throw new NotImplementedException()`, a `NewService(...)` factory. All dates are fixed
(`FirstWeekStart = 2026-01-05`, a Monday) so nothing depends on the clock.

```csharp
using Bryk.Application.Common;
using Bryk.Application.Training.Periodization;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training.Periodization;

public class PeriodizationServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly FirstWeekStart = new(2026, 1, 5); // Monday

    private static TrainingPlan Plan(DateOnly start, DateOnly end, Guid? athleteId = null, Guid? id = null,
        Guid? eventId = null, int? buildWeeks = null, int? recoveryWeeks = null, decimal? recoveryPct = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        AthleteId = athleteId ?? AthleteId,
        Name = "Test Plan",
        Methodology = MethodologyChoice.Polarized,
        StartDate = start,
        EndDate = end,
        EventId = eventId,
        BuildWeeks = buildWeeks,
        RecoveryWeeks = recoveryWeeks,
        RecoveryWeekPercentage = recoveryPct
    };

    private static Workout Completion(DateOnly date, decimal loadOverride) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = AthleteId,
        Sport = Sport.Run,
        CompletedDate = date,
        LoadOverride = loadOverride
    };

    private static PlannedWorkout Planned(DateOnly date, decimal? plannedLoad, Guid trainingPlanId) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = AthleteId,
        TrainingPlanId = trainingPlanId,
        Sport = Sport.Run,
        ScheduledDate = date,
        Title = "Session",
        PlannedLoad = plannedLoad
    };

    private static Event LinkedEvent(Guid id, DateOnly eventDate, Guid? athleteId = null) => new()
    {
        Id = id,
        AthleteId = athleteId ?? AthleteId,
        Name = "Race",
        EventDate = eventDate,
        Sport = Sport.Run,
        Priority = EventPriority.A
    };

    private static PeriodizationService NewService(TrainingPlan? plan, IEnumerable<PlannedWorkout>? planned = null,
        IEnumerable<Workout>? completions = null, Event? linkedEvent = null) =>
        new(new StubCurrentUserService(AthleteId),
            new StubTrainingPlanRepository(plan, planned ?? Array.Empty<PlannedWorkout>()),
            new StubWorkoutRepository(completions ?? Array.Empty<Workout>()),
            new StubEventRepository(linkedEvent),
            new StubAthleteRepository(),
            new StubZoneService());

    [Fact]
    public async Task GetWeeklyTargetsAsync_ForeignPlan_ThrowsKeyNotFound()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(27), athleteId: Guid.NewGuid());
        var service = NewService(plan);

        var act = () => service.GetWeeklyTargetsAsync(plan.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_MissingPlan_ThrowsKeyNotFound()
    {
        var service = NewService(plan: null);

        var act = () => service.GetWeeklyTargetsAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_TrailingFourWeeksOfActuals_UsesTrailingActualBaseline()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(27));
        var completions = new[]
        {
            Completion(FirstWeekStart.AddDays(-28), 200m),
            Completion(FirstWeekStart.AddDays(-21), 200m),
            Completion(FirstWeekStart.AddDays(-14), 200m),
            Completion(FirstWeekStart.AddDays(-1), 200m)
        };
        var service = NewService(plan, completions: completions);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Baseline.Should().Be(200.00m);
        result.BaselineSource.Should().Be(TargetBaselineSource.TrailingActual);
        result.Weeks[0].TargetLoad.Should().Be(200.00m);
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_TrailingWindowExcludesTheWeekBeforeItAndThePlanItself()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(27));
        var completions = new[]
        {
            Completion(FirstWeekStart.AddDays(-29), 500m), // one day before the trailing window opens
            Completion(FirstWeekStart, 500m)                // the plan's own first day, not "trailing"
        };
        var service = NewService(plan, completions: completions);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.BaselineSource.Should().Be(TargetBaselineSource.None);
        result.Baseline.Should().BeNull();
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_PartialHistory_DividesByFourNotByWeeksPresent()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(27));
        var completions = new[] { Completion(FirstWeekStart.AddDays(-10), 200m) };
        var service = NewService(plan, completions: completions);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Baseline.Should().Be(50.00m);
        result.BaselineSource.Should().Be(TargetBaselineSource.TrailingActual);
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_NoHistory_FallsBackToFirstWeekPlannedLoad()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(27));
        var planned = new[]
        {
            Planned(FirstWeekStart, 60m, plan.Id),
            Planned(FirstWeekStart.AddDays(1), 40m, plan.Id)
        };
        var service = NewService(plan, planned: planned);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Baseline.Should().Be(100.00m);
        result.BaselineSource.Should().Be(TargetBaselineSource.FirstWeekPlanned);
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_NoHistoryAndNoPlannedWork_ReturnsNoTargets()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(27));
        var service = NewService(plan);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Baseline.Should().BeNull();
        result.BaselineSource.Should().Be(TargetBaselineSource.None);
        result.Weeks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_MergesPlannedAndActualPerWeek()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(13)); // 2 ISO weeks
        var completions = new List<Workout>
        {
            Completion(FirstWeekStart.AddDays(-28), 200m),
            Completion(FirstWeekStart.AddDays(-21), 200m),
            Completion(FirstWeekStart.AddDays(-14), 200m),
            Completion(FirstWeekStart.AddDays(-1), 200m),
            Completion(FirstWeekStart.AddDays(2), 90m) // inside week 1
        };
        var planned = new[] { Planned(FirstWeekStart, 120m, plan.Id) };
        var service = NewService(plan, planned: planned, completions: completions);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Weeks.Should().HaveCount(2);
        result.Weeks[0].PlannedLoad.Should().Be(120.00m);
        result.Weeks[0].ActualLoad.Should().Be(90.00m);
        result.Weeks[0].TargetLoad.Should().Be(200.00m);
        result.Weeks[1].PlannedLoad.Should().Be(0.00m);
        result.Weeks[1].ActualLoad.Should().Be(0.00m);
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_IgnoresPlannedWorkoutsFromAnotherPlan()
    {
        var plan = Plan(FirstWeekStart, FirstWeekStart.AddDays(27));
        var otherPlanId = Guid.NewGuid();
        var planned = new[] { Planned(FirstWeekStart, 999m, otherPlanId) };
        var completions = new[]
        {
            Completion(FirstWeekStart.AddDays(-28), 200m),
            Completion(FirstWeekStart.AddDays(-21), 200m),
            Completion(FirstWeekStart.AddDays(-14), 200m),
            Completion(FirstWeekStart.AddDays(-1), 200m)
        };
        var service = NewService(plan, planned: planned, completions: completions);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Weeks[0].PlannedLoad.Should().Be(0.00m);
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_LinkedInWindowEvent_ProducesTaperWeeks()
    {
        var eventId = Guid.NewGuid();
        var plan = Plan(new DateOnly(2026, 1, 5), new DateOnly(2026, 3, 29), eventId: eventId);
        var linkedEvent = LinkedEvent(eventId, new DateOnly(2026, 3, 28));
        var completions = new[]
        {
            Completion(new DateOnly(2026, 1, 5).AddDays(-28), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-21), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-14), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-1), 200m)
        };
        var service = NewService(plan, completions: completions, linkedEvent: linkedEvent);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Weeks[^1].IsTaperWeek.Should().BeTrue();
        result.Weeks[^2].IsTaperWeek.Should().BeTrue();
        result.Weeks.Take(result.Weeks.Count - 2).Should().OnlyContain(w => !w.IsTaperWeek);
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_EventOwnedByAnotherAthlete_IsIgnored()
    {
        var eventId = Guid.NewGuid();
        var plan = Plan(new DateOnly(2026, 1, 5), new DateOnly(2026, 3, 29), eventId: eventId);
        var foreignEvent = LinkedEvent(eventId, new DateOnly(2026, 3, 28), athleteId: Guid.NewGuid());
        var completions = new[]
        {
            Completion(new DateOnly(2026, 1, 5).AddDays(-28), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-21), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-14), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-1), 200m)
        };
        var service = NewService(plan, completions: completions, linkedEvent: foreignEvent);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Weeks.Should().OnlyContain(w => !w.IsTaperWeek);
    }

    [Fact]
    public async Task GetWeeklyTargetsAsync_ThreeBuildOneRecoverySixtyPercent_MatchesTheAdrVector()
    {
        var eventId = Guid.NewGuid();
        var plan = Plan(new DateOnly(2026, 1, 5), new DateOnly(2026, 3, 29),
            eventId: eventId, buildWeeks: 3, recoveryWeeks: 1, recoveryPct: 60.0m);
        var linkedEvent = LinkedEvent(eventId, new DateOnly(2026, 3, 28));
        var completions = new[]
        {
            Completion(new DateOnly(2026, 1, 5).AddDays(-28), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-21), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-14), 200m),
            Completion(new DateOnly(2026, 1, 5).AddDays(-1), 200m)
        };
        var service = NewService(plan, completions: completions, linkedEvent: linkedEvent);

        var result = await service.GetWeeklyTargetsAsync(plan.Id);

        result.Baseline.Should().Be(200.00m);
        result.Weeks.Select(w => w.TargetLoad).Should().Equal(
            200.00m, 214.00m, 228.98m, 137.39m, 245.01m, 262.16m, 280.51m, 168.31m,
            300.15m, 321.16m, 257.73m, 171.82m);
    }

    private sealed class StubCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }

    // Range/athlete-filters like the real repo (the plan-scoping is the service's own job — see
    // GetWeeklyTargetsAsync_IgnoresPlannedWorkoutsFromAnotherPlan, which pins that filter).
    private sealed class StubTrainingPlanRepository(TrainingPlan? plan, IEnumerable<PlannedWorkout> planned) : ITrainingPlanRepository
    {
        private readonly List<PlannedWorkout> _planned = planned.ToList();

        public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(plan is not null && plan.Id == id ? plan : null);

        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PlannedWorkout>>(
                _planned.Where(w => w.AthleteId == athleteId && w.ScheduledDate >= start && w.ScheduledDate <= end).ToList());

        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
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

    private sealed class StubEventRepository(Event? toReturn) : IEventRepository
    {
        public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(toReturn);

        public Task<IReadOnlyList<Event>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Event>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Event entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Event entity) => throw new NotImplementedException();
        public void Delete(Event entity) => throw new NotImplementedException();
    }

    private sealed class StubAthleteRepository : IAthleteRepository
    {
        public Task<Athlete?> GetWithSportProfilesAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Athlete?>(null);

        public Task<Athlete?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Athlete?> GetFullProfileAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Athlete>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Athlete athlete, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Athlete athlete) => throw new NotImplementedException();
        public void Delete(Athlete athlete) => throw new NotImplementedException();
        public Task<AthleteSportProfile?> GetSportProfileAsync(Guid athleteId, Sport sport, CancellationToken ct = default) => throw new NotImplementedException();
        public void AddSportProfile(AthleteSportProfile profile) => throw new NotImplementedException();
        public void UpdateSportProfile(AthleteSportProfile profile) => throw new NotImplementedException();
    }

    private sealed class StubZoneService : IZoneService
    {
        public Task<ZonesResponse> GetZonesAsync(CancellationToken ct = default) => Task.FromResult(new ZonesResponse());
        public Task<SportZonesResponse> SetOverridesAsync(Sport sport, ZoneOverrideRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ResetOverridesAsync(Sport sport, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
```

**Verify:** `dotnet test api/Bryk.sln --filter FullyQualifiedName~Periodization` — all 12 new facts pass;
in particular `GetWeeklyTargetsAsync_ThreeBuildOneRecoverySixtyPercent_MatchesTheAdrVector` must match
Task 18-1's pinned 12-week series value for value.

## Step 7 — Integration tests: `WeeklyTargetsControllerTests.cs`

**New file** `api/Bryk.API.Tests/Training/WeeklyTargetsControllerTests.cs` — same folder as
`TrainingPlansControllerTests.cs`, split into a new file rather than extending the sibling (which 18-2
already grew with `PUT` coverage; this task's cases are a distinct concern, mirroring how
`EventsControllerGetTests.cs`/`GoalsControllerGetTests.cs` were split out in 17-1). Same harness pattern
(`BrykWebApplicationFactory`, `JsonOptions` with `JsonStringEnumConverter`, seed through the public API,
the foreign-athlete `ApplicationDbContext` seeding block reused from `TrainingPlansControllerTests.cs`).

Every plan in this file is seeded with `StartDate = today`: `LogWorkoutRequestValidator` (`:11–13`)
forbids a future `CompletedDate`, and `WeekStart(today) ≤ today`, so the trailing
`[firstWeekStart−28, firstWeekStart−1]` window always ends strictly before today. Plans that need an
exact week count use `firstWeekStart.AddDays(27)` as `EndDate` (not `today.AddDays(27)`) so the count is
always exactly 4 ISO weeks regardless of which weekday "today" happens to be when the suite runs.

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Training;
using Bryk.Application.Training.Periodization;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Training;

public class WeeklyTargetsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // Same Monday-anchor expression as AnalyticsService.cs:186 / PeriodizationService — duplicated
    // locally per the codebase's established convention for this expression.
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static TrainingPlanRequest ValidPlan(string name, DateOnly start, DateOnly end) => new()
    {
        Name = name,
        Methodology = MethodologyChoice.Polarized,
        StartDate = start,
        EndDate = end
    };

    private static LogWorkoutRequest Completion(DateOnly completedDate, decimal loadOverride) => new()
    {
        Sport = Sport.Run,
        CompletedDate = completedDate,
        LoadOverride = loadOverride
    };

    [Fact]
    public async Task WeeklyTargets_MissingPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/trainingplans/{Guid.NewGuid()}/weekly-targets");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WeeklyTargets_ForeignPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();

        var foreignAthleteId = Guid.NewGuid();
        var foreignPlanId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = foreignPlanId,
                AthleteId = foreignAthleteId,
                Name = "Foreign Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = Today,
                EndDate = Today.AddDays(27)
            });
            db.Athletes.Add(new Athlete
            {
                Id = foreignAthleteId,
                Name = "Foreign Athlete",
                Gender = Gender.Male,
                DateOfBirth = new DateOnly(1990, 1, 1),
                HeightCm = 180,
                WeightKg = 75,
                TypicalWeeklyHours = 10,
                Methodology = MethodologyChoice.Polarized
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/trainingplans/{foreignPlanId}/weekly-targets");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WeeklyTargets_FreshAthlete_Returns200WithNoTargets()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Fresh Plan", Today, Today.AddDays(27)));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/trainingplans/{created!.Id}/weekly-targets");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.BaselineSource.Should().Be(TargetBaselineSource.None);
        body.Baseline.Should().BeNull();
        body.Weeks.Should().BeEmpty();
    }

    [Fact]
    public async Task WeeklyTargets_WithTrailingActuals_ReturnsRampingTargets()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var firstWeekStart = WeekStart(Today);
        var planEnd = firstWeekStart.AddDays(27); // exactly 4 ISO weeks, whatever weekday "today" is
        foreach (var offsetDays in new[] { -28, -21, -14, -1 })
        {
            await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart.AddDays(offsetDays), 200m));
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Ramp Plan", Today, planEnd));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/trainingplans/{created!.Id}/weekly-targets");
        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);

        body!.BaselineSource.Should().Be(TargetBaselineSource.TrailingActual);
        body.Baseline.Should().Be(200.00m);
        body.Weeks.Should().HaveCount(4);
        body.Weeks.Select(w => w.TargetLoad).Should().BeInAscendingOrder();
        body.Weeks[0].TargetLoad.Should().Be(body.Baseline);
    }

    [Fact]
    public async Task WeeklyTargets_MergesTheAthletesActualLoad()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var firstWeekStart = WeekStart(Today);
        var planEnd = firstWeekStart.AddDays(27);
        foreach (var offsetDays in new[] { -28, -21, -14, -1 })
        {
            await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart.AddDays(offsetDays), 200m));
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Merge Plan", Today, planEnd));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart, 75m));

        var response = await client.GetAsync($"/api/v1/trainingplans/{created!.Id}/weekly-targets");
        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);

        body!.Weeks[0].ActualLoad.Should().Be(75.00m);
    }

    [Fact]
    public async Task WeeklyTargets_AfterPlanPutSetsCadence_TheDipAppears()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var firstWeekStart = WeekStart(Today);
        var planEnd = firstWeekStart.AddDays(27);
        foreach (var offsetDays in new[] { -28, -21, -14, -1 })
        {
            await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart.AddDays(offsetDays), 200m));
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Cadence Plan", Today, planEnd));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var putBody = new TrainingPlanUpdateRequest
        {
            Name = created!.Name,
            Methodology = created.Methodology,
            StartDate = created.StartDate,
            EndDate = created.EndDate,
            BuildWeeks = 3,
            RecoveryWeeks = 1,
            RecoveryWeekPercentage = 60.0m
        };
        var putResponse = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", putBody);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/v1/trainingplans/{created.Id}/weekly-targets");
        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);

        body!.Weeks.Should().HaveCount(4);
        body.Weeks[3].IsRecoveryWeek.Should().BeTrue();
        body.Weeks[3].TargetLoad.Should().BeLessThan(body.Weeks[2].TargetLoad);
    }
}
```

**Verify:** `dotnet test api/Bryk.sln --filter FullyQualifiedName~WeeklyTargetsControllerTests` — all 6
new facts pass, including `WeeklyTargets_AfterPlanPutSetsCadence_TheDipAppears`, which depends on 18-2's
`PUT` endpoint already being live.

## Step 8 — Final verification, live smoke test, diff sanity, commit

- `dotnet build api/Bryk.sln` — 0 errors; warning count unchanged from the known 16 (design-time
  `System.Security.Cryptography.Xml` NU1903 + the two pre-existing `WorkoutsControllerTests.cs` nullable
  warnings — do not touch either).
- `dotnet test api/Bryk.sln` — full suite green. Pass count is the Step 0 baseline **+ 18** (12
  `PeriodizationServiceTests` + 6 `WeeklyTargetsControllerTests`), zero failures.
- `dotnet test api/Bryk.sln --filter FullyQualifiedName~Periodization` — isolates and passes the new
  unit tests (the exact runtime-check command the acceptance contract specifies).
- `cd ui; pnpm run build` — green (no UI file touched this task, but this is a full-repo gate).
- `cd ui; pnpm exec vitest run --no-file-parallelism` — **229 tests / 53 files**, unchanged.
- **Live HTTP smoke against the dev API** (this task's behavior depends on real SQL Server aggregation
  paths that the InMemory-backed integration tests don't exercise — run this before calling the task
  done):
  ```powershell
  # Terminal 1 — leave running (Development environment; serves https://localhost:60129):
  dotnet run --project api/Bryk.API

  # Terminal 2 (PowerShell 7+; use `curl.exe -k` in place of -SkipCertificateCheck on 5.1):
  $base = "https://localhost:60129/api/v1"

  # (a) fresh/empty-weeks: a brand-new plan whose whole trailing window sits far in the future —
  # guaranteed zero trailing actual load AND zero first-week planned load, regardless of what the
  # current db/dev-seed.sql run seeded. Expect baselineSource "None", baseline null, weeks: [].
  $farStart = (Get-Date).AddDays(180).ToString("yyyy-MM-dd")
  $farEnd   = (Get-Date).AddDays(207).ToString("yyyy-MM-dd")
  $fresh = Invoke-RestMethod -SkipCertificateCheck -Method Post -Uri "$base/trainingplans" `
      -ContentType "application/json" `
      -Body (@{ name = "Smoke Fresh"; methodology = "Polarized"; startDate = $farStart; endDate = $farEnd } | ConvertTo-Json)
  Invoke-RestMethod -SkipCertificateCheck -Uri "$base/trainingplans/$($fresh.id)/weekly-targets"

  # (b) seeded/ramping: db/dev-seed.sql's own plan ("Indian Wells 70.3 Build", StartDate = WeekStart-14,
  # BuildWeeks=3/RecoveryWeeks=1/RecoveryWeekPercentage=70). Its own trailing 4 weeks predate the plan
  # (empty), so the ADR-0009 §1 chain is expected to fall through to FirstWeekPlanned (the seed's own
  # week -2 Bike-threshold + Long-run planned load) — a real exercise of the fallback, not TrailingActual.
  # Expect 200, Baseline non-null, weeks non-empty, and at least one isRecoveryWeek == true.
  $plans = Invoke-RestMethod -SkipCertificateCheck -Uri "$base/trainingplans"
  $seeded = $plans | Where-Object { $_.name -eq "Indian Wells 70.3 Build" }
  Invoke-RestMethod -SkipCertificateCheck -Uri "$base/trainingplans/$($seeded.id)/weekly-targets"
  ```
  Stop the dev server (Ctrl+C in Terminal 1) once both responses are inspected. If the dev database has
  never been seeded, run `db/dev-seed.sql` first (paste `DevAuth:CurrentAthleteId` from
  `dotnet user-secrets list --project api/Bryk.API`) so case (b) has a plan to query. The specific
  `baselineSource` in (b) is informational (unit tests already pin the exact chain value-for-value) — the
  load-bearing checks are 200, a non-null `baseline`, and a non-empty, non-flat `weeks` series.
- `git diff --stat` — confirm only the expected files changed/added:
  - `api/Bryk.Application/Training/Periodization/TargetBaselineSource.cs` (new)
  - `api/Bryk.Application/Training/Periodization/WeeklyTargetWeekDto.cs` (new)
  - `api/Bryk.Application/Training/Periodization/WeeklyTargetsResponse.cs` (new)
  - `api/Bryk.Application/Training/Periodization/IPeriodizationService.cs` (new)
  - `api/Bryk.Application/Training/Periodization/PeriodizationService.cs` (new)
  - `api/Bryk.API/Program.cs` (exactly one added line)
  - `api/Bryk.API/Controllers/TrainingPlansController.cs` (one `using`, ctor +1 param, one new action)
  - `api/Bryk.Application.Tests/Training/Periodization/PeriodizationServiceTests.cs` (new)
  - `api/Bryk.API.Tests/Training/WeeklyTargetsControllerTests.cs` (new)
  - No changes to `AnalyticsService.cs`, `WeeklyLoadCalculator.cs`, `ComplianceClassifier.cs`,
    `WeeklyTargetCalculator.cs` (18-1's file), `LoadChart.vue`, `lib/charts/load.ts`,
    `TrainingPlanRequest`/`TrainingPlanRequestValidator`, `TrainingPlanUpdateRequest`/its validator, any
    `ITrainingPlanRepository`/`IWorkoutRepository`/`IEventRepository`/`IAthleteRepository` implementation,
    any migration, or any `ui/` file. If the diff shows any of these — **STOP**, that is scope creep
    beyond `Tasks-18-3.md`.
- Commit with the message from `Tasks-18-3.md`:

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
