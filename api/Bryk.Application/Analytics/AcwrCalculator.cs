namespace Bryk.Application.Analytics;

/// <summary>
/// Pure Acute:Chronic Workload Ratio (ADR-0006 §5): 7-day acute mean ÷ 28-day chronic mean of daily load.
/// Returns null under 28 days of history (honesty rule — never a fabricated ratio) or a zero chronic mean.
/// No I/O; consumes the same zero-filled series <see cref="PmcCalculator"/> does.
/// </summary>
public static class AcwrCalculator
{
    private const int AcuteDays = 7;
    private const int ChronicDays = 28;

    /// <summary>
    /// ACWR at <paramref name="evaluationDay"/> from <paramref name="series"/>. Null when the athlete has
    /// fewer than 28 calendar days of history through the evaluation day (or no history at all,
    /// <paramref name="firstWorkoutDate"/> null), or when the 28-day chronic mean is 0 (no 0/0).
    /// </summary>
    public static decimal? Compute(IReadOnlyList<DailyLoadDto> series, DateOnly evaluationDay, DateOnly? firstWorkoutDate)
    {
        if (firstWorkoutDate is not { } first)
        {
            return null;
        }

        // Fewer than 28 calendar days of history through the evaluation day → not enough for a chronic window.
        if (evaluationDay.DayNumber - first.DayNumber + 1 < ChronicDays)
        {
            return null;
        }

        var acute = WindowMean(series, evaluationDay, AcuteDays);
        var chronic = WindowMean(series, evaluationDay, ChronicDays);

        if (chronic <= 0m)
        {
            return null;
        }

        return Math.Round(acute / chronic, 2);
    }

    // Mean daily load over [evaluationDay − (days − 1), evaluationDay]. A day absent from the series counts
    // as 0 (defensive; the caller's window covers this span whenever ACWR is sufficient).
    private static decimal WindowMean(IReadOnlyList<DailyLoadDto> series, DateOnly evaluationDay, int days)
    {
        var startDayNumber = evaluationDay.DayNumber - (days - 1);
        var endDayNumber = evaluationDay.DayNumber;

        decimal sum = 0m;
        foreach (var d in series)
        {
            var n = d.Date.DayNumber;
            if (n >= startDayNumber && n <= endDayNumber)
            {
                sum += d.Load;
            }
        }

        return sum / days;
    }
}
