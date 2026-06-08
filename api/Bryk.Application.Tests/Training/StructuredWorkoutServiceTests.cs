using Bryk.Application.Common;
using Bryk.Application.Training;
using Bryk.Application.Training.Load;
using Bryk.Application.Training.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training;

public class StructuredWorkoutServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static StructuredWorkoutService NewService(StubStructureRepository repo, StubUnitOfWork uow, ILoadService? loadService = null, Guid? athleteId = null) =>
        new(new StubCurrentUserService(athleteId ?? AthleteId),
            new WorkoutStructureRequestValidator(),
            repo, uow, loadService ?? new StubLoadService());

    private static PlannedWorkout OwnedWorkout(Guid planId, Guid pwId, Sport sport, IEnumerable<WorkoutBlock>? blocks = null) => new()
    {
        Id = pwId,
        AthleteId = AthleteId,
        TrainingPlanId = planId,
        Sport = sport,
        Title = "Session",
        Blocks = (blocks ?? Array.Empty<WorkoutBlock>()).ToList()
    };

    // A valid cardio structure: one 4× block with a Work + Recovery step (Run, zone 3 within 1..5).
    private static WorkoutStructureRequest CardioStructure() => new()
    {
        Blocks =
        {
            new WorkoutBlockDto
            {
                OrderIndex = 0,
                Repeats = 4,
                Steps =
                {
                    new WorkoutStepDto { Intent = StepIntent.Work, DurationSeconds = 180, TargetZone = 3 },
                    new WorkoutStepDto { Intent = StepIntent.Recovery, DurationSeconds = 120 }
                }
            }
        }
    };

    [Fact]
    public async Task SetStructureAsync_OwnedWorkout_StagesBlocksAndStepsAndCommitsOnce()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(planId, pwId, Sport.Run) };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        await service.SetStructureAsync(planId, pwId, CardioStructure());

        repo.AddedBlocks.Should().ContainSingle();
        var block = repo.AddedBlocks.Single();
        block.PlannedWorkoutId.Should().Be(pwId);
        block.AthleteId.Should().Be(AthleteId);
        block.Repeats.Should().Be(4);
        block.Steps.Should().HaveCount(2);
        block.Steps.Select(s => s.OrderIndex).Should().Equal(0, 1);
        block.Steps.Should().OnlyContain(s => s.AthleteId == AthleteId && s.WorkoutBlockId == block.Id);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task SetStructureAsync_ReplacesExistingBlocks()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var existing = new[]
        {
            new WorkoutBlock { Id = Guid.NewGuid(), AthleteId = AthleteId, PlannedWorkoutId = pwId },
            new WorkoutBlock { Id = Guid.NewGuid(), AthleteId = AthleteId, PlannedWorkoutId = pwId }
        };
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(planId, pwId, Sport.Run, existing) };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        await service.SetStructureAsync(planId, pwId, CardioStructure());

        repo.RemovedBlockIds.Should().BeEquivalentTo(existing.Select(b => b.Id));
        repo.AddedBlocks.Should().ContainSingle();
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStructureAsync_OwnedWorkout_ReturnsBlocksAndStepsOrdered()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var b0 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        // Deliberately reversed so the service's ordering is what's under test.
        var blocks = new[]
        {
            new WorkoutBlock
            {
                Id = b1, OrderIndex = 1, Repeats = 1, PlannedWorkoutId = pwId, AthleteId = AthleteId,
                Steps = { new WorkoutStep { Id = Guid.NewGuid(), OrderIndex = 0, WorkoutBlockId = b1 } }
            },
            new WorkoutBlock
            {
                Id = b0, OrderIndex = 0, Repeats = 2, PlannedWorkoutId = pwId, AthleteId = AthleteId,
                Steps =
                {
                    new WorkoutStep { Id = Guid.NewGuid(), OrderIndex = 1, WorkoutBlockId = b0 },
                    new WorkoutStep { Id = Guid.NewGuid(), OrderIndex = 0, WorkoutBlockId = b0 }
                }
            }
        };
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(planId, pwId, Sport.Bike, blocks) };
        var service = NewService(repo, new StubUnitOfWork());

        var result = await service.GetStructureAsync(planId, pwId);

        result.Blocks.Select(b => b.Id).Should().Equal(b0, b1);
        result.Blocks.First().Steps.Select(s => s.OrderIndex).Should().Equal(0, 1);
    }

    [Fact]
    public async Task SetStructureAsync_ForeignAthlete_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var foreign = OwnedWorkout(planId, pwId, Sport.Run);
        foreign.AthleteId = Guid.NewGuid(); // belongs to someone else
        var repo = new StubStructureRepository { ToReturn = foreign };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.SetStructureAsync(planId, pwId, CardioStructure());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.AddedBlocks.Should().BeEmpty();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SetStructureAsync_WorkoutUnderDifferentPlan_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        // Workout exists and is owned, but hangs off a different plan than the route's id.
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(Guid.NewGuid(), pwId, Sport.Run) };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.SetStructureAsync(planId, pwId, CardioStructure());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SetStructureAsync_CardioStepWithBothDurationAndDistance_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(planId, pwId, Sport.Run) };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = CardioStructure();
        request.Blocks[0].Steps[0].DistanceMeters = 1000; // now has both duration AND distance

        var act = () => service.SetStructureAsync(planId, pwId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.AddedBlocks.Should().BeEmpty();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SetStructureAsync_StrengthStepWithPowerTarget_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(planId, pwId, Sport.Strength) };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = new WorkoutStructureRequest
        {
            Blocks =
            {
                new WorkoutBlockDto
                {
                    OrderIndex = 0,
                    Repeats = 3,
                    Steps = { new WorkoutStepDto { Intent = StepIntent.Work, Sets = 3, Reps = 10, TargetPowerLow = 100 } }
                }
            }
        };

        var act = () => service.SetStructureAsync(planId, pwId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SetStructureAsync_TargetZoneOutOfRange_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(planId, pwId, Sport.Run) };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = CardioStructure();
        request.Blocks[0].Steps[0].TargetZone = 6; // Run has 5 zones

        var act = () => service.SetStructureAsync(planId, pwId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task GetStructureAsync_PlannedLoadOverride_EffectiveLoadUsesOverride()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var workout = OwnedWorkout(planId, pwId, Sport.Run);
        workout.PlannedLoad = 88m;
        var repo = new StubStructureRepository { ToReturn = workout };
        var service = NewService(repo, new StubUnitOfWork(), new StubLoadService { Computed = 42m });

        var result = await service.GetStructureAsync(planId, pwId);

        result.ComputedLoad.Should().Be(42m);
        result.IsLoadOverride.Should().BeTrue();
        result.EffectiveLoad.Should().Be(88m);
    }

    [Fact]
    public async Task GetStructureAsync_NoOverride_EffectiveLoadUsesComputed()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var repo = new StubStructureRepository { ToReturn = OwnedWorkout(planId, pwId, Sport.Run) };
        var service = NewService(repo, new StubUnitOfWork(), new StubLoadService { Computed = 42m });

        var result = await service.GetStructureAsync(planId, pwId);

        result.ComputedLoad.Should().Be(42m);
        result.IsLoadOverride.Should().BeFalse();
        result.EffectiveLoad.Should().Be(42m);
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
    }

    private sealed class StubStructureRepository : ITrainingPlanRepository
    {
        public PlannedWorkout? ToReturn { get; init; }
        public List<WorkoutBlock> AddedBlocks { get; } = new();
        public List<Guid> RemovedBlockIds { get; } = new();

        public Task<PlannedWorkout?> GetPlannedWorkoutWithStructureAsync(Guid plannedWorkoutId, CancellationToken ct = default)
            => Task.FromResult(ToReturn);

        public Task AddWorkoutBlockAsync(WorkoutBlock block, CancellationToken ct = default)
        {
            AddedBlocks.Add(block);
            return Task.CompletedTask;
        }

        public void RemoveWorkoutBlock(WorkoutBlock block) => RemovedBlockIds.Add(block.Id);

        // Unused by the structured-workout service.
        public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(TrainingPlan entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(TrainingPlan entity) => throw new NotImplementedException();
        public void Delete(TrainingPlan entity) => throw new NotImplementedException();
        public Task AddPlannedWorkoutAsync(PlannedWorkout plannedWorkout, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdatePlannedWorkout(PlannedWorkout plannedWorkout) => throw new NotImplementedException();
        public void RemovePlannedWorkout(PlannedWorkout plannedWorkout) => throw new NotImplementedException();
        public void UpdateWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
        public Task AddWorkoutStepAsync(WorkoutStep step, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdateWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
        public void RemoveWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
    }
}
