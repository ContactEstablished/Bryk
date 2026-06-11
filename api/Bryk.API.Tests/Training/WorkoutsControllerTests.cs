using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Training;

public class WorkoutsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static LogWorkoutRequest ValidLog() => new()
    {
        Sport = Sport.Bike,
        CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        ActualDurationSeconds = 3600,
        AvgHr = 150
    };

    private static UpdateWorkoutRequest ValidUpdate() => new()
    {
        Sport = Sport.Bike,
        CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        ActualDurationSeconds = 1800,
        AvgHr = 140,
        Notes = "shortened"
    };

    [Fact]
    public async Task Update_ChangesFields_Returns200()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var logResponse = await client.PostAsJsonAsync("/api/v1/workouts", ValidLog());
        logResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var logged = await logResponse.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);
        logged.Should().NotBeNull();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/workouts/{logged!.Id}", ValidUpdate());
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(logged.Id);
        updated.ActualDurationSeconds.Should().Be(1800);
        updated.Notes.Should().Be("shortened");
    }

    [Fact]
    public async Task Update_NonexistentId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/v1/workouts/{Guid.NewGuid()}", ValidUpdate());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesWorkout_Returns204ThenGetIs404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var logResponse = await client.PostAsJsonAsync("/api/v1/workouts", ValidLog());
        var logged = await logResponse.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);
        logged.Should().NotBeNull();

        var deleteResponse = await client.DeleteAsync($"/api/v1/workouts/{logged!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/v1/workouts/{logged.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonexistentId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/workouts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static LogWorkoutRequest Log(Sport sport, DateOnly date) => new()
    {
        Sport = sport,
        CompletedDate = date,
        ActualDurationSeconds = 1200
    };

    [Fact]
    public async Task List_FiltersBySportAndDateRange_NewestFirst()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var d1 = new DateOnly(2026, 3, 1);
        var d2 = new DateOnly(2026, 3, 10);
        var d3 = new DateOnly(2026, 3, 20);
        await client.PostAsJsonAsync("/api/v1/workouts", Log(Sport.Run, d1));
        await client.PostAsJsonAsync("/api/v1/workouts", Log(Sport.Bike, d2));
        await client.PostAsJsonAsync("/api/v1/workouts", Log(Sport.Run, d3));

        // sport filter
        var runs = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/v1/workouts?sport=Run", JsonOptions);
        runs.Should().NotBeNull();
        runs!.Should().HaveCount(2);
        runs.Should().OnlyContain(w => w.Sport == Sport.Run);
        runs.Select(w => w.CompletedDate).Should().ContainInOrder(d3, d1);   // newest first

        // date range filter (inclusive)
        var ranged = await client.GetFromJsonAsync<List<WorkoutResponse>>(
            "/api/v1/workouts?from=2026-03-05&to=2026-03-20", JsonOptions);
        ranged!.Select(w => w.CompletedDate).Should().Equal(d3, d2);
    }

    [Fact]
    public async Task List_PagesAndIsBackwardCompatible()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        for (var i = 1; i <= 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/workouts", Log(Sport.Bike, new DateOnly(2026, 4, i)));
        }

        // page size + skip
        var page1 = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/v1/workouts?take=2", JsonOptions);
        page1!.Select(w => w.CompletedDate).Should().Equal(new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 4));

        var page2 = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/v1/workouts?skip=2&take=2", JsonOptions);
        page2!.Select(w => w.CompletedDate).Should().Equal(new DateOnly(2026, 4, 3), new DateOnly(2026, 4, 2));

        // non-breaking: bare read returns all five newest-first
        var all = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/v1/workouts", JsonOptions);
        all!.Should().HaveCount(5);
        all[0].CompletedDate.Should().Be(new DateOnly(2026, 4, 5));
    }
}
