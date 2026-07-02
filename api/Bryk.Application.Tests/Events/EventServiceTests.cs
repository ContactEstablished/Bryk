using Bryk.Application.Common;
using Bryk.Application.Events;
using Bryk.Application.Onboarding;
using Bryk.Application.Onboarding.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Events;

public class EventServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static EventDto ValidEvent(string name = "Spring Marathon") => new()
    {
        Name = name,
        EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6),
        Sport = Sport.Run,
        Priority = EventPriority.A,
        Notes = "Goal race"
    };

    private static EventService NewService(StubEventRepository repo, StubUnitOfWork uow) =>
        new(new StubCurrentUserService(AthleteId), new EventDtoValidator(), repo, new StubPlanRepository(), uow);

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsForCurrentAthleteAndReturnsResponseWithId()
    {
        var repo = new StubEventRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.CreateAsync(ValidEvent());

        repo.Added.Should().NotBeNull();
        repo.Added!.AthleteId.Should().Be(AthleteId);
        repo.Added.Name.Should().Be("Spring Marathon");
        result.Id.Should().NotBeEmpty();
        result.Id.Should().Be(repo.Added.Id);
        result.Name.Should().Be("Spring Marathon");
        result.Sport.Should().Be(Sport.Run);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_WhenOwned_MutatesEntityAndReturnsUpdatedResponse()
    {
        var eventId = Guid.NewGuid();
        var existing = new Event
        {
            Id = eventId,
            AthleteId = AthleteId,
            Name = "Original",
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2),
            Priority = EventPriority.B
        };
        var repo = new StubEventRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.UpdateAsync(eventId, ValidEvent("Renamed"));

        existing.Name.Should().Be("Renamed");
        repo.Updated.Should().BeSameAs(existing);
        result.Id.Should().Be(eventId);
        result.Name.Should().Be("Renamed");
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ThrowsKeyNotFound()
    {
        var repo = new StubEventRepository { ToReturn = null };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpdateAsync(Guid.NewGuid(), ValidEvent());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_WhenOwnedByAnotherAthlete_ThrowsKeyNotFound()
    {
        var eventId = Guid.NewGuid();
        var existing = new Event
        {
            Id = eventId,
            AthleteId = Guid.NewGuid(), // belongs to someone else
            Name = "Theirs",
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2),
            Priority = EventPriority.B
        };
        var repo = new StubEventRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpdateAsync(eventId, ValidEvent());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_WhenOwned_DeletesEntity()
    {
        var eventId = Guid.NewGuid();
        var existing = new Event { Id = eventId, AthleteId = AthleteId };
        var repo = new StubEventRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        await service.DeleteAsync(eventId);

        repo.Deleted.Should().BeSameAs(existing);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenOwnedByAnotherAthlete_ThrowsKeyNotFound()
    {
        var eventId = Guid.NewGuid();
        var existing = new Event { Id = eventId, AthleteId = Guid.NewGuid() };
        var repo = new StubEventRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.DeleteAsync(eventId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Deleted.Should().BeNull();
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

    private sealed class StubEventRepository : IEventRepository
    {
        public Event? ToReturn { get; init; }
        public Event? Added { get; private set; }
        public Event? Updated { get; private set; }
        public Event? Deleted { get; private set; }

        public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);

        public Task AddAsync(Event entity, CancellationToken ct = default)
        {
            Added = entity;
            return Task.CompletedTask;
        }

        public void Update(Event entity) => Updated = entity;
        public void Delete(Event entity) => Deleted = entity;

        public Task<IReadOnlyList<Event>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Event>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
    }

    // Create/Update/Delete don't touch the plan repo; the read paths are covered by integration tests.
    private sealed class StubPlanRepository : ITrainingPlanRepository
    {
        public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default) => throw new NotImplementedException();
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
}
