namespace Bryk.Application.Analytics;

/// <summary>
/// Pure Performance-Management-Chart math (ADR-0006 §4, ROADMAP math conventions). No I/O — the caller
/// passes an ordered, contiguous, zero-filled daily-load series; this walks it once and emits CTL/ATL/TSB
/// per day. Deterministic and unit-tested directly, the same shape as <see cref="Training.Load.LoadCalculator"/>.
/// </summary>
public static class PmcCalculator
{
    private const decimal CtlDays = 42m; // "fitness" EWMA span
    private const decimal AtlDays = 7m;  // "fatigue" EWMA span

    /// <summary>
    /// CTL (42-day EWMA), ATL (7-day EWMA) and TSB (yesterday's CTL − ATL) for each day of
    /// <paramref name="series"/>. The series MUST be ordered, one entry per calendar day, and zero-filled
    /// — the caller (AnalyticsService) guarantees this; the calculator does not re-sort or gap-fill. Seeds
    /// CTL = ATL = 0 before the first day. Empty input → empty list.
    /// </summary>
    public static IReadOnlyList<PmcPointDto> Compute(IReadOnlyList<DailyLoadDto> series)
    {
        var points = new List<PmcPointDto>(series.Count);
        decimal ctlPrev = 0m;
        decimal atlPrev = 0m;
        PmcPointDto? yesterday = null;

        foreach (var day in series)
        {
            // TSB is yesterday's CTL − ATL, taken from the DISPLAYED (rounded) values so the form a user
            // reads reconciles with the previous day's shown fitness/fatigue. Seeded 0 before the first day.
            var tsb = yesterday is null ? 0m : Math.Round(yesterday.Ctl - yesterday.Atl, 2);

            var ctl = ctlPrev + (day.Load - ctlPrev) / CtlDays;
            var atl = atlPrev + (day.Load - atlPrev) / AtlDays;

            var point = new PmcPointDto
            {
                Date = day.Date,
                Load = day.Load,
                Ctl = Math.Round(ctl, 2),
                Atl = Math.Round(atl, 2),
                Tsb = tsb
            };
            points.Add(point);
            yesterday = point;

            // Carry the UNROUNDED EWMA values forward to avoid rounding drift across a long series.
            ctlPrev = ctl;
            atlPrev = atl;
        }

        return points;
    }
}
