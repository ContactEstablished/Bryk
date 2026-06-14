namespace Bryk.Application.Analytics;

/// <summary>
/// Pure weekly-load math (ADR-0007 §1, §3). No I/O — the caller passes the per-week actual loads ordered
/// oldest → newest; this returns the trailing 4-week rolling average per week and the single optimal band.
/// Deterministic and unit-tested directly, the same shape as <see cref="PmcCalculator"/>.
/// </summary>
public static class WeeklyLoadCalculator
{
    private const decimal BandLowerMultiplier = 0.8m; // ACWR sweet-spot floor
    private const decimal BandUpperMultiplier = 1.3m; // ACWR sweet-spot ceiling (Phase 18's ramp cap)
    private const int RollingWindow = 4;              // 4-week trailing mean

    /// <summary>
    /// For each week, the trailing mean of <paramref name="actualLoads"/> over the last up-to-4 weeks; and
    /// the optimal band = <c>[0.8, 1.3] × A</c> where <c>A</c> is the most recent week's rolling average.
    /// The band is <c>null</c> when the series is empty or <c>A == 0</c> (a fresh athlete — no fabricated band).
    /// </summary>
    public static (IReadOnlyList<decimal> RollingAverages, OptimalBandDto? Band) Compute(IReadOnlyList<decimal> actualLoads)
    {
        var rolling = new decimal[actualLoads.Count];
        for (var i = 0; i < actualLoads.Count; i++)
        {
            var start = Math.Max(0, i - (RollingWindow - 1));
            decimal sum = 0m;
            for (var j = start; j <= i; j++)
            {
                sum += actualLoads[j];
            }

            rolling[i] = Math.Round(sum / (i - start + 1), 2);
        }

        OptimalBandDto? band = null;
        if (rolling.Length > 0 && rolling[^1] > 0m)
        {
            var a = rolling[^1];
            band = new OptimalBandDto
            {
                Lower = Math.Round(BandLowerMultiplier * a, 2),
                Upper = Math.Round(BandUpperMultiplier * a, 2)
            };
        }

        return (rolling, band);
    }
}
