using Bryk.Application.Common;
using Bryk.Application.Training;
using Bryk.Application.Training.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training;

public class TrainingPlanServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TrainingPlanRequest ValidPlan(string name = "Base Block") => new()
    {
        Name = name,
        Methodology = MethodologyChoice.Polarized,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(28)
    };

    private static PlannedWorkoutDto ValidWorkout(string title = "Easy Run") => new()
    {
        Sport = Sport.Run,
        ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
        Title = title,
        PlannedDurationMinutes = 60,
        PlannedLoad = 50.0m
    };

    private static TrainingPlanService NewService(StubTrainingPlanRepository repo, StubUnitOfWork uow, Guid? athleteId = null) =>
        new(new StubCurrentUserService(athleteId ?? AthleteId),
            new TrainingPlanRequestValidator(),
            new PlannedWorkoutDtoValidator(),
            repo, uow);

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsForCurrentAthleteWithChildren()
    {
        var repo = new StubTrainingPlanRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidPlan();
        request.PlannedWorkouts = new List<PlannedWorkoutDto> { ValidWorkout("Long Ride") };

        var result = await service.CreateAsync(request);

        repo.Added.Should().NotBeNull();
        repo.Added!.AthleteId.Should().Be(AthleteId);
        repo.Added.PlannedWorkouts.Should().ContainSingle();
        repo.Added.PlannedWorkouts.Single().AthleteId.Should().Be(AthleteId);
        repo.Added.PlannedWorkouts.Single().TrainingPlanId.Should().Be(repo.Added.Id);
        result.Id.Should().NotBeEmpty().And.Be(repo.Added.Id);
        result.Name.Should().Be("Base Block");
        result.PlannedWorkouts.Should().ContainSingle(pw => pw.Title == "Long Ride" && pw.Id != Guid.Empty);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_EndDateBeforeStartDate_ThrowsValidation()
    {
        var repo = new StubTrainingPlanRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidPlan();
        request.EndDate = request.StartDate.AddDays(-1);

        var act = () => service.CreateAsync(request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Added.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task AddPlannedWorkoutAsync_OwnedPlan_StagesAndReturnsResponse()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.AddPlannedWorkoutAsync(planId, ValidWorkout("Tempo"));

        repo.AddedPlannedWorkout.Should().NotBeNull();
        repo.AddedPlannedWorkout!.TrainingPlanId.Should().Be(planId);
        repo.AddedPlannedWorkout.AthleteId.Should().Be(AthleteId);
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("Tempo");
        result.TrainingPlanId.Should().Be(planId);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task AddPlannedWorkoutAsync_EmptyTitle_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var invalid = ValidWorkout();
        invalid.Title = "";

        var act = () => service.AddPlannedWorkoutAsync(planId, invalid);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.AddedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdatePlannedWorkoutAsync_ForeignPlan_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = Guid.NewGuid() }; // belongs to someone else
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpdatePlannedWorkoutAsync(planId, Guid.NewGuid(), ValidWorkout());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.UpdatedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RemovePlannedWorkoutAsync_ForeignPlan_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = Guid.NewGuid() }; // belongs to someone else
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.RemovePlannedWorkoutAsync(planId, Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.RemovedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RemovePlannedWorkoutAsync_OwnedPlannedWorkout_Removes()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var plan = new TrainingPlan
        {
            Id = planId,
            AthleteId = AthleteId,
            PlannedWorkouts = new List<PlannedWorkout>
            {
                new() { Id = pwId, AthleteId = AthleteId, TrainingPlanId = planId, Title = "Drop me" }
            }
        };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        await service.RemovePlannedWorkoutAsync(planId, pwId);

        repo.RemovedPlannedWorkout.Should().NotBeNull();
        repo.RemovedPlannedWorkout!.Id.Should().Be(pwId);
        uow.SaveCount.Should().Be(1);
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

    private sealed class StubTrainingPlanRepository : ITrainingPlanRepository
    {
        public TrainingPlan? ToReturn { get; init; }
        public IReadOnlyList<TrainingPlan> ByAthlete { get; init; } = new List<TrainingPlan>();
        public TrainingPlan? Added { get; private set; }
        public TrainingPlan? Updated { get; private set; }
        public TrainingPlan? Deleted { get; private set; }
        public PlannedWorkout? AddedPlannedWorkout { get; private set; }
        public PlannedWorkout? UpdatedPlannedWorkout { get; private set; }
        public PlannedWorkout? RemovedPlannedWorkout { get; private set; }

        public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => Task.FromResult(ByAthlete);
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();

        public Task AddAsync(TrainingPlan entity, CancellationToken ct = default)
        {
            Added = entity;
            return Task.CompletedTask;
        }

        public void Update(TrainingPlan entity) => Updated = entity;
        public void Delete(TrainingPlan entity) => Deleted = entity;

        public Task AddPlannedWorkoutAsync(PlannedWorkout plannedWorkout, CancellationToken ct = default)
        {
            AddedPlannedWorkout = plannedWorkout;
            return Task.CompletedTask;
        }

        public void UpdatePlannedWorkout(PlannedWorkout plannedWorkout) => UpdatedPlannedWorkout = plannedWorkout;
        public void RemovePlannedWorkout(PlannedWorkout plannedWorkout) => RemovedPlannedWorkout = plannedWorkout;
    }
}
