using Bryk.Application.Common;
using Bryk.Application.Wellness;
using Bryk.Application.Wellness.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Wellness;

public class WellnessServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static WellnessService NewService(StubDailyWellnessRepository repo, StubUnitOfWork uow) =>
        new(new StubCurrentUserService(AthleteId),
            new WellnessEntryRequestValidator(),
            new WellnessRangeRequestValidator(),
            repo,
            uow);

    [Fact]
    public async Task UpsertAsync_WhenTheDayHasNoRow_AddsForTheCurrentAthleteAndSavesOnce()
    {
        var repo = new StubDailyWellnessRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);
        var date = Today.AddDays(-1);

        var result = await service.UpsertAsync(date, new WellnessEntryRequest
        {
            SleepHours = 7.5m,
            RestingHr = 48,
            Soreness = 3
        });

        repo.Added.Should().NotBeNull();
        repo.Added!.AthleteId.Should().Be(AthleteId);
        repo.Added.Date.Should().Be(date);
        repo.Added.SleepHours.Should().Be(7.5m);
        repo.Added.RestingHr.Should().Be(48);
        repo.Added.Soreness.Should().Be(3);
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(1);
        result.Id.Should().Be(repo.Added.Id);
        result.Date.Should().Be(date);
    }

    [Fact]
    public async Task UpsertAsync_WhenTheDayAlreadyHasARow_MutatesItAndDoesNotAdd()
    {
        // THE IDEMPOTENCY FACT at the unit level: the service looks first and updates in place.
        var date = Today.AddDays(-1);
        var existing = new DailyWellness
        {
            Id = Guid.NewGuid(),
            AthleteId = AthleteId,
            Date = date,
            SleepHours = 7m
        };
        var repo = new StubDailyWellnessRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.UpsertAsync(date, new WellnessEntryRequest
        {
            SleepHours = 8m,
            RestingHr = 47
        });

        repo.Added.Should().BeNull();
        existing.SleepHours.Should().Be(8m);
        existing.RestingHr.Should().Be(47);
        repo.Updated.Should().BeSameAs(existing);
        uow.SaveCount.Should().Be(1);
        result.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task UpsertAsync_ClearsAMetricOmittedFromTheRequest()
    {
        // PUT replaces the whole day (ADR-0011 §2).
        var date = Today.AddDays(-1);
        var existing = new DailyWellness
        {
            Id = Guid.NewGuid(),
            AthleteId = AthleteId,
            Date = date,
            RestingHr = 50
        };
        var repo = new StubDailyWellnessRepository { ToReturn = existing };
        var service = NewService(repo, new StubUnitOfWork());

        await service.UpsertAsync(date, new WellnessEntryRequest { SleepHours = 7m });

        existing.SleepHours.Should().Be(7m);
        existing.RestingHr.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_UsesTheRouteDateNotTheBodyDate()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());
        var routeDate = Today.AddDays(-1);

        await service.UpsertAsync(routeDate, new WellnessEntryRequest
        {
            Date = Today.AddDays(-5), // the body lies; the route wins
            RestingHr = 50
        });

        repo.Added!.Date.Should().Be(routeDate);
    }

    [Fact]
    public async Task UpsertAsync_FutureDate_ThrowsValidationExceptionAndPersistsNothing()
    {
        var repo = new StubDailyWellnessRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpsertAsync(Today.AddDays(1), new WellnessEntryRequest { RestingHr = 50 });

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Added.Should().BeNull();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpsertAsync_AllNullMetrics_ThrowsValidationExceptionAndPersistsNothing()
    {
        var repo = new StubDailyWellnessRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpsertAsync(Today, new WellnessEntryRequest());

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Added.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public void UpsertAsync_DoesNotResolveAnAthleteRepository()
    {
        // ADR-0011 §1 — cheap, permanent, structural. Wellness never reads or writes Athlete.
        var parameters = typeof(WellnessService).GetConstructors().Single().GetParameters();

        parameters.Should().NotContain(p => p.ParameterType == typeof(IAthleteRepository));
    }

    [Fact]
    public async Task GetRangeAsync_MissingBounds_ThrowsValidationException()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());

        var act = () => service.GetRangeAsync(null, null);

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.RangeQuery.Should().BeNull(); // the repository was never called
    }

    [Fact]
    public async Task GetRangeAsync_FromAfterTo_ThrowsValidationException()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());

        var act = () => service.GetRangeAsync(Today, Today.AddDays(-1));

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.RangeQuery.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_LoadsExactlyFourteenDaysEndingToday()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());

        await service.GetSummaryAsync();

        repo.RangeQuery.Should().NotBeNull();
        repo.RangeQuery!.Value.From.Should().Be(Today.AddDays(-13));
        repo.RangeQuery.Value.To.Should().Be(Today);
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

    private sealed class StubDailyWellnessRepository : IDailyWellnessRepository
    {
        public DailyWellness? ToReturn { get; init; }
        public IReadOnlyList<DailyWellness> RangeResult { get; init; } = [];

        public DailyWellness? Added { get; private set; }
        public DailyWellness? Updated { get; private set; }
        public (Guid AthleteId, DateOnly From, DateOnly To)? RangeQuery { get; private set; }

        public Task<DailyWellness?> GetByAthleteAndDateTrackedAsync(Guid athleteId, DateOnly date, CancellationToken ct = default)
            => Task.FromResult(ToReturn);

        public Task<IReadOnlyList<DailyWellness>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            RangeQuery = (athleteId, from, to);
            return Task.FromResult(RangeResult);
        }

        public Task AddAsync(DailyWellness entity, CancellationToken ct = default)
        {
            Added = entity;
            return Task.CompletedTask;
        }

        public void Update(DailyWellness entity) => Updated = entity;
    }
}
