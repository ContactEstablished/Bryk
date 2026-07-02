using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Goals;
using Bryk.Application.Onboarding;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Goals;

public class GoalsControllerGetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static GoalDto MakeGoal(string description, DateOnly? targetDate) => new()
    {
        Type = GoalType.General,
        Description = description,
        TargetDate = targetDate
    };

    // Direct DbContext seed — GoalDtoValidator forbids past TargetDate, so an overdue goal can only be
    // set up below the endpoint. Shares the factory's InMemory store with the HTTP request pipeline.
    private static async Task SeedGoalDirectAsync(
        BrykWebApplicationFactory factory, Guid athleteId, string description, DateOnly? targetDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Goals.Add(new Goal
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            Type = GoalType.General,
            Description = description,
            TargetDate = targetDate
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAll_DueSoonGoal_ReturnsThreeDaysRemainingAndDueSoon()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/goals", MakeGoal("Due soon goal", Today.AddDays(3)));

        var goals = await client.GetFromJsonAsync<List<GoalListItemResponse>>("/api/v1/goals", JsonOptions);

        goals.Should().ContainSingle();
        goals![0].DaysRemaining.Should().Be(3);
        goals[0].Status.Should().Be(GoalStatus.DueSoon);
    }

    [Fact]
    public async Task GetAll_NullTargetGoal_ReturnsNullDaysRemainingAndNoDate()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/goals", MakeGoal("No date goal", null));

        var goals = await client.GetFromJsonAsync<List<GoalListItemResponse>>("/api/v1/goals", JsonOptions);

        goals.Should().ContainSingle();
        goals![0].DaysRemaining.Should().BeNull();
        goals[0].Status.Should().Be(GoalStatus.NoDate);
    }

    [Fact]
    public async Task GetAll_PastTargetGoal_ReturnsOverdue()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await SeedGoalDirectAsync(factory, BrykWebApplicationFactory.TestAthleteId, "Overdue goal", Today.AddDays(-10));

        var goals = await client.GetFromJsonAsync<List<GoalListItemResponse>>("/api/v1/goals", JsonOptions);

        goals.Should().ContainSingle();
        goals![0].DaysRemaining.Should().Be(-10);
        goals[0].Status.Should().Be(GoalStatus.Overdue);
    }

    [Fact]
    public async Task GetAll_FreshAthlete_ReturnsEmptyArray()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/goals");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var goals = await response.Content.ReadFromJsonAsync<List<GoalListItemResponse>>(JsonOptions);
        goals.Should().NotBeNull().And.BeEmpty();
    }
}
