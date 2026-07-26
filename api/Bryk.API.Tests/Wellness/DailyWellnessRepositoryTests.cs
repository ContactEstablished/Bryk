using Bryk.API.Tests.Fixtures;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using Bryk.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Wellness;

public class DailyWellnessRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetByAthleteAndDateTracked_RoundTripsEveryMetric()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var date = today.AddDays(-1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);
            await repo.AddAsync(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = date,
                SleepHours = 7.5m,
                SleepQuality = 4,
                RestingHr = 48,
                WeightKg = 72.40m,
                Soreness = 3,
                HrvMs = 88,
                Notes = "slept well"
            });
            await db.SaveChangesAsync();
        }

        // Fresh scope — proves the round trip survives a new DbContext instance, not just the change tracker.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var loaded = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, date);

            loaded.Should().NotBeNull();
            loaded!.Date.Should().Be(date);
            loaded.SleepHours.Should().Be(7.5m);
            loaded.SleepQuality.Should().Be(4);
            loaded.RestingHr.Should().Be(48);
            loaded.WeightKg.Should().Be(72.40m);
            loaded.Soreness.Should().Be(3);
            loaded.HrvMs.Should().Be(88);
            loaded.Notes.Should().Be("slept well");
        }
    }

    [Fact]
    public async Task GetByAthleteAndDateTracked_ReturnsATrackedInstance()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.Add(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = today,
                RestingHr = 50
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var loaded = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, today);
            loaded.Should().NotBeNull();

            // No repo.Update() call: the instance must already be tracked. This is the fact the whole
            // per-day upsert (Task 20-2) rests on — if AsNoTracking() is ever added to the repository
            // read, SaveChangesAsync persists nothing and this test fails.
            loaded!.RestingHr = 44;
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var reloaded = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, today);

            reloaded.Should().NotBeNull();
            reloaded!.RestingHr.Should().Be(44);
        }
    }

    [Fact]
    public async Task GetByAthleteAndDateTracked_ForAnotherAthlete_ReturnsNull()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.Add(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = today,
                SleepHours = 8m
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteAndDateTrackedAsync(Guid.NewGuid(), today);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetByAthleteAndDateTracked_ForADayWithNoEntry_ReturnsNull()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.Add(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = today.AddDays(-5),
                SleepHours = 8m
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, today);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetByAthleteInRange_IsInclusiveOnBothEndsAndAscending()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Seeded out of order on purpose — the repository, not the insert order, owns the sort.
            db.DailyWellness.AddRange(
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today, RestingHr = 46 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today.AddDays(-3), RestingHr = 47 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today.AddDays(-2), RestingHr = 48 });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteInRangeAsync(
                BrykWebApplicationFactory.TestAthleteId, today.AddDays(-3), today);

            result.Should().HaveCount(3);
            result.Should().BeInAscendingOrder(w => w.Date);
            result[0].Date.Should().Be(today.AddDays(-3));
            result[^1].Date.Should().Be(today);

            // Single-day range: both ends inclusive on the same date.
            var singleDay = await repo.GetByAthleteInRangeAsync(
                BrykWebApplicationFactory.TestAthleteId, today.AddDays(-2), today.AddDays(-2));

            singleDay.Should().ContainSingle();
            singleDay[0].Date.Should().Be(today.AddDays(-2));
        }
    }

    [Fact]
    public async Task GetByAthleteInRange_ExcludesOtherAthletes()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var otherAthleteId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.AddRange(
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today.AddDays(-1), HrvMs = 90 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today, HrvMs = 92 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = otherAthleteId, Date = today.AddDays(-1), HrvMs = 50 });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteInRangeAsync(
                BrykWebApplicationFactory.TestAthleteId, today.AddDays(-2), today);

            result.Should().HaveCount(2);
            result.Should().OnlyContain(w => w.AthleteId == BrykWebApplicationFactory.TestAthleteId);
        }
    }

    [Fact]
    public async Task GetByAthleteInRange_WithNoEntries_ReturnsEmpty()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = new DailyWellnessRepository(db);

        var result = await repo.GetByAthleteInRangeAsync(
            BrykWebApplicationFactory.TestAthleteId, today.AddDays(-6), today);

        result.Should().BeEmpty();
    }
}
