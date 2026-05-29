using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Goals;
using Bryk.Application.Onboarding;
using Bryk.Application.Profile;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Goals;

public class GoalsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static GoalDto ValidGoal(string description = "Run a sub-3 marathon") => new()
    {
        Type = GoalType.General,
        Description = description,
        TargetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3)
    };

    [Fact]
    public async Task Create_ReturnsCreatedWithId()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/goals", ValidGoal());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GoalResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Description.Should().Be("Run a sub-3 marathon");
        body.Type.Should().Be(GoalType.General);
    }

    [Fact]
    public async Task Update_ReturnsOkWithUpdatedResource()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var created = await CreateAsync(client, ValidGoal("Original"));

        var response = await client.PutAsJsonAsync($"/api/v1/goals/{created.Id}", ValidGoal("Rewritten"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoalResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Description.Should().Be("Rewritten");
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var created = await CreateAsync(client, ValidGoal());

        var response = await client.DeleteAsync($"/api/v1/goals/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_NonexistentId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/v1/goals/{Guid.NewGuid()}", ValidGoal());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonexistentId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/goals/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ThenGoalAppearsInProfileGoalsWithId()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // Profile reads 404 without an athlete row; create one via onboarding required first.
        await SubmitRequiredAsync(client);
        var created = await CreateAsync(client, ValidGoal("Win nationals"));

        var response = await client.GetAsync("/api/v1/profile/goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var goals = await response.Content.ReadFromJsonAsync<ProfileGoalsResponse>(JsonOptions);
        goals.Should().NotBeNull();
        goals!.Goals.Should().ContainSingle(g => g.Id == created.Id && g.Description == "Win nationals");
    }

    private static async Task<GoalResponse> CreateAsync(HttpClient client, GoalDto dto)
    {
        var response = await client.PostAsJsonAsync("/api/v1/goals", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GoalResponse>(JsonOptions);
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
