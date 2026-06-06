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

public class TrainingPlansControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static TrainingPlanRequest ValidPlan(string name = "Base Block") => new()
    {
        Name = name,
        Methodology = MethodologyChoice.Polarized,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(28),
        PlannedWorkouts = new List<PlannedWorkoutDto>
        {
            new()
            {
                Sport = Sport.Run,
                ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
                Title = "Easy Run",
                PlannedDurationMinutes = 45
            },
            new()
            {
                Sport = Sport.Bike,
                ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2),
                Title = "Endurance Ride",
                PlannedDurationMinutes = 120,
                PlannedLoad = 80m
            }
        }
    };

    [Fact]
    public async Task Create_ThenGetById_ReturnsPlanWithPlannedWorkouts()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Spring Base"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Name.Should().Be("Spring Base");

        var getResponse = await client.GetAsync($"/api/v1/trainingplans/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await getResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        plan.Should().NotBeNull();
        plan!.Id.Should().Be(created.Id);
        plan.Methodology.Should().Be(MethodologyChoice.Polarized);
        plan.PlannedWorkouts.Should().HaveCount(2);
        plan.PlannedWorkouts.Select(pw => pw.Title).Should().Contain(new[] { "Easy Run", "Endurance Ride" });
        plan.PlannedWorkouts.Should().OnlyContain(pw => pw.Id != Guid.Empty && pw.TrainingPlanId == created.Id);
    }

    [Fact]
    public async Task GetById_NonexistentId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/trainingplans/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
