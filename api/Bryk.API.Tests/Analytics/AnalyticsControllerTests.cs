using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Analytics;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Analytics;

public class AnalyticsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    // Inject a deterministic daily load via LoadOverride (no sport profile exists in the in-memory DB, so
    // computed cardio load would be 0 — the override is the load and also proves it's respected).
    private static LogWorkoutRequest LogWithOverride(DateOnly date, decimal load) => new()
    {
        Sport = Sport.Bike,
        CompletedDate = date,
        ActualDurationSeconds = 3600,
        LoadOverride = load
    };

    // ── Validation (shared range contract) ──

    [Theory]
    [InlineData("/api/v1/analytics/pmc")]                       // both missing
    [InlineData("/api/v1/analytics/pmc?from=2026-01-01")]       // to missing
    [InlineData("/api/v1/analytics/pmc?to=2026-01-01")]         // from missing
    [InlineData("/api/v1/analytics/daily-load")]                // daily-load shares the rule
    [InlineData("/api/v1/analytics/time-in-zone")]              // time-in-zone shares the rule
    [InlineData("/api/v1/analytics/time-in-zone?from=2026-01-01")]
    public async Task MissingRange_Returns400(string url)
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FutureTo_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/analytics/pmc?from={Iso(Today.AddDays(-1))}&to={Iso(Today.AddDays(5))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RangeOver400Days_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/analytics/pmc?from={Iso(Today.AddDays(-401))}&to={Iso(Today)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FromAfterTo_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/analytics/pmc?from={Iso(Today)}&to={Iso(Today.AddDays(-5))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── daily-load ──

    [Fact]
    public async Task DailyLoad_ZeroFillsGaps_AndSumsOverridesPerDay()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var d0 = Today.AddDays(-10);
        var gap = Today.AddDays(-9);
        var d2 = Today.AddDays(-8);

        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(d0, 60m));
        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(d0, 25m)); // same day → sums
        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(d2, 40m));

        var series = await client.GetFromJsonAsync<List<DailyLoadDto>>(
            $"/api/v1/analytics/daily-load?from={Iso(d0)}&to={Iso(d2)}", JsonOptions);

        series.Should().NotBeNull();
        series!.Select(p => p.Date).Should().Equal(d0, gap, d2);   // contiguous, ordered
        series![0].Load.Should().Be(85m);   // 60 + 25, override respected
        series[1].Load.Should().Be(0m);     // gap day zero-filled, not skipped
        series[2].Load.Should().Be(40m);
    }

    // ── pmc ──

    [Fact]
    public async Task Pmc_FreshAthlete_CurrentNull_AndZeroSeries()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var from = Today.AddDays(-30);
        var pmc = await client.GetFromJsonAsync<PmcResponse>(
            $"/api/v1/analytics/pmc?from={Iso(from)}&to={Iso(Today)}", JsonOptions);

        pmc.Should().NotBeNull();
        pmc!.Current.Should().BeNull();                 // honesty: no history → no current
        pmc.Series.Should().HaveCount(31);              // inclusive 30..0
        pmc.Series.Should().OnlyContain(p => p.Load == 0m && p.Ctl == 0m && p.Atl == 0m && p.Tsb == 0m);
    }

    [Fact]
    public async Task Pmc_CurrentIsRangeEnd_AndAcwrNullUnder28Days()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // Only a few days of history → < 28 days → ACWR null, but TSB present.
        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(Today.AddDays(-3), 80m));

        var from = Today.AddDays(-30);
        var pmc = await client.GetFromJsonAsync<PmcResponse>(
            $"/api/v1/analytics/pmc?from={Iso(from)}&to={Iso(Today)}", JsonOptions);

        pmc.Should().NotBeNull();
        pmc!.Current.Should().NotBeNull();
        pmc.Current!.Date.Should().Be(Today);
        pmc.Current.Acwr.Should().BeNull();             // < 28 days of history
        pmc.Series.Should().HaveCount(31);
    }

    // ── weekly-load ──

    [Theory]
    [InlineData(0)]
    [InlineData(27)]
    public async Task WeeklyLoad_OutOfRange_Returns400(int weeks)
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/analytics/weekly-load?weeks={weeks}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WeeklyLoad_DefaultsTo8Weeks_FreshAthleteHasNoBand()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<WeeklyLoadResponse>(
            "/api/v1/analytics/weekly-load", JsonOptions);

        result.Should().NotBeNull();
        result!.Weeks.Should().HaveCount(8);                                // default span
        result.OptimalBand.Should().BeNull();                              // honest: no actual load
        result.Weeks.Should().OnlyContain(w => w.ActualLoad == 0m && w.PlannedLoad == 0m);
    }

    [Fact]
    public async Task WeeklyLoad_SumsCompletedActualLoad_AndComputesBand()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(Today, 75m));

        var result = await client.GetFromJsonAsync<WeeklyLoadResponse>(
            "/api/v1/analytics/weekly-load?weeks=8", JsonOptions);

        result.Should().NotBeNull();
        result!.Weeks.Sum(w => w.ActualLoad).Should().Be(75m);
        result.Weeks[^1].ActualLoad.Should().Be(75m);                      // today is in the current (last) week
        result.OptimalBand.Should().NotBeNull();                           // rolling average > 0
    }

    // ── peaks ──

    [Fact]
    public async Task Peaks_FreshAthlete_Empty()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PeaksResponse>("/api/v1/analytics/peaks", JsonOptions);

        result.Should().NotBeNull();
        result!.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Peaks_LoadRecord_PicksMax_AndSportFilterRestricts()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(Today.AddDays(-2), 90m));
        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(Today.AddDays(-1), 130m)); // best

        var all = await client.GetFromJsonAsync<PeaksResponse>("/api/v1/analytics/peaks", JsonOptions);
        var load = all!.Records.Single(r => r.Kind == PeakKind.Load);
        load.Value.Should().Be(130m);
        load.PreviousValue.Should().Be(90m);
        load.IsRecent.Should().BeTrue();

        // both workouts are Bike → filtering to Run yields nothing
        var run = await client.GetFromJsonAsync<PeaksResponse>("/api/v1/analytics/peaks?sport=Run", JsonOptions);
        run!.Records.Should().BeEmpty();
    }

    // ── time-in-zone ──

    [Fact]
    public async Task TimeInZone_UnlinkedNoHr_IsUnclassified_AndMethodsSumToTotal()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // An unlinked workout with no AvgHr (and no athlete MaxHr in the in-memory DB) → unclassified;
        // the duration is the whole total, proving the endpoint + the sums-to-total invariant end-to-end.
        var day = Today.AddDays(-3);
        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(day, 50m)); // duration 3600

        var result = await client.GetFromJsonAsync<TimeInZoneResponse>(
            $"/api/v1/analytics/time-in-zone?from={Iso(day.AddDays(-1))}&to={Iso(Today)}", JsonOptions);

        result.Should().NotBeNull();
        result!.TotalSeconds.Should().Be(3600);
        result.MethodBreakdown.UnclassifiedSeconds.Should().Be(3600);
        result.MethodBreakdown.StructureSeconds.Should().Be(0);
        result.MethodBreakdown.SessionAvgSeconds.Should().Be(0);
        (result.MethodBreakdown.StructureSeconds + result.MethodBreakdown.SessionAvgSeconds
            + result.MethodBreakdown.UnclassifiedSeconds).Should().Be(result.TotalSeconds);
        result.Zones.Should().HaveCount(5);
        result.Zones.Sum(z => z.Seconds).Should().Be(0);
    }

    [Fact]
    public async Task TimeInZone_WithNoImportedFiles_ReportsZeroSampleSeconds()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var day = Today.AddDays(-3);
        await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(day, 50m));

        var result = await client.GetFromJsonAsync<TimeInZoneResponse>(
            $"/api/v1/analytics/time-in-zone?from={Iso(day.AddDays(-1))}&to={Iso(Today)}", JsonOptions);

        result!.MethodBreakdown.SampleSeconds.Should().Be(0);
        result.MethodBreakdown.UnclassifiedSeconds.Should().Be(3600);
        result.TotalSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task TimeInZone_AfterCommittingAnImportedFile_ReportsSampleSeconds()
    {
        await using var factory = new BrykWebApplicationFactory();

        // Seed a bike FTP so the imported ride's power samples resolve into real zone bands — without a
        // threshold the athlete has no zone model and every sample would fall out of the histogram.
        using (var scope = factory.Services.CreateScope())
        {
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
                Sport = Sport.Bike,
                IsActive = true,
                ThresholdValue = 200m
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        var form = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", "sample-ride.tcx"));
        form.Add(new ByteArrayContent(bytes), "file", "sample-ride.tcx");

        var uploaded = await client.PostAsync("/api/v1/activityfiles", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var preview = await uploaded.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

        var committed = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{preview!.Id}/commit", new CommitActivityFileRequest());
        committed.StatusCode.Should().Be(HttpStatusCode.Created);

        // The ride fixture is dated 2026-06-02.
        var rideDate = new DateOnly(2026, 6, 2);
        var result = await client.GetFromJsonAsync<TimeInZoneResponse>(
            $"/api/v1/analytics/time-in-zone?from={Iso(rideDate.AddDays(-1))}&to={Iso(rideDate.AddDays(1))}",
            JsonOptions);

        result.Should().NotBeNull();
        result!.MethodBreakdown.SampleSeconds.Should().BePositive();
        (result.MethodBreakdown.SampleSeconds + result.MethodBreakdown.StructureSeconds
            + result.MethodBreakdown.SessionAvgSeconds + result.MethodBreakdown.UnclassifiedSeconds)
            .Should().Be(result.TotalSeconds);
    }
}
