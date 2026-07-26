using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Wellness;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Wellness;

public class WellnessControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class ApiError
    {
        public int Status { get; set; }
        public string? Error { get; set; }
        public string[]? Errors { get; set; }
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Url(DateOnly date) => $"/api/v1/wellness/{date:yyyy-MM-dd}";

    [Fact]
    public async Task Put_CreatesTheDayAndReturnsOkWithTheStoredValues()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var yesterday = Today.AddDays(-1);

        var response = await client.PutAsJsonAsync(Url(yesterday), new
        {
            sleepHours = 7.5m,
            restingHr = 48,
            soreness = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK); // 200, not 201 — the URL is client-chosen
        var body = await response.Content.ReadFromJsonAsync<WellnessEntryResponse>(JsonOptions);
        body!.Id.Should().NotBeEmpty();
        body.Date.Should().Be(yesterday);
        body.SleepHours.Should().Be(7.5m);
        body.RestingHr.Should().Be(48);
        body.Soreness.Should().Be(3);
    }

    [Fact]
    public async Task Put_Twice_UpdatesInPlaceAndLeavesExactlyOneRow()
    {
        // THE HEADLINE FACT. Idempotency is proven by counting rows through the API, NOT by asserting a
        // duplicate insert throws: the {AthleteId, Date} unique index is real in SQL Server but the EF
        // InMemory provider enforces no unique index (BrykWebApplicationFactory.cs:11-23), so a
        // "duplicate throws" test would pass for the wrong reason here and fail against SQL Server.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var day = Today.AddDays(-1);

        var first = await client.PutAsJsonAsync(Url(day), new { sleepHours = 7m });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutAsJsonAsync(Url(day), new { sleepHours = 8m, restingHr = 47 });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var range = await client.GetFromJsonAsync<List<WellnessEntryResponse>>(
            $"/api/v1/wellness?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}", JsonOptions);

        range.Should().ContainSingle();
        range![0].SleepHours.Should().Be(8m);
        range[0].RestingHr.Should().Be(47);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.DailyWellness.CountAsync(w => w.Date == day)).Should().Be(1);
    }

    [Fact]
    public async Task Put_MalformedDateSegment_Returns404()
    {
        // LAYER ONE: the {date:datetime} route constraint rejects the segment before any binding
        // happens. Pinned precisely because SuppressModelStateInvalidFilter (Program.cs:32-33) means an
        // UNCONSTRAINED route would have bound 0001-01-01 and RUN THE ACTION. A 200 here is a data bug.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/wellness/not-a-date", new { sleepHours = 7m });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_MinValueDateSegment_Returns400WithADateMessage()
    {
        // LAYER TWO: 0001-01-01 is a well-formed date, so it satisfies the route constraint and binds
        // cleanly — the validator's default(DateOnly) rule is the only thing that stops it.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/wellness/0001-01-01", new { sleepHours = 7m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().NotBeNull();
        error.Errors![0].Should().StartWith("Date:");
    }

    [Fact]
    public async Task Put_FutureDate_Returns400WithADateMessage()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(Url(Today.AddDays(1)), new { sleepHours = 7m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors![0].Should().StartWith("Date:");
    }

    [Fact]
    public async Task Put_OutOfRangeMetric_Returns400WithTheFieldName()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(Url(Today), new { restingHr = 200 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        // The ROADMAP's "field messages" criterion: ValidateOrThrowAsync drops property names, so the
        // message has to carry its own.
        error!.Errors.Should().Contain(e => e.StartsWith("RestingHr:"));
    }

    [Fact]
    public async Task Put_EmptyBody_Returns400WithTheEntryMessage()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(Url(Today), new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().Contain(e => e.StartsWith("Entry:"));
    }

    [Fact]
    public async Task Put_DoesNotModifyTheAthleteRow()
    {
        // ADR-0011 §1 — wellness is independent of Athlete and never writes back to it.
        await using var factory = new BrykWebApplicationFactory();

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
                WeightKg = 70m,
                RestingHr = 55,
                TypicalWeeklyHours = 10,
                Methodology = MethodologyChoice.Polarized
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.PutAsJsonAsync(Url(Today), new { restingHr = 44, weightKg = 68.5m });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var freshScope = factory.Services.CreateScope();
        var freshDb = freshScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var athlete = await freshDb.Athletes.AsNoTracking()
            .SingleAsync(a => a.Id == BrykWebApplicationFactory.TestAthleteId);

        athlete.RestingHr.Should().Be(55);
        athlete.WeightKg.Should().Be(70m);
    }

    [Fact]
    public async Task Get_Range_ReturnsOnlyDaysWithEntriesAscending()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var d3 = Today.AddDays(-3);
        var d2 = Today.AddDays(-2);
        var d1 = Today.AddDays(-1);

        await client.PutAsJsonAsync(Url(d3), new { sleepHours = 6m });
        await client.PutAsJsonAsync(Url(d2), new { sleepHours = 7m });
        await client.PutAsJsonAsync(Url(d1), new { sleepHours = 8m });

        var range = await client.GetFromJsonAsync<List<WellnessEntryResponse>>(
            $"/api/v1/wellness?from={d3:yyyy-MM-dd}&to={d2:yyyy-MM-dd}", JsonOptions);

        range.Should().HaveCount(2);
        range.Should().BeInAscendingOrder(e => e.Date);
        range![0].Date.Should().Be(d3);
        range[1].Date.Should().Be(d2);
    }

    [Fact]
    public async Task Get_Range_MissingBounds_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/wellness");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Range_FromAfterTo_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/wellness?from={Today:yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Summary_WithNoEntries_ReturnsNullAveragesAndHasAnyEntriesFalse()
    {
        // A fresh factory means a fresh database, so this athlete has never logged wellness.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var summary = await client.GetFromJsonAsync<WellnessSummaryResponse>("/api/v1/wellness/summary", JsonOptions);

        summary!.HasAnyEntries.Should().BeFalse();
        summary.SleepHours.Average.Should().BeNull();   // null, never 0
        summary.SleepHours.PriorAverage.Should().BeNull();
        summary.SleepHours.Delta.Should().BeNull();
        summary.SleepHours.DaysWithData.Should().Be(0);
        summary.Days.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_Summary_ReturnsTheSevenDayAverageAndTheDailySeries()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PutAsJsonAsync(Url(Today), new { sleepHours = 8m });
        await client.PutAsJsonAsync(Url(Today.AddDays(-1)), new { sleepHours = 7m });

        var summary = await client.GetFromJsonAsync<WellnessSummaryResponse>("/api/v1/wellness/summary", JsonOptions);

        summary!.HasAnyEntries.Should().BeTrue();
        summary.SleepHours.Average.Should().Be(7.5m);
        summary.SleepHours.DaysWithData.Should().Be(2);
        summary.Days.Should().HaveCount(2);
        summary.Days.Should().BeInAscendingOrder(d => d.Date);
    }

    [Fact]
    public async Task Get_Summary_DeltaIsNullWithNoPriorWeekData()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PutAsJsonAsync(Url(Today), new { sleepHours = 8m });
        await client.PutAsJsonAsync(Url(Today.AddDays(-1)), new { sleepHours = 7m });

        var summary = await client.GetFromJsonAsync<WellnessSummaryResponse>("/api/v1/wellness/summary", JsonOptions);

        summary!.SleepHours.PriorAverage.Should().BeNull();
        summary.SleepHours.Delta.Should().BeNull(); // never fabricated as 0
    }
}
