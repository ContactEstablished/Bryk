using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Analytics;

public class AnalyticsService(
    ICurrentUserService currentUser,
    IValidator<AnalyticsRangeRequest> validator,
    IWorkoutRepository workoutRepo) : IAnalyticsService
{
    // Bounded warm-up before `from` so the EWMA is primed; 180 days ≫ the 42-day CTL constant (ADR-0006 §2).
    private const int LookbackDays = 180;

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
