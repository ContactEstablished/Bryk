using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Calendar;
using Bryk.Application.Training;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Training;

public class TrainingPlansControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class ApiError
    {
        public int Status { get; set; }
        public string? Error { get; set; }
        public string[]? Errors { get; set; }
    }

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

    [Fact]
    public async Task Reschedule_InWindow_Returns204AndMovesDate()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // Seed a plan with a planned workout in the middle of the window.
        var planRequest = ValidPlan("Reschedule Test");
        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", planRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        created.Should().NotBeNull();
        var pw = created!.PlannedWorkouts.First();
        var newDate = created.StartDate.AddDays(7);

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/v1/trainingplans/{created.Id}/plannedworkouts/{pw.Id}/schedule",
            new ScheduleRequest { ScheduledDate = newDate });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify via GET.
        var getResponse = await client.GetAsync($"/api/v1/trainingplans/{created.Id}");
        var plan = await getResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        plan!.PlannedWorkouts.Should().Contain(p => p.Id == pw.Id && p.ScheduledDate == newDate);
    }

    [Fact]
    public async Task Reschedule_BelowWindow_Returns400WithScheduledDateError()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var planRequest = ValidPlan("Below Window Test");
        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", planRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        var pw = created!.PlannedWorkouts.First();
        var beforeStart = created.StartDate.AddDays(-1);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/trainingplans/{created.Id}/plannedworkouts/{pw.Id}/schedule",
            new ScheduleRequest { ScheduledDate = beforeStart });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorBody = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        errorBody.Should().NotBeNull();
        errorBody!.Errors.Should().Contain(e => e.StartsWith("ScheduledDate:"));
    }

    [Fact]
    public async Task Reschedule_AboveWindow_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var planRequest = ValidPlan("Above Window Test");
        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", planRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        var pw = created!.PlannedWorkouts.First();
        var afterEnd = created.EndDate.AddDays(1);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/trainingplans/{created.Id}/plannedworkouts/{pw.Id}/schedule",
            new ScheduleRequest { ScheduledDate = afterEnd });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorBody = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        errorBody.Should().NotBeNull();
        errorBody!.Errors.Should().Contain(e => e.StartsWith("ScheduledDate:"));
    }

    [Fact]
    public async Task Reschedule_MissingPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/trainingplans/{Guid.NewGuid()}/plannedworkouts/{Guid.NewGuid()}/schedule",
            new ScheduleRequest { ScheduledDate = new DateOnly(2026, 6, 15) });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reschedule_ForeignPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();

        // Seed a plan owned by a different athlete directly through the DbContext.
        var foreignAthleteId = Guid.NewGuid();
        var foreignPlanId = Guid.NewGuid();
        var foreignPwId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = foreignPlanId,
                AthleteId = foreignAthleteId,
                Name = "Foreign Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 30),
                PlannedWorkouts = new List<PlannedWorkout>
                {
                    new()
                    {
                        Id = foreignPwId,
                        AthleteId = foreignAthleteId,
                        TrainingPlanId = foreignPlanId,
                        Sport = Sport.Run,
                        ScheduledDate = new DateOnly(2026, 6, 15),
                        Title = "Foreign Workout",
                        PlannedDurationMinutes = 60,
                        PlannedLoad = 50m
                    }
                }
            });
            db.Athletes.Add(new Athlete
            {
                Id = foreignAthleteId,
                Name = "Foreign Athlete",
                Gender = Gender.Male,
                DateOfBirth = new DateOnly(1990, 1, 1),
                HeightCm = 180,
                WeightKg = 75,
                TypicalWeeklyHours = 10,
                Methodology = MethodologyChoice.Polarized
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/trainingplans/{foreignPlanId}/plannedworkouts/{foreignPwId}/schedule",
            new ScheduleRequest { ScheduledDate = new DateOnly(2026, 6, 20) });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reschedule_MissingPlannedWorkout_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var planRequest = ValidPlan("Missing PW Test");
        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", planRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/trainingplans/{created!.Id}/plannedworkouts/{Guid.NewGuid()}/schedule",
            new ScheduleRequest { ScheduledDate = created.StartDate.AddDays(1) });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
