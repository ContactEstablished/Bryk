using Bryk.Domain.Entities;

namespace Bryk.Application.Analytics;

// One completed workout reduced to its peak-relevant session figures (ADR-0007 §2). The service derives
// these from the Workout (+ step results for bike power) so the calculator stays pure; missing figures are
// null and simply don't qualify for their record.
public class PeakWorkoutSummary
{
    public Guid WorkoutId { get; set; }
    public DateOnly Date { get; set; }
    public Sport Sport { get; set; }
    public decimal? Load { get; set; }              // EffectiveLoad (TSS)
    public int? DurationSeconds { get; set; }
    public int? DistanceMeters { get; set; }
    public decimal? AvgPaceSecPerUnit { get; set; } // run = /km, swim = /100 m (lower is faster)
    public decimal? AvgPowerWatts { get; set; }     // bike, duration-weighted session mean
}

/// <summary>
/// Pure session-level peaks (ADR-0007 §2). No I/O — the caller passes per-workout summaries + "today" (the
/// 90-day recency anchor; calculators never read the clock). Emits the best record per kind that has any
/// qualifying sample, with the second-best as <c>PreviousValue</c> for an honest improvement delta.
/// </summary>
public static class PeaksCalculator
{
    private const int RecentDays = 90;

    public static IReadOnlyList<PeakRecordDto> Compute(IReadOnlyList<PeakWorkoutSummary> summaries, DateOnly today)
    {
        var records = new List<PeakRecordDto>();

        AddIfAny(records, Select(PeakKind.Load, Samples(summaries, s => s.Load), higherIsBetter: true, today));
        AddIfAny(records, Select(PeakKind.Duration, Samples(summaries, s => s.DurationSeconds), higherIsBetter: true, today));
        AddIfAny(records, Select(PeakKind.Distance, Samples(summaries, s => s.DistanceMeters), higherIsBetter: true, today));
        AddIfAny(records, Select(PeakKind.Pace, Samples(summaries, s => s.AvgPaceSecPerUnit), higherIsBetter: false, today));
        AddIfAny(records, Select(PeakKind.Power, Samples(summaries, s => s.AvgPowerWatts), higherIsBetter: true, today));

        return records;
    }

    private static void AddIfAny(List<PeakRecordDto> records, PeakRecordDto? record)
    {
        if (record is not null)
        {
            records.Add(record);
        }
    }

    // Workouts whose metric is present and positive, projected to (summary, value).
    private static List<(PeakWorkoutSummary S, decimal V)> Samples(
        IEnumerable<PeakWorkoutSummary> summaries, Func<PeakWorkoutSummary, decimal?> selector)
        => summaries
            .Select(s => (S: s, V: selector(s)))
            .Where(x => x.V is > 0m)
            .Select(x => (x.S, x.V!.Value))
            .ToList();

    private static PeakRecordDto? Select(PeakKind kind, List<(PeakWorkoutSummary S, decimal V)> samples, bool higherIsBetter, DateOnly today)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        samples.Sort((a, b) => higherIsBetter ? b.V.CompareTo(a.V) : a.V.CompareTo(b.V));
        var best = samples[0];

        decimal? previous = null;
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i].V != best.V)
            {
                previous = Math.Round(samples[i].V, 2);
                break;
            }
        }

        return new PeakRecordDto
        {
            Kind = kind,
            Sport = best.S.Sport,
            Value = Math.Round(best.V, 2),
            AchievedDate = best.S.Date,
            AchievedWorkoutId = best.S.WorkoutId,
            IsRecent = best.S.Date >= today.AddDays(-(RecentDays - 1)),
            PreviousValue = previous
        };
    }
}
