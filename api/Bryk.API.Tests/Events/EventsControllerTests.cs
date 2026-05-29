using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Events;
using Bryk.Application.Onboarding;
using Bryk.Application.Profile;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Events;

public class EventsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static EventDto ValidEvent(string name = "Spring Marathon") => new()
    {
        Name = name,
        EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6),
        Sport = Sport.Run,
        Priority = EventPriority.A,
        Notes = "Goal race"
    };

    [Fact]
    public async Task Create_ReturnsCreatedWithId()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/events", ValidEvent());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Name.Should().Be("Spring Marathon");
        body.Sport.Should().Be(Sport.Run);
        body.Priority.Should().Be(EventPriority.A);
    }

    [Fact]
    public async Task Update_ReturnsOkWithUpdatedResource()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var created = await CreateAsync(client, ValidEvent("Original"));

        var response = await client.PutAsJsonAsync($"/api/v1/events/{created.Id}", ValidEvent("Renamed"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var created = await CreateAsync(client, ValidEvent());

        var response = await client.DeleteAsync($"/api/v1/events/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_NonexistentId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/v1/events/{Guid.NewGuid()}", ValidEvent());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonexistentId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/events/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ThenEventAppearsInProfileGoalsWithId()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // Profile reads 404 without an athlete row; create one via onboarding required first.
        await SubmitRequiredAsync(client);
        var created = await CreateAsync(client, ValidEvent("Worlds"));

        var response = await client.GetAsync("/api/v1/profile/goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var goals = await response.Content.ReadFromJsonAsync<ProfileGoalsResponse>(JsonOptions);
        goals.Should().NotBeNull();
        goals!.Events.Should().ContainSingle(e => e.Id == created.Id && e.Name == "Worlds");
    }

    private static async Task<EventResponse> CreateAsync(HttpClient client, EventDto dto)
    {
        var response = await client.PostAsJsonAsync("/api/v1/events", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        body.Should().NotBeNull();
        return body!;
    }

    private static async Task SubmitRequiredAsync(HttpClient client)
    {
        var required = new OnboardingRequiredRequest
        {
            Name = "Test Athlete",
            Gender = Gender.Female,
            DateOfBirth = new DateOnly(1992, 6, 15),
            HeightCm = 170m,
            WeightKg = 65m,
            YearsTraining = 4,
            TypicalWeeklyHours = 9m,
            Methodology = MethodologyChoice.Polarized
        };
        (await client.PostAsJsonAsync("/api/v1/onboarding/required", required))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
