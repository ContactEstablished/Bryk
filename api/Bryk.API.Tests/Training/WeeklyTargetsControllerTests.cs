using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Training;
using Bryk.Application.Training.Periodization;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Training;

public class WeeklyTargetsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // Same Monday-anchor expression as AnalyticsService.cs:186 / PeriodizationService — duplicated
    // locally per the codebase's established convention for this expression.
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static TrainingPlanRequest ValidPlan(string name, DateOnly start, DateOnly end) => new()
    {
        Name = name,
        Methodology = MethodologyChoice.Polarized,
        StartDate = start,
        EndDate = end
    };

    private static LogWorkoutRequest Completion(DateOnly completedDate, decimal loadOverride) => new()
    {
        Sport = Sport.Run,
        CompletedDate = completedDate,
        LoadOverride = loadOverride
    };

    [Fact]
    public async Task WeeklyTargets_MissingPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/trainingplans/{Guid.NewGuid()}/weekly-targets");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WeeklyTargets_ForeignPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();

        var foreignAthleteId = Guid.NewGuid();
        var foreignPlanId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = foreignPlanId,
                AthleteId = foreignAthleteId,
                Name = "Foreign Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = Today,
                EndDate = Today.AddDays(27)
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
        var response = await client.GetAsync($"/api/v1/trainingplans/{foreignPlanId}/weekly-targets");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WeeklyTargets_FreshAthlete_Returns200WithNoTargets()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Fresh Plan", Today, Today.AddDays(27)));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/trainingplans/{created!.Id}/weekly-targets");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.BaselineSource.Should().Be(TargetBaselineSource.None);
        body.Baseline.Should().BeNull();
        body.Weeks.Should().BeEmpty();
    }

    [Fact]
    public async Task WeeklyTargets_WithTrailingActuals_ReturnsRampingTargets()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var firstWeekStart = WeekStart(Today);
        var planEnd = firstWeekStart.AddDays(27); // exactly 4 ISO weeks, whatever weekday "today" is
        foreach (var offsetDays in new[] { -28, -21, -14, -1 })
        {
            await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart.AddDays(offsetDays), 200m));
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Ramp Plan", Today, planEnd));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/trainingplans/{created!.Id}/weekly-targets");
        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);

        body!.BaselineSource.Should().Be(TargetBaselineSource.TrailingActual);
        body.Baseline.Should().Be(200.00m);
        body.Weeks.Should().HaveCount(4);
        body.Weeks.Select(w => w.TargetLoad).Should().BeInAscendingOrder();
        body.Weeks[0].TargetLoad.Should().Be(body.Baseline);
    }

    [Fact]
    public async Task WeeklyTargets_MergesTheAthletesActualLoad()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var firstWeekStart = WeekStart(Today);
        var planEnd = firstWeekStart.AddDays(27);
        foreach (var offsetDays in new[] { -28, -21, -14, -1 })
        {
            await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart.AddDays(offsetDays), 200m));
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Merge Plan", Today, planEnd));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart, 75m));

        var response = await client.GetAsync($"/api/v1/trainingplans/{created!.Id}/weekly-targets");
        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);

        body!.Weeks[0].ActualLoad.Should().Be(75.00m);
    }

    [Fact]
    public async Task WeeklyTargets_AfterPlanPutSetsCadence_TheDipAppears()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var firstWeekStart = WeekStart(Today);
        var planEnd = firstWeekStart.AddDays(27);
        foreach (var offsetDays in new[] { -28, -21, -14, -1 })
        {
            await client.PostAsJsonAsync("/api/v1/workouts", Completion(firstWeekStart.AddDays(offsetDays), 200m));
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Cadence Plan", Today, planEnd));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var putBody = new TrainingPlanUpdateRequest
        {
            Name = created!.Name,
            Methodology = created.Methodology,
            StartDate = created.StartDate,
            EndDate = created.EndDate,
            BuildWeeks = 3,
            RecoveryWeeks = 1,
            RecoveryWeekPercentage = 60.0m
        };
        var putResponse = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", putBody);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/v1/trainingplans/{created.Id}/weekly-targets");
        var body = await response.Content.ReadFromJsonAsync<WeeklyTargetsResponse>(JsonOptions);

        body!.Weeks.Should().HaveCount(4);
        body.Weeks[3].IsRecoveryWeek.Should().BeTrue();
        body.Weeks[3].TargetLoad.Should().BeLessThan(body.Weeks[2].TargetLoad);
    }
}
