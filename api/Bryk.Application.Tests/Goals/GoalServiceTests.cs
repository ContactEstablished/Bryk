using Bryk.Application.Common;
using Bryk.Application.Goals;
using Bryk.Application.Onboarding;
using Bryk.Application.Onboarding.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Goals;

public class GoalServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static GoalDto ValidGoal(string description = "Run a sub-3 marathon") => new()
    {
        Type = GoalType.General,
        Description = description,
        TargetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3)
    };

    private static GoalService NewService(StubGoalRepository repo, StubUnitOfWork uow) =>
        new(new StubCurrentUserService(AthleteId), new GoalDtoValidator(), repo, uow);

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsForCurrentAthleteAndReturnsResponseWithId()
    {
        var repo = new StubGoalRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.CreateAsync(ValidGoal());

        repo.Added.Should().NotBeNull();
        repo.Added!.AthleteId.Should().Be(AthleteId);
        repo.Added.Description.Should().Be("Run a sub-3 marathon");
        result.Id.Should().NotBeEmpty();
        result.Id.Should().Be(repo.Added.Id);
        result.Description.Should().Be("Run a sub-3 marathon");
        result.Type.Should().Be(GoalType.General);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_WhenOwned_MutatesEntityAndReturnsUpdatedResponse()
    {
        var goalId = Guid.NewGuid();
        var existing = new Goal
        {
            Id = goalId,
            AthleteId = AthleteId,
            Type = GoalType.General,
            Description = "Original"
        };
        var repo = new StubGoalRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.UpdateAsync(goalId, ValidGoal("Rewritten"));

        existing.Description.Should().Be("Rewritten");
        repo.Updated.Should().BeSameAs(existing);
        result.Id.Should().Be(goalId);
        result.Description.Should().Be("Rewritten");
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ThrowsKeyNotFound()
    {
        var repo = new StubGoalRepository { ToReturn = null };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpdateAsync(Guid.NewGuid(), ValidGoal());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_WhenOwnedByAnotherAthlete_ThrowsKeyNotFound()
    {
        var goalId = Guid.NewGuid();
        var existing = new Goal
        {
            Id = goalId,
            AthleteId = Guid.NewGuid(), // belongs to someone else
            Type = GoalType.General,
            Description = "Theirs"
        };
        var repo = new StubGoalRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpdateAsync(goalId, ValidGoal());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_WhenOwned_DeletesEntity()
    {
        var goalId = Guid.NewGuid();
        var existing = new Goal { Id = goalId, AthleteId = AthleteId };
        var repo = new StubGoalRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        await service.DeleteAsync(goalId);

        repo.Deleted.Should().BeSameAs(existing);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenOwnedByAnotherAthlete_ThrowsKeyNotFound()
    {
        var goalId = Guid.NewGuid();
        var existing = new Goal { Id = goalId, AthleteId = Guid.NewGuid() };
        var repo = new StubGoalRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.DeleteAsync(goalId);

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

    private sealed class StubGoalRepository : IGoalRepository
    {
        public Goal? ToReturn { get; init; }
        public Goal? Added { get; private set; }
        public Goal? Updated { get; private set; }
        public Goal? Deleted { get; private set; }

        public Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);

        public Task AddAsync(Goal entity, CancellationToken ct = default)
        {
            Added = entity;
            return Task.CompletedTask;
        }

        public void Update(Goal entity) => Updated = entity;
        public void Delete(Goal entity) => Deleted = entity;

        public Task<IReadOnlyList<Goal>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }
}
