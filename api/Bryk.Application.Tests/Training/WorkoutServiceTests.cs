using Bryk.Application.Common;
using Bryk.Application.Training.Load;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training;

public class WorkoutServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static WorkoutService NewService(StubWorkoutRepository workoutRepo, StubPlanRepository planRepo, StubUnitOfWork uow, decimal? computedLoad = 42m) =>
        new(new StubCurrentUserService(AthleteId),
            new LogWorkoutRequestValidator(),
            new UpdateWorkoutRequestValidator(),
            workoutRepo, planRepo,
            new StubLoadService { Computed = computedLoad },
            uow);

    private static UpdateWorkoutRequest ValidUpdate(Guid? plannedWorkoutId = null) => new()
    {
        Sport = Sport.Bike,
        CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PlannedWorkoutId = plannedWorkoutId,
        ActualDurationSeconds = 3000,
        AvgHr = 145
    };

    private static Workout OwnedWorkout() => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = AthleteId,
        Sport = Sport.Bike,
        CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    private static LogWorkoutRequest ValidRequest(Guid? plannedWorkoutId = null) => new()
    {
        Sport = Sport.Bike,
        CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PlannedWorkoutId = plannedWorkoutId,
        ActualDurationSeconds = 3600,
        AvgHr = 150
    };

    [Fact]
    public async Task LogAsync_Unplanned_StagesAndReturnsWithComputedLoad()
    {
        var repo = new StubWorkoutRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow);

        var result = await service.LogAsync(ValidRequest());

        repo.Added.Should().NotBeNull();
        repo.Added!.AthleteId.Should().Be(AthleteId);
        repo.Added.ComputedLoad.Should().Be(42m);
        result.ComputedLoad.Should().Be(42m);
        result.EffectiveLoad.Should().Be(42m);
        result.IsLoadOverride.Should().BeFalse();
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task LogAsync_ForeignPlannedWorkout_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var foreignPlanned = new PlannedWorkout { Id = planId, AthleteId = Guid.NewGuid(), Sport = Sport.Bike };
        var planRepo = new StubPlanRepository { ToReturn = foreignPlanned };
        var repo = new StubWorkoutRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, planRepo, uow);

        var act = () => service.LogAsync(ValidRequest(planId));

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Added.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task LogAsync_PartialStepActuals_LogsSuccessfully()
    {
        var repo = new StubWorkoutRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow);

        var request = ValidRequest();
        request.StepResults = new List<WorkoutStepResultDto>
        {
            new() { AvgPower = 200 },                            // only power captured
            new() { AvgHr = 160, ActualDurationSeconds = 300 }   // partial actuals
        };

        await service.LogAsync(request);

        repo.Added.Should().NotBeNull();
        repo.Added!.StepResults.Should().HaveCount(2);
        repo.Added.StepResults.Select(r => r.OrderIndex).Should().Equal(0, 1);
        repo.Added.StepResults.Should().OnlyContain(r => r.AthleteId == AthleteId && r.WorkoutId == repo.Added.Id);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task LogAsync_FutureCompletedDate_ThrowsValidation()
    {
        var repo = new StubWorkoutRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow);

        var request = ValidRequest();
        request.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var act = () => service.LogAsync(request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Added.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_ForeignWorkout_ThrowsKeyNotFound()
    {
        var foreign = new Workout { Id = Guid.NewGuid(), AthleteId = Guid.NewGuid(), Sport = Sport.Run };
        var repo = new StubWorkoutRepository { ToReturn = foreign };
        var service = NewService(repo, new StubPlanRepository(), new StubUnitOfWork());

        var act = () => service.GetAsync(foreign.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_RecomputesComputedLoad_NoOverride()
    {
        var existing = OwnedWorkout();
        var repo = new StubWorkoutRepository { Tracked = existing, ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow, computedLoad: 99m);

        var result = await service.UpdateAsync(existing.Id, ValidUpdate());

        existing.ComputedLoad.Should().Be(99m);
        result.ComputedLoad.Should().Be(99m);
        result.EffectiveLoad.Should().Be(99m);
        result.IsLoadOverride.Should().BeFalse();
        existing.ActualDurationSeconds.Should().Be(3000);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_LoadOverride_WinsOverComputed()
    {
        var existing = OwnedWorkout();
        var repo = new StubWorkoutRepository { Tracked = existing, ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow, computedLoad: 99m);

        var request = ValidUpdate();
        request.LoadOverride = 150m;

        var result = await service.UpdateAsync(existing.Id, request);

        existing.ComputedLoad.Should().Be(99m);   // still recomputed
        result.EffectiveLoad.Should().Be(150m);    // override wins
        result.IsLoadOverride.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ReplacesStepResults()
    {
        var existing = OwnedWorkout();
        existing.StepResults.Add(new WorkoutStepResult { Id = Guid.NewGuid(), AthleteId = AthleteId, WorkoutId = existing.Id, OrderIndex = 0 });
        var repo = new StubWorkoutRepository { Tracked = existing, ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow);

        var request = ValidUpdate();
        request.StepResults = new List<WorkoutStepResultDto> { new() { AvgPower = 210 }, new() { AvgPower = 220 } };

        await service.UpdateAsync(existing.Id, request);

        existing.StepResults.Should().HaveCount(2);
        existing.StepResults.Select(r => r.OrderIndex).Should().Equal(0, 1);
        existing.StepResults.Should().OnlyContain(r => r.AthleteId == AthleteId && r.WorkoutId == existing.Id);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_MissingOrForeign_ThrowsKeyNotFound()
    {
        var foreign = new Workout { Id = Guid.NewGuid(), AthleteId = Guid.NewGuid(), Sport = Sport.Run };
        var repo = new StubWorkoutRepository { Tracked = foreign };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow);

        var act = () => service.UpdateAsync(foreign.Id, ValidUpdate());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_ForeignPlannedWorkout_ThrowsKeyNotFound()
    {
        var existing = OwnedWorkout();
        var planId = Guid.NewGuid();
        var foreignPlanned = new PlannedWorkout { Id = planId, AthleteId = Guid.NewGuid(), Sport = Sport.Bike };
        var repo = new StubWorkoutRepository { Tracked = existing, ToReturn = existing };
        var planRepo = new StubPlanRepository { ToReturn = foreignPlanned };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, planRepo, uow);

        var act = () => service.UpdateAsync(existing.Id, ValidUpdate(planId));

        await act.Should().ThrowAsync<KeyNotFoundException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_OwnedWorkout_StagesDeleteAndSaves()
    {
        var existing = OwnedWorkout();
        var repo = new StubWorkoutRepository { Tracked = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow);

        await service.DeleteAsync(existing.Id);

        repo.Deleted.Should().BeSameAs(existing);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_MissingOrForeign_ThrowsKeyNotFound()
    {
        var foreign = new Workout { Id = Guid.NewGuid(), AthleteId = Guid.NewGuid(), Sport = Sport.Run };
        var repo = new StubWorkoutRepository { Tracked = foreign };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, new StubPlanRepository(), uow);

        var act = () => service.DeleteAsync(foreign.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Deleted.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ClampsTakeAndSkip()
    {
        var repo = new StubWorkoutRepository();
        var service = NewService(repo, new StubPlanRepository(), new StubUnitOfWork());

        await service.GetWorkoutsAsync(null, null, null, skip: -5, take: 500);
        repo.LastFilter!.Value.Take.Should().Be(100);
        repo.LastFilter.Value.Skip.Should().Be(0);

        await service.GetWorkoutsAsync(null, null, null, skip: null, take: 0);
        repo.LastFilter!.Value.Take.Should().Be(20);    // 0/absent -> default page size

        await service.GetWorkoutsAsync(null, null, null, skip: null, take: null);
        repo.LastFilter!.Value.Take.Should().Be(20);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ForwardsFiltersAndMaps()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var repo = new StubWorkoutRepository
        {
            Filtered = new List<Workout> { OwnedWorkout(), OwnedWorkout() }
        };
        var service = NewService(repo, new StubPlanRepository(), new StubUnitOfWork());

        var result = await service.GetWorkoutsAsync(from, to, Sport.Run, skip: 10, take: 25);

        repo.LastFilter!.Value.From.Should().Be(from);
        repo.LastFilter.Value.To.Should().Be(to);
        repo.LastFilter.Value.Sport.Should().Be(Sport.Run);
        repo.LastFilter.Value.Skip.Should().Be(10);
        repo.LastFilter.Value.Take.Should().Be(25);
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.TrainingPlanId == null);   // list reads stay single-table
    }

    private sealed class StubCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class StubLoadService : ILoadService
    {
        public decimal? Computed { get; init; }
        public Task<decimal?> ComputePlannedLoadAsync(PlannedWorkout workout, CancellationToken ct = default) => Task.FromResult(Computed);
        public Task<decimal?> ComputeActualLoadAsync(Workout workout, CancellationToken ct = default) => Task.FromResult(Computed);
    }

    private sealed class StubWorkoutRepository : IWorkoutRepository
    {
        public Workout? ToReturn { get; init; }
        public Workout? Tracked { get; init; }
        public Workout? Added { get; private set; }
        public Workout? Deleted { get; private set; }

        public Task AddAsync(Workout workout, CancellationToken ct = default)
        {
            Added = workout;
            return Task.CompletedTask;
        }

        public Task<Workout?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn ?? Added);

        public Task<Workout?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Tracked);

        public void Delete(Workout workout) => Deleted = workout;

        public IReadOnlyList<Workout> Filtered { get; init; } = new List<Workout>();
        public (DateOnly? From, DateOnly? To, Sport? Sport, int Skip, int Take)? LastFilter { get; private set; }

        public Task<IReadOnlyList<Workout>> GetByAthleteFilteredAsync(Guid athleteId, DateOnly? from, DateOnly? to, Sport? sport, int skip, int take, CancellationToken ct = default)
        {
            LastFilter = (from, to, sport, skip, take);
            return Task.FromResult(Filtered);
        }

        public Task<IReadOnlyList<Workout>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DateOnly?> GetFirstWorkoutDateAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Workout>> GetByAthleteWithStepResultsAsync(Guid athleteId, Sport? sport, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Workout workout) => throw new NotImplementedException();
    }

    private sealed class StubPlanRepository : ITrainingPlanRepository
    {
        public PlannedWorkout? ToReturn { get; init; }

        public Task<PlannedWorkout?> GetPlannedWorkoutWithStructureAsync(Guid plannedWorkoutId, CancellationToken ct = default) => Task.FromResult(ToReturn);

        public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(TrainingPlan entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(TrainingPlan entity) => throw new NotImplementedException();
        public void Delete(TrainingPlan entity) => throw new NotImplementedException();
        public Task AddPlannedWorkoutAsync(PlannedWorkout plannedWorkout, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdatePlannedWorkout(PlannedWorkout plannedWorkout) => throw new NotImplementedException();
        public void RemovePlannedWorkout(PlannedWorkout plannedWorkout) => throw new NotImplementedException();
        public Task AddWorkoutBlockAsync(WorkoutBlock block, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdateWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
        public void RemoveWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
        public Task AddWorkoutStepAsync(WorkoutStep step, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdateWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
        public void RemoveWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
    }
}
