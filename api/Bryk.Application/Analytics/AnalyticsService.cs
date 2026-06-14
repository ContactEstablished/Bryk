using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Analytics;

public class AnalyticsService(
    ICurrentUserService currentUser,
    IValidator<AnalyticsRangeRequest> validator,
    IValidator<WeeklyLoadRequest> weeklyValidator,
    IWorkoutRepository workoutRepo,
    ITrainingPlanRepository planRepo,
    IAthleteRepository athleteRepo,
    IZoneService zoneService) : IAnalyticsService
{
    // Bounded warm-up before `from` so the EWMA is primed; 180 days ≫ the 42-day CTL constant (ADR-0006 §2).
    private const int LookbackDays = 180;

    private const int DefaultWeeks = 8;

    public async Task<IReadOnlyList<DailyLoadDto>> GetDailyLoadAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var build = await BuildSeriesAsync(from, to, ct);
        return Slice(build.Series, build.From, build.To);
    }

    public async Task<PmcResponse> GetPmcAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var build = await BuildSeriesAsync(from, to, ct);

        var pmc = PmcCalculator.Compute(build.Series);
        var pmcSeries = Slice(pmc, build.From, build.To);

        // current = the range's last day (= today for the dashboard's to=today call). Null for a fresh
        // athlete (no workout on/before `to`) so the tile renders "—" rather than a real-looking 0/0/0.
        PmcSummaryDto? current = null;
        if (build.FirstWorkoutDate is { } first && first <= build.To)
        {
            var last = pmc[^1]; // pmc covers [computeFrom, to]; its last element is `to`.
            current = new PmcSummaryDto
            {
                Date = last.Date,
                Ctl = last.Ctl,
                Atl = last.Atl,
                Tsb = last.Tsb,
                Acwr = AcwrCalculator.Compute(build.Series, build.To, build.FirstWorkoutDate)
            };
        }

        return new PmcResponse { Series = pmcSeries, Current = current };
    }

    public async Task<WeeklyLoadResponse> GetWeeklyLoadAsync(int? weeks, CancellationToken ct = default)
    {
        var w = weeks ?? DefaultWeeks;
        await weeklyValidator.ValidateOrThrowAsync(new WeeklyLoadRequest { Weeks = w }, ct);
        var athleteId = currentUser.GetCurrentAthleteId();

        // The N Monday-anchored ISO weeks ending with the current week (ThisWeekService's Monday math).
        var currentMonday = WeekStart(DateOnly.FromDateTime(DateTime.UtcNow));
        var weekStarts = Enumerable.Range(0, w)
            .Select(i => currentMonday.AddDays(-7 * (w - 1 - i))) // oldest → newest
            .ToList();
        var spanStart = weekStarts[0];
        var spanEnd = currentMonday.AddDays(6);

        // Planned: Σ EffectiveLoad of scheduled PlannedWorkouts per ISO week — the ThisWeekService
        // computation (structure + profiles + zones → LoadCalculator), generalised across the span.
        var planned = await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, spanStart, spanEnd, ct);
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

        // Actual: Σ EffectiveLoad of completed Workouts per ISO week (persisted LoadOverride ?? ComputedLoad).
        var completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, spanStart, spanEnd, ct);
        var actualByWeek = new Dictionary<DateOnly, decimal>();
        foreach (var workout in completed)
        {
            var weekStart = WeekStart(workout.CompletedDate);
            actualByWeek[weekStart] = actualByWeek.GetValueOrDefault(weekStart, 0m) + (workout.LoadOverride ?? workout.ComputedLoad ?? 0m);
        }

        var actualLoads = weekStarts.Select(ws => Math.Round(actualByWeek.GetValueOrDefault(ws, 0m), 2)).ToList();
        var (rollingAverages, band) = WeeklyLoadCalculator.Compute(actualLoads);

        var weeksDto = weekStarts.Select((ws, i) => new WeeklyLoadWeekDto
        {
            WeekStart = ws,
            PlannedLoad = Math.Round(plannedByWeek.GetValueOrDefault(ws, 0m), 2),
            ActualLoad = actualLoads[i],
            RollingAverage = rollingAverages[i]
        }).ToList();

        return new WeeklyLoadResponse { Weeks = weeksDto, OptimalBand = band };
    }

    public async Task<PeaksResponse> GetPeaksAsync(Sport? sport, CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var workouts = await workoutRepo.GetByAthleteWithStepResultsAsync(athleteId, sport, ct);

        var summaries = workouts.Select(ToSummary).ToList();
        var records = PeaksCalculator.Compute(summaries, DateOnly.FromDateTime(DateTime.UtcNow));

        return new PeaksResponse { Records = records };
    }

    // Reduce a completed Workout to its peak-relevant figures. Pace is the honest session avg (distance ÷
    // time); power is the duration-weighted mean of the captured per-step powers (the only session power).
    private static PeakWorkoutSummary ToSummary(Workout workout)
    {
        decimal? avgPace = null;
        if ((workout.Sport == Sport.Run || workout.Sport == Sport.Swim)
            && workout.ActualDurationSeconds is { } dur && dur > 0
            && workout.ActualDistanceMeters is { } dist && dist > 0)
        {
            var unitMeters = workout.Sport == Sport.Run ? 1000m : 100m; // sec per km (run) / per 100 m (swim)
            avgPace = dur / (dist / unitMeters);
        }

        decimal? avgPower = null;
        if (workout.Sport == Sport.Bike)
        {
            var powered = workout.StepResults
                .Where(r => r.AvgPower is > 0 && r.ActualDurationSeconds is > 0)
                .ToList();
            var totalDuration = powered.Sum(r => r.ActualDurationSeconds!.Value);
            if (totalDuration > 0)
            {
                avgPower = powered.Sum(r => r.AvgPower!.Value * (decimal)r.ActualDurationSeconds!.Value) / totalDuration;
            }
        }

        return new PeakWorkoutSummary
        {
            WorkoutId = workout.Id,
            Date = workout.CompletedDate,
            Sport = workout.Sport,
            Load = workout.LoadOverride ?? workout.ComputedLoad,
            DurationSeconds = workout.ActualDurationSeconds,
            DistanceMeters = workout.ActualDistanceMeters,
            AvgPaceSecPerUnit = avgPace,
            AvgPowerWatts = avgPower
        };
    }

    // Monday-based ISO week start for a date — the same math as ThisWeekService.CurrentWeek.
    // ((int)DayOfWeek + 6) % 7 maps Mon→0 … Sun→6, so subtracting it lands on Monday.
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    // Validate the range, resolve the athlete, read the bounded window, group by date, and materialise the
    // contiguous zero-filled series over [computeFrom, to]. Returns the resolved bounds + first-workout date.
    private async Task<(DateOnly From, DateOnly To, DateOnly? FirstWorkoutDate, IReadOnlyList<DailyLoadDto> Series)>
        BuildSeriesAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var request = new AnalyticsRangeRequest { From = from, To = to };
        await validator.ValidateOrThrowAsync(request, ct);

        // Non-null after validation.
        var fromDate = from!.Value;
        var toDate = to!.Value;
        var athleteId = currentUser.GetCurrentAthleteId();

        var firstWorkoutDate = await workoutRepo.GetFirstWorkoutDateAsync(athleteId, ct);
        var computeFrom = ComputeFrom(fromDate, firstWorkoutDate);

        var workouts = await workoutRepo.GetByAthleteInRangeAsync(athleteId, computeFrom, toDate, ct);

        // Σ EffectiveLoad (LoadOverride ?? ComputedLoad) per CompletedDate; days with none stay 0.
        var byDate = workouts
            .GroupBy(w => w.CompletedDate)
            .ToDictionary(g => g.Key, g => g.Sum(w => w.LoadOverride ?? w.ComputedLoad ?? 0m));

        var series = new List<DailyLoadDto>(toDate.DayNumber - computeFrom.DayNumber + 1);
        for (var d = computeFrom; d <= toDate; d = d.AddDays(1))
        {
            series.Add(new DailyLoadDto { Date = d, Load = byDate.GetValueOrDefault(d, 0m) });
        }

        return (fromDate, toDate, firstWorkoutDate, series);
    }

    // ADR-0006 §2: max( min(firstWorkout, from), from − 180 ). With no history, just `from`.
    private static DateOnly ComputeFrom(DateOnly from, DateOnly? firstWorkoutDate)
    {
        if (firstWorkoutDate is not { } first)
        {
            return from;
        }

        var earliestUseful = first < from ? first : from;            // min(first, from)
        var lookbackFloor = from.AddDays(-LookbackDays);             // from − 180
        return earliestUseful > lookbackFloor ? earliestUseful : lookbackFloor; // max(…)
    }

    private static IReadOnlyList<DailyLoadDto> Slice(IReadOnlyList<DailyLoadDto> series, DateOnly from, DateOnly to) =>
        series.Where(p => p.Date >= from && p.Date <= to).ToList();

    private static IReadOnlyList<PmcPointDto> Slice(IReadOnlyList<PmcPointDto> series, DateOnly from, DateOnly to) =>
        series.Where(p => p.Date >= from && p.Date <= to).ToList();
}
