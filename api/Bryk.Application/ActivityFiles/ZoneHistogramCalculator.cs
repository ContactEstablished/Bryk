using Bryk.Application.Zones;
using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

/// <summary>
/// Pure sample-to-bucket math (ADR-0010 §5, ADR-0007 §4). Pure: no I/O, no <see cref="DateTime.UtcNow"/>.
/// Always returns exactly five entries, <see cref="ZoneHistogramEntry.ZoneNumber"/> 1..5 ascending, even
/// when every bucket is 0. Per-sample duration is the gap to the next sample clamped to
/// <c>MaxSampleGapSeconds</c> — a paused/gapped file cannot dump an hour into one bucket — and the
/// last sample always contributes 0 (there is no following sample to bound it).
///
/// The band predicate and the coarse %HRmax fallback are deliberately duplicated from
/// <see cref="Analytics.TimeInZoneCalculator"/> rather than shared: that file belongs to Task 19-6, and
/// Tasks-19-2 records the ~10-line duplication as tech debt rather than coupling the two tasks' files.
/// </summary>
public static class ZoneHistogramCalculator
{
    private const int ZoneCount = 5;
    private const int MaxSampleGapSeconds = 60;

    public static IReadOnlyList<ZoneHistogramEntry> Compute(
        ParsedActivity activity,
        SportZonesResponse? sportZones,
        int? maxHr)
    {
        var seconds = new int[ZoneCount + 1]; // index 1..5; index 0 unused
        var samples = activity.Samples;

        for (var i = 0; i < samples.Count; i++)
        {
            // The last sample has no following sample to bound it, so it contributes 0 seconds.
            var duration = i + 1 < samples.Count
                ? Math.Clamp(samples[i + 1].ElapsedSeconds - samples[i].ElapsedSeconds, 0, MaxSampleGapSeconds)
                : 0;

            if (duration <= 0)
            {
                continue;
            }

            if (ResolveZone(samples[i], sportZones, maxHr) is { } zone)
            {
                seconds[zone] += duration;
            }
            // else: no usable signal on this sample — those seconds are dropped (ADR-0007 §4's honesty
            // rule: the histogram sums to less than the session duration when coverage is partial).
        }

        return Enumerable.Range(1, ZoneCount)
            .Select(z => new ZoneHistogramEntry(z, seconds[z]))
            .ToList();
    }

    // First match wins (Tasks-19-2 §3): Power metric+value, then Pace metric+value, then coarse %HRmax.
    // Once a branch is taken on metric+value presence, a missed band lookup inside that branch means "no
    // bucket" — it does NOT fall through to the next branch (mirrors TimeInZoneCalculator.ClassifyStep,
    // where a null band also returns null rather than trying another provenance).
    private static int? ResolveZone(ActivitySample sample, SportZonesResponse? sportZones, int? maxHr)
    {
        if (sportZones is { Metric: ZoneMetric.Power } && sample.Power is { } power)
        {
            return BandZone(power, sportZones.Zones);
        }

        if (sportZones is { Metric: ZoneMetric.Pace } && sample.PaceSecPerUnit is { } pace)
        {
            return BandZone(pace, sportZones.Zones);
        }

        if (sample.Hr is { } hr && hr > 0 && maxHr is { } max && max > 0)
        {
            return HrZone(hr, max);
        }

        return null;
    }

    // Character-identical to TimeInZoneCalculator.cs:122's predicate — do not invert for pace, do not
    // change the comparison operators. Task 19-6 unions this histogram with that calculator's output, so
    // the two must be commensurable.
    private static int? BandZone(int value, IReadOnlyList<ZoneDto> zones)
    {
        var band = zones.FirstOrDefault(z => value >= z.LowerBound && (z.UpperBound is null || value < z.UpperBound));
        return band is null ? null : Math.Min(band.ZoneNumber, ZoneCount);
    }

    // Coarse %HRmax 5-zone scheme, duplicated from TimeInZoneCalculator.cs:127–138 verbatim (ADR-0007 §4).
    private static int HrZone(int avgHr, int maxHr)
    {
        var pct = (decimal)avgHr / maxHr;
        return pct switch
        {
            < 0.60m => 1,
            < 0.70m => 2,
            < 0.80m => 3,
            < 0.90m => 4,
            _ => 5
        };
    }
}
