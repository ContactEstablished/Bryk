using Bryk.API.Tests.Fixtures;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using Bryk.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class ActivityFileRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetByIdTracked_RoundTripsContentFormatAndByteSize()
    {
        await using var factory = new BrykWebApplicationFactory();
        var fileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            await repo.AddAsync(new ActivityFile
            {
                Id = fileId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                FileName = "ride.tcx",
                Format = ActivityFileFormat.Tcx,
                ByteSize = 4,
                Content = new byte[] { 1, 2, 3, 4 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Fresh scope — proves the round trip survives a new DbContext instance, not just the change tracker.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            var loaded = await repo.GetByIdTrackedAsync(fileId);

            loaded.Should().NotBeNull();
            loaded!.Content.Should().Equal(1, 2, 3, 4);
            loaded.Format.Should().Be(ActivityFileFormat.Tcx);
            loaded.ByteSize.Should().Be(4);
        }
    }

    [Fact]
    public async Task GetByParsedWorkoutIds_EmptyIds_ReturnsEmpty()
    {
        await using var factory = new BrykWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = new ActivityFileRepository(db);

        var result = await repo.GetByParsedWorkoutIdsAsync(BrykWebApplicationFactory.TestAthleteId, Array.Empty<Guid>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByParsedWorkoutIds_ReturnsOnlyMatchingRowsForThatAthlete()
    {
        await using var factory = new BrykWebApplicationFactory();
        var w1 = Guid.NewGuid();
        var otherAthleteId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.AddRange(
                new ActivityFile { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, FileName = "a.fit", Format = ActivityFileFormat.Fit, ByteSize = 1, Content = new byte[] { 1 }, UploadedAt = DateTime.UtcNow, ParsedWorkoutId = w1 },
                new ActivityFile { Id = Guid.NewGuid(), AthleteId = otherAthleteId, FileName = "b.fit", Format = ActivityFileFormat.Fit, ByteSize = 1, Content = new byte[] { 1 }, UploadedAt = DateTime.UtcNow, ParsedWorkoutId = w1 },
                new ActivityFile { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, FileName = "c.fit", Format = ActivityFileFormat.Fit, ByteSize = 1, Content = new byte[] { 1 }, UploadedAt = DateTime.UtcNow, ParsedWorkoutId = null });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);

            var result = await repo.GetByParsedWorkoutIdsAsync(BrykWebApplicationFactory.TestAthleteId, new[] { w1 });

            result.Should().ContainSingle();
            result[0].ParsedWorkoutId.Should().Be(w1);
            result[0].AthleteId.Should().Be(BrykWebApplicationFactory.TestAthleteId);
        }
    }

    [Fact]
    public async Task GetByParsedWorkoutIds_DoesNotLoadContent()
    {
        await using var factory = new BrykWebApplicationFactory();
        var workoutId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                FileName = "run.fit",
                Format = ActivityFileFormat.Fit,
                ByteSize = 4,
                Content = new byte[] { 9, 9, 9, 9 },
                UploadedAt = DateTime.UtcNow,
                ParsedWorkoutId = workoutId,
                ZoneHistogramJson = "[]"
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);

            var result = await repo.GetByParsedWorkoutIdsAsync(BrykWebApplicationFactory.TestAthleteId, new[] { workoutId });

            result.Should().ContainSingle();
            result[0].Content.Should().BeEmpty(); // proves the projection dropped the varbinary column
            result[0].ByteSize.Should().Be(4);     // ...while keeping the cheap scalar columns
            result[0].ZoneHistogramJson.Should().Be("[]");
        }
    }

    [Fact]
    public async Task Delete_RemovesTheRow()
    {
        await using var factory = new BrykWebApplicationFactory();
        var fileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = fileId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                FileName = "delete-me.gpx",
                Format = ActivityFileFormat.Gpx,
                ByteSize = 2,
                Content = new byte[] { 5, 6 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            var tracked = await repo.GetByIdTrackedAsync(fileId);
            repo.Delete(tracked!);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            var result = await repo.GetByIdTrackedAsync(fileId);
            result.Should().BeNull();
        }
    }
}
