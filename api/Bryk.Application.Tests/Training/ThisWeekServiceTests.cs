using Bryk.Application.Common;
using Bryk.Application.Training;
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

    private static ThisWeekService NewService(params PlannedWorkout[] workouts) =>
        new(new StubCurrentUserService(AthleteId),
            new StubTrainingPlanRepository(workouts),
            new StubAthleteRepository(),
            new StubZoneService());

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

    private sealed class StubCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }

    // Applies the same athlete + range filter the real SQL query does, so the service's
    // week-window math is what's under test.
    private sealed class StubTrainingPlanRepository(IEnumerable<PlannedWorkout> workouts) : ITrainingPlanRepository
    {
        private readonly List<PlannedWorkout> _workouts = workouts.ToList();

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
