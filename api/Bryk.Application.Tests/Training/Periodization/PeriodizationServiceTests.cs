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
