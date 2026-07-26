using Bryk.Application.Common;
using Bryk.Application.Training;
using Bryk.Application.Training.Periodization;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training;

public class ThisWeekServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static PlannedWorkout Workout(DateOnly date, string title, Sport sport = Sport.Run, Guid? athleteId = null) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = athleteId ?? AthleteId,
        TrainingPlanId = Guid.NewGuid(),
        Sport = sport,
        ScheduledDate = date,
        Title = title
    };

    private static TrainingPlan Plan(DateOnly start, DateOnly end, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        AthleteId = AthleteId,
        Name = "Plan",
        StartDate = start,
        EndDate = end
    };

    private static Workout Completion(DateOnly date, decimal? loadOverride, decimal? computedLoad = null) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = AthleteId,
        Sport = Sport.Run,
        CompletedDate = date,
        LoadOverride = loadOverride,
        ComputedLoad = computedLoad
    };

    private static ThisWeekService NewService(params PlannedWorkout[] workouts) =>
        NewServiceWith(workouts);

    private static ThisWeekService NewServiceWith(
        IEnumerable<PlannedWorkout>? workouts = null,
        IEnumerable<TrainingPlan>? plans = null,
        IEnumerable<Workout>? completions = null,
        StubPeriodizationService? periodization = null) =>
        new(new StubCurrentUserService(AthleteId),
            new StubTrainingPlanRepository(workouts ?? Array.Empty<PlannedWorkout>(), plans ?? Array.Empty<TrainingPlan>()),
            new StubAthleteRepository(),
            new StubZoneService(),
            new StubWorkoutRepository(completions ?? Array.Empty<Workout>()),
            periodization ?? new StubPeriodizationService());

    [Fact]
    public async Task GetThisWeekAsync_ReturnsWorkoutScheduledThisWeek_WithMondaySundayRange()
    {
        var service = NewService(Workout(Today, "Today's session"));

        var result = await service.GetThisWeekAsync();

        result.WeekStart.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.WeekEnd.Should().Be(result.WeekStart.AddDays(6));
        (result.WeekStart <= Today && Today <= result.WeekEnd).Should().BeTrue();
        result.PlannedWorkouts.Should().ContainSingle(pw => pw.Title == "Today's session");
    }

    [Fact]
    public async Task GetThisWeekAsync_ExcludesWorkoutsInAdjacentWeeks()
    {
        // -10 days is always before this week's Monday (Monday >= today-6);
        // +10 days is always after this week's Sunday (Sunday <= today+6) — deterministic across boundaries.
        var service = NewService(
            Workout(Today.AddDays(-10), "Last week"),
            Workout(Today, "This week"),
            Workout(Today.AddDays(10), "Next week"));

        var result = await service.GetThisWeekAsync();

        result.PlannedWorkouts.Should().ContainSingle();
        result.PlannedWorkouts[0].Title.Should().Be("This week");
    }

    [Fact]
    public async Task GetThisWeekAsync_NoPlannedWorkouts_ReturnsEmptyListWithWeekRange()
    {
        var service = NewService();

        var result = await service.GetThisWeekAsync();

        result.PlannedWorkouts.Should().BeEmpty();
        result.WeekStart.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.WeekEnd.Should().Be(result.WeekStart.AddDays(6));
    }

    [Fact]
    public async Task GetThisWeekAsync_WeeklyLoad_SumsEffectiveLoad()
    {
        var a = Workout(Today, "A");
        a.PlannedLoad = 50m;
        var b = Workout(Today, "B");
        b.PlannedLoad = 30m;
        var service = NewService(a, b);

        var result = await service.GetThisWeekAsync();

        result.WeeklyLoad.Should().Be(80m);
        result.PlannedWorkouts.Should().OnlyContain(pw => pw.IsLoadOverride && pw.EffectiveLoad == pw.PlannedLoad);
    }

    // ── Phase 18: target vs actual ─────────────

    private static WeeklyTargetWeekDto TargetWeek(DateOnly weekStart, decimal target) =>
        new() { WeekStart = weekStart, TargetLoad = target };

    private static DateOnly ThisMonday() => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    [Fact]
    public async Task GetThisWeekAsync_NoPlanCoversToday_TargetLoadIsNull()
    {
        var periodization = new StubPeriodizationService();
        var service = NewServiceWith(
            plans: new[]
            {
                Plan(Today.AddDays(-40), Today.AddDays(-1)),  // ended yesterday
                Plan(Today.AddDays(1), Today.AddDays(40))     // starts tomorrow
            },
            periodization: periodization);

        var result = await service.GetThisWeekAsync();

        result.TargetLoad.Should().BeNull();
        periodization.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetThisWeekAsync_PlanCoveringToday_ReturnsThisWeeksTarget()
    {
        var periodization = new StubPeriodizationService
        {
            ToReturn = new List<WeeklyTargetWeekDto>
            {
                TargetWeek(ThisMonday().AddDays(-7), 300.00m),
                TargetWeek(ThisMonday(), 320.00m)
            }
        };
        var service = NewServiceWith(
            plans: new[] { Plan(Today.AddDays(-20), Today.AddDays(20)) },
            periodization: periodization);

        var result = await service.GetThisWeekAsync();

        result.TargetLoad.Should().Be(320.00m);
    }

    [Fact]
    public async Task GetThisWeekAsync_OverlappingPlans_PicksTheLatestStartDate()
    {
        var older = Plan(Today.AddDays(-30), Today.AddDays(30));
        var newer = Plan(Today.AddDays(-5), Today.AddDays(30));
        var periodization = new StubPeriodizationService();
        var service = NewServiceWith(plans: new[] { older, newer }, periodization: periodization);

        await service.GetThisWeekAsync();

        periodization.CalledWithPlanId.Should().Be(newer.Id);
    }

    [Fact]
    public async Task GetThisWeekAsync_PlanWithNoTargets_TargetLoadIsNull()
    {
        var service = NewServiceWith(
            plans: new[] { Plan(Today.AddDays(-20), Today.AddDays(20)) },
            periodization: new StubPeriodizationService()); // Weeks = []

        var result = await service.GetThisWeekAsync();

        result.TargetLoad.Should().BeNull();
    }

    [Fact]
    public async Task GetThisWeekAsync_TargetsMissingTheCurrentWeek_TargetLoadIsNull()
    {
        var periodization = new StubPeriodizationService
        {
            ToReturn = new List<WeeklyTargetWeekDto> { TargetWeek(ThisMonday().AddDays(-14), 250.00m) }
        };
        var service = NewServiceWith(
            plans: new[] { Plan(Today.AddDays(-20), Today.AddDays(20)) },
            periodization: periodization);

        var result = await service.GetThisWeekAsync();

        result.TargetLoad.Should().BeNull();
    }

    [Fact]
    public async Task GetThisWeekAsync_ActualLoad_SumsEffectiveLoadOfTheWeeksCompletions()
    {
        var service = NewServiceWith(completions: new[]
        {
            Completion(ThisMonday(), 40m),
            Completion(ThisMonday(), null, 25m),
            Completion(Today.AddDays(-10), 500m) // outside the week
        });

        var result = await service.GetThisWeekAsync();

        result.ActualLoad.Should().Be(65.00m);
    }

    [Fact]
    public async Task GetThisWeekAsync_NoCompletions_ActualLoadIsZero()
    {
        var service = NewServiceWith();

        var result = await service.GetThisWeekAsync();

        result.ActualLoad.Should().Be(0m);
    }

    private sealed class StubCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }

    // Applies the same athlete + range filter the real SQL query does, so the service's
    // week-window math is what's under test.
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

        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(_plans.Where(p => p.AthleteId == athleteId).ToList());

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
        public IReadOnlyList<WeeklyTargetWeekDto> ToReturn { get; init; } = new List<WeeklyTargetWeekDto>();
        public Guid? CalledWithPlanId { get; private set; }
        public int CallCount { get; private set; }

        public Task<WeeklyTargetsResponse> GetWeeklyTargetsAsync(Guid planId, CancellationToken ct = default)
        {
            CalledWithPlanId = planId;
            CallCount++;
            return Task.FromResult(new WeeklyTargetsResponse { PlanId = planId, Weeks = ToReturn });
        }
    }
}
