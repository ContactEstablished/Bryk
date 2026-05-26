using System.Net;
using System.Net.Http.Json;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Onboarding;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Onboarding;

public class OnboardingControllerTests : IClassFixture<BrykWebApplicationFactory>
{
    private readonly BrykWebApplicationFactory _factory;

    public OnboardingControllerTests(BrykWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStatus_FreshAthlete_ReturnsAllFlagsFalse()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/onboarding/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<OnboardingStatusResponse>();
        status.Should().NotBeNull();
        status!.RequiredComplete.Should().BeFalse();
        status.RecommendedComplete.Should().BeFalse();
        status.GoalsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task PostRequired_HappyPath_FlipsRequiredCompleteTrue()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new OnboardingRequiredRequest
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

        var postResponse = await client.PostAsJsonAsync("/api/v1/onboarding/required", request);
        postResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var statusResponse = await client.GetAsync("/api/v1/onboarding/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await statusResponse.Content.ReadFromJsonAsync<OnboardingStatusResponse>();
        status.Should().NotBeNull();
        status!.RequiredComplete.Should().BeTrue();
        status.RecommendedComplete.Should().BeFalse();
        status.GoalsComplete.Should().BeFalse();
    }
}
