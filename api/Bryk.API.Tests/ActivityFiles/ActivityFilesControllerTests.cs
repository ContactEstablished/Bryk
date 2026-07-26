using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class ActivityFilesControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // The run fixture starts 2026-06-01T06:00Z; the ride fixture 2026-06-02T06:00Z.
    private static readonly DateOnly RunDate = new(2026, 6, 1);

    private sealed record ApiError(int Status, string Error, string[]? Errors, string? TraceId);

    private static byte[] FixtureBytes(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    private static MultipartFormDataContent Multipart(byte[] content, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(content), "file", fileName);
        return form;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content, string fileName) =>
        client.PostAsync("/api/v1/activityfiles", Multipart(content, fileName));

    private static Task<HttpResponseMessage> UploadFixtureAsync(HttpClient client, string fixture, string? asName = null) =>
        UploadAsync(client, FixtureBytes(fixture), asName ?? fixture);

    private static async Task SeedAthleteAsync(BrykWebApplicationFactory factory, Sport sport, decimal threshold)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Athletes.Add(new Athlete
        {
            Id = BrykWebApplicationFactory.TestAthleteId,
            Name = "Test Athlete",
            Gender = Gender.Male,
            DateOfBirth = new DateOnly(1990, 1, 1),
            HeightCm = 180,
            WeightKg = 75,
            TypicalWeeklyHours = 10,
            Methodology = MethodologyChoice.Polarized
        });
        db.AthleteSportProfiles.Add(new AthleteSportProfile
        {
            Id = Guid.NewGuid(),
            AthleteId = BrykWebApplicationFactory.TestAthleteId,
            Sport = sport,
            IsActive = true,
            ThresholdValue = threshold
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedPlannedWorkoutAsync(
        BrykWebApplicationFactory factory, Sport sport, DateOnly date, string title = "Planned session", Guid? athleteId = null)
    {
        var owner = athleteId ?? BrykWebApplicationFactory.TestAthleteId;
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.TrainingPlans.Add(new TrainingPlan
        {
            Id = planId,
            AthleteId = owner,
            Name = "Plan",
            Methodology = MethodologyChoice.Polarized,
            StartDate = date.AddDays(-30),
            EndDate = date.AddDays(30),
            PlannedWorkouts = new List<PlannedWorkout>
            {
                new()
                {
                    Id = pwId,
                    AthleteId = owner,
                    TrainingPlanId = planId,
                    Sport = sport,
                    ScheduledDate = date,
                    Title = title,
                    PlannedDurationMinutes = 60,
                    PlannedLoad = 50m
                }
            }
        });
        await db.SaveChangesAsync();
        return pwId;
    }

    // ---------- upload ----------

    [Fact]
    public async Task Upload_TcxRunFixture_Returns201WithParsedPreview()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().NotBe(Guid.Empty);
        body.ByteSize.Should().BePositive();
        body.Format.Should().Be(ActivityFileFormat.Tcx);
        body.Parsed.Sport.Should().Be(Sport.Run);
        body.Parsed.DurationSeconds.Should().Be(600);
        body.Parsed.DistanceMeters.Should().Be(2000);
        body.Parsed.AvgHr.Should().Be(144);
        body.Parsed.AvgPace.Should().Be(300);
        body.Parsed.CompletedDate.Should().Be(RunDate);
        body.Parsed.SampleCount.Should().Be(5);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx", "ride.csv");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().Contain(e => e.StartsWith("FileName:"));
    }

    [Fact]
    public async Task Upload_ExtensionAndContentMismatch_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // TCX bytes announced as .fit — caught by the magic-byte sniff, before any parser runs.
        var response = await UploadFixtureAsync(client, "sample-run.tcx", "ride.fit");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().Contain(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task Upload_CorruptXml_Returns400AndPersistsNothing()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await UploadAsync(client, Encoding.UTF8.GetBytes("<not xml"), "ride.tcx");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ActivityFiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Upload_MissingFilePart_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/activityfiles", new MultipartFormDataContent());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_SameDaySameSportUnlinkedPlannedWorkout_IsOfferedAsACandidate()
    {
        await using var factory = new BrykWebApplicationFactory();
        var pwId = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate, "Easy 30");
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx");
        var body = await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        body!.MatchCandidates.Should().ContainSingle();
        body.MatchCandidates[0].PlannedWorkoutId.Should().Be(pwId);
        body.MatchCandidates[0].DayOffset.Should().Be(0);
        body.MatchCandidates[0].Title.Should().Be("Easy 30");
    }

    [Fact]
    public async Task Upload_PlannedWorkoutTwoDaysAway_IsNotOffered()
    {
        await using var factory = new BrykWebApplicationFactory();
        var minusTwo = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate.AddDays(-2), "minus two");
        var minusOne = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate.AddDays(-1), "minus one");
        var plusOne = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate.AddDays(1), "plus one");
        var plusTwo = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate.AddDays(2), "plus two");
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx");
        var body = await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        var ids = body!.MatchCandidates.Select(c => c.PlannedWorkoutId).ToList();
        ids.Should().Contain(new[] { minusOne, plusOne });
        ids.Should().NotContain(minusTwo);
        ids.Should().NotContain(plusTwo);
    }

    [Fact]
    public async Task Upload_PlannedWorkoutOfADifferentSport_IsNotOffered()
    {
        await using var factory = new BrykWebApplicationFactory();
        await SeedPlannedWorkoutAsync(factory, Sport.Bike, RunDate, "Bike session");
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx");
        var body = await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        body!.MatchCandidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_PlannedWorkoutAlreadyLinkedToAWorkout_IsNotOffered()
    {
        await using var factory = new BrykWebApplicationFactory();
        var pwId = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate, "Already done");
        var client = factory.CreateClient();

        var logged = await client.PostAsJsonAsync("/api/v1/workouts", new LogWorkoutRequest
        {
            PlannedWorkoutId = pwId,
            Sport = Sport.Run,
            CompletedDate = RunDate,
            ActualDurationSeconds = 600
        });
        logged.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await UploadFixtureAsync(client, "sample-run.tcx");
        var body = await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        body!.MatchCandidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_ZoneSeconds_AlwaysHasFiveBuckets()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx");
        var body = await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        body!.ZoneSeconds.Should().HaveCount(5);
        body.ZoneSeconds.Select(z => z.ZoneNumber).Should().Equal(1, 2, 3, 4, 5);
    }

    // ---------- commit ----------

    [Fact]
    public async Task Commit_BikeFileWithPower_ComputesLoadThroughThePowerIfBranch()
    {
        // The phase's headline assertion (ADR-0010 §3). sample-ride.tcx averages 210 W over 3600 s; with
        // FTP 200 that is IF 1.05 and TSS = 3600 × 1.05² / 3600 × 100 = 110.25. Without the synthetic
        // WorkoutStepResult the same file would fall to the HR branch and produce a different number.
        await using var factory = new BrykWebApplicationFactory();
        await SeedAthleteAsync(factory, Sport.Bike, threshold: 200m);
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-ride.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        var committed = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());

        committed.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await committed.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);
        body!.ComputedLoad.Should().Be(110.25m);

        // The preview promised the same number the commit persisted.
        preview.ComputedLoad.Should().Be(110.25m);
    }

    [Fact]
    public async Task Commit_CreatesExactlyOneSyntheticStepResult()
    {
        await using var factory = new BrykWebApplicationFactory();
        await SeedAthleteAsync(factory, Sport.Bike, threshold: 200m);
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-ride.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        var committed = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());
        var commitBody = await committed.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        var workout = await client.GetFromJsonAsync<WorkoutResponse>(
            $"/api/v1/workouts/{commitBody!.WorkoutId}", JsonOptions);

        workout!.StepResults.Should().ContainSingle();
        workout.StepResults[0].WorkoutStepId.Should().BeNull();
        workout.StepResults[0].OrderIndex.Should().Be(0);
        workout.StepResults[0].AvgPower.Should().Be(210);
        workout.StepResults[0].AvgHr.Should().Be(141);
    }

    [Fact]
    public async Task Commit_WithoutPlannedWorkoutId_CreatesAnUnlinkedWorkout()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        var committed = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());
        var body = await committed.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        body!.PlannedWorkoutId.Should().BeNull();
        var workout = await client.GetFromJsonAsync<WorkoutResponse>($"/api/v1/workouts/{body.WorkoutId}", JsonOptions);
        workout!.PlannedWorkoutId.Should().BeNull();
    }

    [Fact]
    public async Task Commit_WithOwnedPlannedWorkoutId_LinksIt()
    {
        await using var factory = new BrykWebApplicationFactory();
        var pwId = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate);
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        var committed = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{preview!.Id}/commit",
            new CommitActivityFileRequest { PlannedWorkoutId = pwId });

        committed.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await committed.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);
        body!.PlannedWorkoutId.Should().Be(pwId);

        var workout = await client.GetFromJsonAsync<WorkoutResponse>($"/api/v1/workouts/{body.WorkoutId}", JsonOptions);
        workout!.PlannedWorkoutId.Should().Be(pwId);
    }

    [Fact]
    public async Task Commit_ForeignPlannedWorkoutId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var foreignPwId = await SeedPlannedWorkoutAsync(factory, Sport.Run, RunDate, "Foreign", Guid.NewGuid());
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        var committed = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{preview!.Id}/commit",
            new CommitActivityFileRequest { PlannedWorkoutId = foreignPwId });

        committed.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Commit_Twice_Returns409()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        var first = await client.PostAsJsonAsync($"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync($"/api/v1/activityfiles/{preview.Id}/commit", new CommitActivityFileRequest());
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Workouts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Commit_UnknownFileId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{Guid.NewGuid()}/commit", new CommitActivityFileRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Commit_ForeignFileId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var foreignFileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = foreignFileId,
                AthleteId = Guid.NewGuid(),
                FileName = "foreign.tcx",
                Format = ActivityFileFormat.Tcx,
                ByteSize = 4,
                Content = new byte[] { 1, 2, 3, 4 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{foreignFileId}/commit", new CommitActivityFileRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Commit_PersistsTheZoneHistogramJson()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        await client.PostAsJsonAsync($"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.ActivityFiles.SingleAsync(f => f.Id == preview.Id);

        row.ZoneHistogramJson.Should().NotBeNullOrEmpty();
        // camelCase is the shape Task 19-6 reads back.
        var entries = JsonSerializer.Deserialize<List<ZoneHistogramEntry>>(row.ZoneHistogramJson!, JsonOptions);
        entries.Should().HaveCount(5);
        entries!.Select(e => e.ZoneNumber).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Commit_SetsParsedWorkoutIdToTheNewWorkout()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        var committed = await client.PostAsJsonAsync($"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());
        var body = await committed.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.ActivityFiles.SingleAsync(f => f.Id == preview.Id);

        row.ParsedWorkoutId.Should().Be(body!.WorkoutId);
    }

    // ---------- discard + source ----------

    [Fact]
    public async Task Discard_UncommittedFile_Returns204AndRemovesTheRow()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        var response = await client.DeleteAsync($"/api/v1/activityfiles/{preview!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ActivityFiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Discard_CommittedFile_Returns409()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        await client.PostAsJsonAsync($"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());

        var response = await client.DeleteAsync($"/api/v1/activityfiles/{preview.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Discard_ForeignFile_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var foreignFileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = foreignFileId,
                AthleteId = Guid.NewGuid(),
                FileName = "foreign.tcx",
                Format = ActivityFileFormat.Tcx,
                ByteSize = 4,
                Content = new byte[] { 1, 2, 3, 4 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/activityfiles/{foreignFileId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSource_ForACommittedWorkout_ReturnsTheFileSummary()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadFixtureAsync(client, "sample-run.tcx");
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        var committed = await client.PostAsJsonAsync($"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());
        var commitBody = await committed.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/activityfiles/by-workout/{commitBody!.WorkoutId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var source = await response.Content.ReadFromJsonAsync<ActivityFileSourceResponse>(JsonOptions);
        source.Should().NotBeNull();
        source!.FileName.Should().Be("sample-run.tcx");
        source.Format.Should().Be(ActivityFileFormat.Tcx);
    }

    [Fact]
    public async Task GetSource_ForAManuallyLoggedWorkout_Returns200WithNullBody()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var logged = await client.PostAsJsonAsync("/api/v1/workouts", new LogWorkoutRequest
        {
            Sport = Sport.Run,
            CompletedDate = RunDate,
            ActualDurationSeconds = 600
        });
        var workout = await logged.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/activityfiles/by-workout/{workout!.Id}");

        // 200 with a null body, NOT 404 — "this workout has no source file" is the common case.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Trim().Should().Be("null");
    }
}
