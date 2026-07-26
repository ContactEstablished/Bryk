using Bryk.Application.Common;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Interfaces;

namespace Bryk.Application.Training.Periodization;

public class PeriodizationService(
    ICurrentUserService currentUser,
    ITrainingPlanRepository planRepo,
    IWorkoutRepository workoutRepo,
    IEventRepository eventRepo,
    IAthleteRepository athleteRepo,
    IZoneService zoneService) : IPeriodizationService
{
    // Trailing baseline window (ADR-0009 §1): exactly 4 ISO weeks ending the day before the plan's
    // first week, fixed divisor — empty weeks are load-bearing zeros, never skipped.
    private const int TrailingWindowDays = 28;
    private const int TrailingWeekDivisor = 4;

    public async Task<WeeklyTargetsResponse> GetWeeklyTargetsAsync(Guid planId, CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var plan = await planRepo.GetByIdAsync(planId, ct);
        if (plan is null || plan.AthleteId != athleteId)
        {
            throw new KeyNotFoundException();
        }

        var firstWeekStart = WeekStart(plan.StartDate);
        var lastWeekEnd = WeekStart(plan.EndDate).AddDays(6);

        // Planned per week — THIS plan only. GetPlannedWorkoutsInRangeWithStructureAsync is athlete-wide
        // across all of the athlete's plans, so filter on TrainingPlanId or another plan's sessions leak
        // into this plan's weeks. Aggregation shape lifted verbatim from AnalyticsService.GetWeeklyLoadAsync.
        var planned = (await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, firstWeekStart, lastWeekEnd, ct))
            .Where(pw => pw.TrainingPlanId == plan.Id)
            .ToList();
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var zones = await zoneService.GetZonesAsync(ct);

        var plannedByWeek = new Dictionary<DateOnly, decimal>();
        foreach (var pw in planned)
        {
            var profile = athlete?.SportProfiles.FirstOrDefault(p => p.Sport == pw.Sport);
            var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == pw.Sport);
            var effective = pw.PlannedLoad ?? LoadCalculator.ComputePlannedLoad(pw, profile, sportZones) ?? 0m;
            var weekStart = WeekStart(pw.ScheduledDate);
            plannedByWeek[weekStart] = plannedByWeek.GetValueOrDefault(weekStart, 0m) + effective;
        }

        // Actual per week — athlete-wide by design (a completed Workout carries no plan attribution;
        // see the comment on WeeklyTargetWeekDto). Same aggregation shape as AnalyticsService.
        var actuals = await workoutRepo.GetByAthleteInRangeAsync(athleteId, firstWeekStart, lastWeekEnd, ct);
        var actualByWeek = new Dictionary<DateOnly, decimal>();
        foreach (var w in actuals)
        {
            var weekStart = WeekStart(w.CompletedDate);
            actualByWeek[weekStart] = actualByWeek.GetValueOrDefault(weekStart, 0m) + (w.LoadOverride ?? w.ComputedLoad ?? 0m);
        }

        // Baseline (ADR-0009 §1), anchored on the plan's FIRST WEEK — never on today. Anchoring on the
        // plan start keeps the target series stable for the plan's whole life; a today-anchored baseline
        // would silently reshape every target every Monday.
        var trailingStart = firstWeekStart.AddDays(-TrailingWindowDays);
        var trailingEnd = firstWeekStart.AddDays(-1);
        var trailingCompleted = await workoutRepo.GetByAthleteInRangeAsync(athleteId, trailingStart, trailingEnd, ct);
        var trailingMean = Math.Round(trailingCompleted.Sum(w => w.LoadOverride ?? w.ComputedLoad ?? 0m) / TrailingWeekDivisor, 2);

        decimal? baseline;
        TargetBaselineSource baselineSource;
        if (trailingMean > 0m)
        {
            baseline = trailingMean;
            baselineSource = TargetBaselineSource.TrailingActual;
        }
        else
        {
            var firstWeekPlanned = plannedByWeek.GetValueOrDefault(firstWeekStart, 0m);
            if (firstWeekPlanned > 0m)
            {
                baseline = firstWeekPlanned;
                baselineSource = TargetBaselineSource.FirstWeekPlanned;
            }
            else
            {
                baseline = null;
                baselineSource = TargetBaselineSource.None;
            }
        }

        // Linked event date — defensive ownership check (the FK is SetNull and 18-2 already validates
        // ownership on write; this guards a stale/foreign EventId some other path might produce).
        DateOnly? eventDate = null;
        if (plan.EventId is { } eventId)
        {
            var ev = await eventRepo.GetByIdAsync(eventId, ct);
            if (ev is not null && ev.AthleteId == athleteId)
            {
                eventDate = ev.EventDate;
            }
        }

        var targets = WeeklyTargetCalculator.Compute(new WeeklyTargetInput(
            plan.StartDate, plan.EndDate, baseline, plan.BuildWeeks, plan.RecoveryWeeks, plan.RecoveryWeekPercentage, eventDate));

        var weeks = targets.Select(t => new WeeklyTargetWeekDto
        {
            WeekStart = t.WeekStart,
            TargetLoad = t.TargetLoad,
            IsRecoveryWeek = t.IsRecoveryWeek,
            IsTaperWeek = t.IsTaperWeek,
            PlannedLoad = Math.Round(plannedByWeek.GetValueOrDefault(t.WeekStart, 0m), 2),
            ActualLoad = Math.Round(actualByWeek.GetValueOrDefault(t.WeekStart, 0m), 2)
        }).ToList();

        return new WeeklyTargetsResponse
        {
            PlanId = plan.Id,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            Baseline = baseline,
            BaselineSource = baselineSource,
            Weeks = weeks
        };
    }

    // Monday-anchored ISO week start — same expression as AnalyticsService.cs:186 / ThisWeekService.cs:44.
    // Duplicated deliberately (ADR-0009's stated convention); not refactored into a shared helper.
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
