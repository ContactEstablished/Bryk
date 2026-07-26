using Bryk.Application.Common;
using Bryk.Application.Training.Load;
using Bryk.Application.Training.Periodization;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;

namespace Bryk.Application.Training;

public class ThisWeekService(
    ICurrentUserService currentUser,
    ITrainingPlanRepository planRepo,
    IAthleteRepository athleteRepo,
    IZoneService zoneService,
    IWorkoutRepository workoutRepo,
    IPeriodizationService periodization) : IThisWeekService
{
    public async Task<ThisWeekResponse> GetThisWeekAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (weekStart, weekEnd) = CurrentWeek(today);

        // Load the week's workouts WITH structure, plus the athlete's profiles + effective zones once,
        // so the per-workout load computation is a single set of round-trips (ADR-0005 §3 read-cost note).
        var workouts = await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, weekStart, weekEnd, ct);
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var zones = await zoneService.GetZonesAsync(ct);

        var planned = workouts.Select(w => Map(w, athlete, zones)).ToList();
        var weeklyLoad = Math.Round(planned.Sum(p => p.EffectiveLoad ?? 0m), 2);

        // Actual load — the same aggregation AnalyticsService uses, narrowed to this one week.
        var completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, weekStart, weekEnd, ct);
        var actualLoad = Math.Round(completed.Sum(w => w.LoadOverride ?? w.ComputedLoad ?? 0m), 2);

        // Active plan = the one whose window contains today; an overlap is broken by the most recently
        // begun plan. No such plan → no target (never faked from the planned sum).
        var plans = await planRepo.GetByAthleteIdAsync(athleteId, ct);
        var active = plans
            .Where(p => p.StartDate <= today && today <= p.EndDate)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefault();

        decimal? targetLoad = null;
        if (active is not null)
        {
            // Reusing IPeriodizationService costs this dashboard call an extra plan + workout read.
            // Accepted for v1: duplicating the ramp math here would give it two homes.
            var targets = await periodization.GetWeeklyTargetsAsync(active.Id, ct);
            targetLoad = targets.Weeks.FirstOrDefault(w => w.WeekStart == weekStart)?.TargetLoad;
        }

        return new ThisWeekResponse
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            WeeklyLoad = weeklyLoad,
            TargetLoad = targetLoad,
            ActualLoad = actualLoad,
            PlannedWorkouts = planned
        };
    }

    // Monday-based week in UTC, matching how the domain treats DateOnly elsewhere
    // (e.g. EventDtoValidator uses DateOnly.FromDateTime(DateTime.UtcNow) as "today").
    // ((int)DayOfWeek + 6) % 7 maps Mon→0 … Sun→6, so subtracting it lands on Monday.
    private static (DateOnly Start, DateOnly End) CurrentWeek(DateOnly today)
    {
        var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (start, start.AddDays(6));
    }

    private static PlannedWorkoutResponse Map(PlannedWorkout pw, Athlete? athlete, ZonesResponse zones)
    {
        var profile = athlete?.SportProfiles.FirstOrDefault(p => p.Sport == pw.Sport);
        var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == pw.Sport);
        var computed = LoadCalculator.ComputePlannedLoad(pw, profile, sportZones);

        return new PlannedWorkoutResponse
        {
            Id = pw.Id,
            TrainingPlanId = pw.TrainingPlanId,
            Sport = pw.Sport,
            ScheduledDate = pw.ScheduledDate,
            Title = pw.Title,
            Description = pw.Description,
            PlannedDurationMinutes = pw.PlannedDurationMinutes,
            PlannedLoad = pw.PlannedLoad,
            ComputedLoad = computed,
            IsLoadOverride = pw.PlannedLoad is not null,
            EffectiveLoad = pw.PlannedLoad ?? computed
            // Blocks intentionally omitted — This Week shows the load number, not the structure.
        };
    }
}
