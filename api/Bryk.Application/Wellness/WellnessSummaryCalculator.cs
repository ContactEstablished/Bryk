using Bryk.Domain.Entities;

namespace Bryk.Application.Wellness;

/// <summary>
/// Pure summary math for <c>GET /wellness/summary</c> (ADR-0011 §2). No I/O and no clock read — the
/// caller passes the athlete's entries and <paramref name="today"/>, so the window edges, the averages
/// and the rounding are all deterministic and unit-tested directly, the same shape as
/// <see cref="Analytics.WeeklyLoadCalculator"/>.
///
/// Current window is <c>[today-6, today]</c> (7 days inclusive); the prior window is
/// <c>[today-13, today-7]</c> (7 days inclusive, non-overlapping). An average is taken over the days
/// that CARRY a value — a missing day is missing, never a zero — so a window with no values yields
/// <c>null</c>, not <c>0</c>.
/// </summary>
public static class WellnessSummaryCalculator
{
    public static WellnessSummaryResponse Compute(IReadOnlyList<DailyWellness> entries, DateOnly today)
    {
        var to = today;
        var from = today.AddDays(-6);
        var priorFrom = today.AddDays(-13);
        var priorTo = today.AddDays(-7);

        var current = entries.Where(e => e.Date >= from && e.Date <= to).ToList();
        var prior = entries.Where(e => e.Date >= priorFrom && e.Date <= priorTo).ToList();

        return new WellnessSummaryResponse
        {
            To = to,
            From = from,
            PriorFrom = priorFrom,
            // Integer metrics are cast to decimal before averaging — never integer-divided.
            SleepHours = Summarize(current, prior, e => e.SleepHours),
            SleepQuality = Summarize(current, prior, e => e.SleepQuality),
            RestingHr = Summarize(current, prior, e => e.RestingHr),
            WeightKg = Summarize(current, prior, e => e.WeightKg),
            Soreness = Summarize(current, prior, e => e.Soreness),
            HrvMs = Summarize(current, prior, e => e.HrvMs),
            // Sparse and ascending over the full 14 days; entries outside that span are ignored even if
            // the caller passes them.
            Days = entries
                .Where(e => e.Date >= priorFrom && e.Date <= to)
                .OrderBy(e => e.Date)
                .Select(e => new WellnessDailyPointDto
                {
                    Date = e.Date,
                    SleepHours = e.SleepHours,
                    SleepQuality = e.SleepQuality,
                    RestingHr = e.RestingHr,
                    WeightKg = e.WeightKg,
                    Soreness = e.Soreness,
                    HrvMs = e.HrvMs
                })
                .ToList(),
            // The caller loads exactly the 14-day window, so this answers "has this athlete logged
            // recently" — Task 20-4's Resting HR fallback is keyed on it.
            HasAnyEntries = entries.Count > 0
        };
    }

    private static WellnessMetricSummaryDto Summarize(
        IReadOnlyList<DailyWellness> current,
        IReadOnlyList<DailyWellness> prior,
        Func<DailyWellness, decimal?> select)
    {
        var currentValues = current.Select(select).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var priorValues = prior.Select(select).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        decimal? average = currentValues.Count > 0 ? Math.Round(currentValues.Average(), 2) : null;
        decimal? priorAverage = priorValues.Count > 0 ? Math.Round(priorValues.Average(), 2) : null;

        return new WellnessMetricSummaryDto
        {
            Average = average,
            PriorAverage = priorAverage,
            Delta = average.HasValue && priorAverage.HasValue
                ? Math.Round(average.Value - priorAverage.Value, 2)
                : null,
            DaysWithData = currentValues.Count
        };
    }
}
