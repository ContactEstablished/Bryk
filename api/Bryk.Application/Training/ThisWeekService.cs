using Bryk.Application.Common;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;

namespace Bryk.Application.Training;

public class ThisWeekService(
    ICurrentUserService currentUser,
    ITrainingPlanRepository planRepo) : IThisWeekService
{
    public async Task<ThisWeekResponse> GetThisWeekAsync(CancellationToken ct = default)
    {
        var (weekStart, weekEnd) = CurrentWeek();

        var workouts = await planRepo.GetPlannedWorkoutsInRangeAsync(
            currentUser.GetCurrentAthleteId(), weekStart, weekEnd, ct);

        return new ThisWeekResponse
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            PlannedWorkouts = workouts.Select(Map).ToList()
        };
    }

    // Monday-based week in UTC, matching how the domain treats DateOnly elsewhere
    // (e.g. EventDtoValidator uses DateOnly.FromDateTime(DateTime.UtcNow) as "today").
    // ((int)DayOfWeek + 6) % 7 maps Mon→0 … Sun→6, so subtracting it lands on Monday.
    private static (DateOnly Start, DateOnly End) CurrentWeek()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (start, start.AddDays(6));
    }

    private static PlannedWorkoutResponse Map(PlannedWorkout pw) => new()
    {
        Id = pw.Id,
        TrainingPlanId = pw.TrainingPlanId,
        Sport = pw.Sport,
        ScheduledDate = pw.ScheduledDate,
        Title = pw.Title,
        Description = pw.Description,
        PlannedDurationMinutes = pw.PlannedDurationMinutes,
        PlannedLoad = pw.PlannedLoad
    };
}
