using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Training;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Training;

public class ThisWeekControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GetThisWeek_ReturnsInWeekPlannedWorkout()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new TrainingPlanRequest
        {
            Name = "Current Block",
            Methodology = MethodologyChoice.Polarized,
            StartDate = today.AddDays(-7),
            EndDate = today.AddDays(21),
            PlannedWorkouts = new List<PlannedWorkoutDto>
            {
                new() { Sport = Sport.Run, ScheduledDate = today, Title = "Today's Run", PlannedDurationMinutes = 50 },
                new() { Sport = Sport.Bike, ScheduledDate = today.AddDays(20), Title = "Far Future Ride" }
            }
        };

        (await client.PostAsJsonAsync("/api/v1/trainingplans", plan))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync("/api/v1/training/this-week");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var thisWeek = await response.Content.ReadFromJsonAsync<ThisWeekResponse>(JsonOptions);
        thisWeek.Should().NotBeNull();
        thisWeek!.WeekStart.DayOfWeek.Should().Be(DayOfWeek.Monday);
        thisWeek.WeekEnd.Should().Be(thisWeek.WeekStart.AddDays(6));
        thisWeek.PlannedWorkouts.Should().ContainSingle(pw => pw.Title == "Today's Run");
        thisWeek.PlannedWorkouts.Should().NotContain(pw => pw.Title == "Far Future Ride");
    }

    [Fact]
    public async Task GetThisWeek_NoPlans_Returns200WithEmptyList()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/training/this-week");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var thisWeek = await response.Content.ReadFromJsonAsync<ThisWeekResponse>(JsonOptions);
        thisWeek.Should().NotBeNull();
        thisWeek!.PlannedWorkouts.Should().BeEmpty();
    }
}
