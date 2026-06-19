using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Calendar;
using Bryk.Application.Events;
using Bryk.Application.Onboarding;
using Bryk.Application.Training;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Calendar;

public class CalendarControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    // A plan whose window covers a past day (for the red-missed case) and a past day we'll complete
    // at ratio 1.0 (for the green-linked case). The plan spans 60 days so reschedule tests in 16-2/16-4
    // have room, but the calendar range we query is small.
    private static TrainingPlanRequest PlanSpanning(DateOnly start, DateOnly end) => new()
    {
        Name = "Calendar Test Plan",
        Methodology = MethodologyChoice.Polarized,
        StartDate = start,
        EndDate = end,
        PlannedWorkouts = new List<PlannedWorkoutDto>
        {
            new()
            {
                Sport = Sport.Run,
                ScheduledDate = Today.AddDays(-3),   // past, no completion → Red
                Title = "Missed Run",
                PlannedLoad = 80m
            },
            new()
            {
                Sport = Sport.Bike,
                ScheduledDate = Today.AddDays(-2),   // past, will be completed at ratio 1.0 → Green
                Title = "Done Ride",
                PlannedLoad = 100m
            }
        }
    };

    private static LogWorkoutRequest LogLinked(DateOnly date, Guid plannedWorkoutId, decimal load) => new()
    {
        Sport = Sport.Bike,
        CompletedDate = date,
        PlannedWorkoutId = plannedWorkoutId,
        ActualDurationSeconds = 3600,
        LoadOverride = load
    };

    private static LogWorkoutRequest LogUnplanned(DateOnly date) => new()
    {
        Sport = Sport.Run,
        CompletedDate = date,
        ActualDurationSeconds = 1800,
        LoadOverride = 50m
    };

    private static EventDto ValidEvent(DateOnly date, string name = "Race Day") => new()
    {
        Name = name,
        EventDate = date,
        Sport = Sport.Run,
        Priority = EventPriority.A,
        Notes = "Goal race"
    };

    // ── validation ──

    [Fact]
    public async Task FromAfterTo_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/calendar?from={Iso(Today)}&to={Iso(Today.AddDays(-5))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RangeOver62Days_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/calendar?from={Iso(Today.AddDays(-70))}&to={Iso(Today)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Defaults_Return42DayWindowEndingToday()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<CalendarFeedResponse>("/api/v1/calendar", JsonOptions);

        result.Should().NotBeNull();
        result!.RangeEnd.Should().Be(Today);
        result.RangeStart.Should().Be(Today.AddDays(-41));
        result.Days.Should().HaveCount(42);
    }

    // ── compliance + chip rendering ──

    [Fact]
    public async Task PastPlanned_NoCompletion_IsRed_WithNoWorkoutId()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await CreatePlanAsync(client, PlanSpanning(Today.AddDays(-10), Today.AddDays(50)));

        var from = Today.AddDays(-5);
        var feed = await client.GetFromJsonAsync<CalendarFeedResponse>(
            $"/api/v1/calendar?from={Iso(from)}&to={Iso(Today)}", JsonOptions);

        var missedDay = feed!.Days.Single(d => d.Date == Today.AddDays(-3));
        var planned = missedDay.Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.Planned).Subject;
        planned.Compliance.Should().Be(ComplianceBucket.Red);
        planned.WorkoutId.Should().BeNull();
        planned.TrainingPlanId.Should().NotBeNull(); // surfaced for the reschedule PATCH (16-3/16-4)
    }

    [Fact]
    public async Task LinkedCompletion_Ratio1_BothGreen_InverseLinksSet()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var plan = await CreatePlanAsync(client, PlanSpanning(Today.AddDays(-10), Today.AddDays(50)));
        var donePlannedId = plan.PlannedWorkouts.Single(pw => pw.Title == "Done Ride").Id;

        // Match the planned load (100) exactly → ratio 1.0 → Green.
        await client.PostAsJsonAsync("/api/v1/workouts", LogLinked(Today.AddDays(-2), donePlannedId, 100m));

        var from = Today.AddDays(-5);
        var feed = await client.GetFromJsonAsync<CalendarFeedResponse>(
            $"/api/v1/calendar?from={Iso(from)}&to={Iso(Today)}", JsonOptions);

        var day = feed!.Days.Single(d => d.Date == Today.AddDays(-2));
        var planned = day.Items.Single(i => i.Kind == CalendarItemKind.Planned);
        var completed = day.Items.Single(i => i.Kind == CalendarItemKind.Completed);

        planned.Compliance.Should().Be(ComplianceBucket.Green);
        planned.WorkoutId.Should().Be(completed.Id);
        completed.Compliance.Should().Be(ComplianceBucket.Green);
        completed.PlannedWorkoutId.Should().Be(planned.Id);
        completed.IsUnplanned.Should().BeFalse();
        completed.PlannedLoad.Should().Be(planned.PlannedLoad); // linked planned's effective load
    }

    [Fact]
    public async Task UnplannedCompletion_IsGreen_PlannedWorkoutIdNull()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var day = Today.AddDays(-1);
        await client.PostAsJsonAsync("/api/v1/workouts", LogUnplanned(day));

        var feed = await client.GetFromJsonAsync<CalendarFeedResponse>(
            $"/api/v1/calendar?from={Iso(day)}&to={Iso(Today)}", JsonOptions);

        var cell = feed!.Days.Single(d => d.Date == day);
        var completed = cell.Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.Completed).Subject;
        completed.IsUnplanned.Should().BeTrue();
        completed.Compliance.Should().Be(ComplianceBucket.Green);
        completed.PlannedWorkoutId.Should().BeNull();
    }

    [Fact]
    public async Task Event_OnSeededDay_KindEvent_PriorityAndNotesEchoed()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // EventDate must be today/future per EventDtoValidator, so query a forward-looking range.
        var day = Today.AddDays(1);
        await CreateEventAsync(client, ValidEvent(day, "Local 10K"));

        var feed = await client.GetFromJsonAsync<CalendarFeedResponse>(
            $"/api/v1/calendar?from={Iso(Today)}&to={Iso(Today.AddDays(5))}", JsonOptions);

        var cell = feed!.Days.Single(d => d.Date == day);
        var ev = cell.Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.Event).Subject;
        ev.Title.Should().Be("Local 10K");
        ev.Priority.Should().Be(EventPriority.A);
        ev.Notes.Should().Be("Goal race");
        ev.Compliance.Should().BeNull();      // events aren't graded
        ev.Load.Should().BeNull();
    }

    [Fact]
    public async Task EmptyDays_AppearForEveryDateInRange()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var from = Today.AddDays(-10);
        var to = Today;

        var feed = await client.GetFromJsonAsync<CalendarFeedResponse>(
            $"/api/v1/calendar?from={Iso(from)}&to={Iso(to)}", JsonOptions);

        feed!.Days.Should().HaveCount(11);
        feed.Days.Select(d => d.Date).Should().BeInAscendingOrder();
        feed.Days.First().Date.Should().Be(from);
        feed.Days.Last().Date.Should().Be(to);
        // A fresh athlete has no items anywhere — every day is empty.
        feed.Days.Should().OnlyContain(d => d.Items.Count == 0);
    }

    [Fact]
    public async Task FreshAthlete_Returns200_FullRange_AllDaysEmpty()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var from = Today.AddDays(-5);
        var to = Today.AddDays(5);

        var feed = await client.GetFromJsonAsync<CalendarFeedResponse>(
            $"/api/v1/calendar?from={Iso(from)}&to={Iso(to)}", JsonOptions);

        feed.Should().NotBeNull();
        feed!.RangeStart.Should().Be(from);
        feed.RangeEnd.Should().Be(to);
        feed.Days.Should().HaveCount(11);
        feed.Days.Should().OnlyContain(d => d.Items.Count == 0);
    }

    // ── helpers ──

    private static async Task<TrainingPlanResponse> CreatePlanAsync(HttpClient client, TrainingPlanRequest plan)
    {
        var response = await client.PostAsJsonAsync("/api/v1/trainingplans", plan);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        body.Should().NotBeNull();
        return body!;
    }

    private static async Task<EventResponse> CreateEventAsync(HttpClient client, EventDto dto)
    {
        var response = await client.PostAsJsonAsync("/api/v1/events", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        body.Should().NotBeNull();
        return body!;
    }
}
