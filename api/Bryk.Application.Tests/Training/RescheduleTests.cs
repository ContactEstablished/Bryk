using Bryk.Application.Calendar;
using Bryk.Application.Calendar.Validators;
using Bryk.Application.Common;
using Bryk.Application.Training;
using Bryk.Application.Training.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training;

public class RescheduleTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TrainingPlanService NewService(StubTrainingPlanRepository repo, StubUnitOfWork uow, Guid? athleteId = null) =>
        new(new StubCurrentUserService(athleteId ?? AthleteId),
            new TrainingPlanRequestValidator(),
            new PlannedWorkoutDtoValidator(),
            new ScheduleRequestValidator(),
            repo, uow);

    private static TrainingPlan PlanWithWindow(DateOnly start, DateOnly end, Guid? planId = null, Guid? athleteId = null,
        Guid? pwId = null) =>
        new()
        {
            Id = planId ?? Guid.NewGuid(),
            AthleteId = athleteId ?? AthleteId,
            StartDate = start,
            EndDate = end,
            PlannedWorkouts = new List<PlannedWorkout>
            {
                new()
                {
                    Id = pwId ?? Guid.NewGuid(),
                    AthleteId = athleteId ?? AthleteId,
                    TrainingPlanId = planId ?? Guid.NewGuid(),
                    Sport = Sport.Run,
                    ScheduledDate = start.AddDays(1),
                    Title = "Easy Run",
                    Description = "Keep it easy",
                    PlannedDurationMinutes = 60,
                    PlannedLoad = 50m,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

    [Fact]
    public async Task RescheduleAsync_OnWindow_UpdatesScheduledDate()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var pwId = Guid.NewGuid();
        var plan = PlanWithWindow(start, end, pwId: pwId);
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = new ScheduleRequest { ScheduledDate = new DateOnly(2026, 6, 15) };

        await service.RescheduleAsync(plan.Id, pwId, request);

        repo.UpdatedPlannedWorkout.Should().NotBeNull();
        repo.UpdatedPlannedWorkout!.ScheduledDate.Should().Be(new DateOnly(2026, 6, 15));
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task RescheduleAsync_AtWindowStartBoundary_Succeeds()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var pwId = Guid.NewGuid();
        var plan = PlanWithWindow(start, end, pwId: pwId);
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = new ScheduleRequest { ScheduledDate = start };

        await service.RescheduleAsync(plan.Id, pwId, request);

        repo.UpdatedPlannedWorkout.Should().NotBeNull();
        repo.UpdatedPlannedWorkout!.ScheduledDate.Should().Be(start);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task RescheduleAsync_AtWindowEndBoundary_Succeeds()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var pwId = Guid.NewGuid();
        var plan = PlanWithWindow(start, end, pwId: pwId);
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = new ScheduleRequest { ScheduledDate = end };

        await service.RescheduleAsync(plan.Id, pwId, request);

        repo.UpdatedPlannedWorkout.Should().NotBeNull();
        repo.UpdatedPlannedWorkout!.ScheduledDate.Should().Be(end);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task RescheduleAsync_BelowWindow_ThrowsValidationException()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var pwId = Guid.NewGuid();
        var plan = PlanWithWindow(start, end, pwId: pwId);
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = new ScheduleRequest { ScheduledDate = new DateOnly(2026, 5, 31) };

        var act = () => service.RescheduleAsync(plan.Id, pwId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.StartsWith("ScheduledDate:")));
        repo.UpdatedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RescheduleAsync_AboveWindow_ThrowsValidationException()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var pwId = Guid.NewGuid();
        var plan = PlanWithWindow(start, end, pwId: pwId);
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = new ScheduleRequest { ScheduledDate = new DateOnly(2026, 7, 1) };

        var act = () => service.RescheduleAsync(plan.Id, pwId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.StartsWith("ScheduledDate:")));
        repo.UpdatedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RescheduleAsync_MissingPlan_ThrowsKeyNotFound()
    {
        var repo = new StubTrainingPlanRepository { ToReturn = null };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.RescheduleAsync(Guid.NewGuid(), Guid.NewGuid(),
            new ScheduleRequest { ScheduledDate = new DateOnly(2026, 6, 15) });

        await act.Should().ThrowAsync<KeyNotFoundException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RescheduleAsync_ForeignPlan_ThrowsKeyNotFound()
    {
        var plan = PlanWithWindow(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            athleteId: Guid.NewGuid());
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.RescheduleAsync(plan.Id, Guid.NewGuid(),
            new ScheduleRequest { ScheduledDate = new DateOnly(2026, 6, 15) });

        await act.Should().ThrowAsync<KeyNotFoundException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RescheduleAsync_MissingPlannedWorkout_ThrowsKeyNotFound()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var plan = PlanWithWindow(start, end);
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.RescheduleAsync(plan.Id, Guid.NewGuid(),
            new ScheduleRequest { ScheduledDate = new DateOnly(2026, 6, 15) });

        await act.Should().ThrowAsync<KeyNotFoundException>();
        uow.SaveCount.Should().Be(0);
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
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default) => throw new NotImplementedException();

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

        public Task<PlannedWorkout?> GetPlannedWorkoutWithStructureAsync(Guid plannedWorkoutId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddWorkoutBlockAsync(WorkoutBlock block, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdateWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
        public void RemoveWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
        public Task AddWorkoutStepAsync(WorkoutStep step, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdateWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
        public void RemoveWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
    }
}