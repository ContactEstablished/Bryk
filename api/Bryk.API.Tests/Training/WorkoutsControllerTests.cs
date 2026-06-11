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
}
