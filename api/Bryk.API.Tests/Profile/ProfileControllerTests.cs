using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Onboarding;
using Bryk.Application.Profile;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Profile;

public class ProfileControllerTests
{
    // The API serializes enums as strings (JsonStringEnumConverter in Program.cs), so the
    // response reader needs the converter too — default web options would fail on enum fields.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GetRequired_NoAthlete_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/profile/required");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRequired_AfterSubmit_ReturnsSavedValues()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await SubmitRequiredAsync(client);

        var response = await client.GetAsync("/api/v1/profile/required");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ProfileRequiredResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be("Test Athlete");
        body.Gender.Should().Be(Gender.Female);
        body.DateOfBirth.Should().Be(new DateOnly(1992, 6, 15));
        body.HeightCm.Should().Be(170m);
        body.WeightKg.Should().Be(65m);
        body.YearsTraining.Should().Be(4);
        body.TypicalWeeklyHours.Should().Be(9m);
        body.Methodology.Should().Be(MethodologyChoice.Polarized);
    }

    [Fact]
    public async Task GetRecommended_AfterSubmit_ReturnsHrAndThresholds()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await SubmitRequiredAsync(client);

        var recommended = new OnboardingRecommendedRequest
        {
            RestingHr = 48,
            MaxHr = 190,
            SportThresholds = new List<SportThresholdsDto>
            {
                new()
                {
                    Sport = Sport.Bike,
                    IsActive = true,
                    ThresholdValue = 280m,
                    Lt1 = 200m,
                    Lt2 = 260m
                }
            }
        };
        (await client.PostAsJsonAsync("/api/v1/onboarding/recommended", recommended))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetAsync("/api/v1/profile/recommended");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ProfileRecommendedResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.RestingHr.Should().Be(48);
        body.MaxHr.Should().Be(190);
        body.SportThresholds.Should().ContainSingle();
        body.SportThresholds[0].Sport.Should().Be(Sport.Bike);
        body.SportThresholds[0].ThresholdValue.Should().Be(280m);
        body.SportThresholds[0].Lt1.Should().Be(200m);
        body.SportThresholds[0].Lt2.Should().Be(260m);
    }

    [Fact]
    public async Task GetGoals_AfterSubmit_ReturnsEventsAndGoals()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await SubmitRequiredAsync(client);

        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6);
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3);
        var goals = new OnboardingGoalsRequest
        {
            Events = new List<EventDto>
            {
                new()
                {
                    Name = "Spring Marathon",
                    EventDate = eventDate,
                    Sport = Sport.Run,
                    Priority = EventPriority.A,
                    Notes = "Goal race"
                }
            },
            Goals = new List<GoalDto>
            {
                new()
                {
                    Type = GoalType.General,
                    Description = "Run a sub-3 marathon",
                    TargetDate = targetDate
                }
            }
        };
        (await client.PostAsJsonAsync("/api/v1/onboarding/goals", goals))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetAsync("/api/v1/profile/goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ProfileGoalsResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Events.Should().ContainSingle();
        body.Events[0].Name.Should().Be("Spring Marathon");
        body.Events[0].EventDate.Should().Be(eventDate);
        body.Events[0].Sport.Should().Be(Sport.Run);
        body.Events[0].Priority.Should().Be(EventPriority.A);
        body.Goals.Should().ContainSingle();
        body.Goals[0].Type.Should().Be(GoalType.General);
        body.Goals[0].Description.Should().Be("Run a sub-3 marathon");
        body.Goals[0].TargetDate.Should().Be(targetDate);
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
