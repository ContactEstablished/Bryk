using Bryk.Application.ActivityFiles;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;

namespace Bryk.Application.Analytics;

/// <summary>
/// Pure time-in-zone (ADR-0007 §4, ADR-0010 §5, ROADMAP math conventions). No I/O — the caller passes the
/// completed workouts, the linked planned structures, the athlete's zones, MaxHr, and the per-zone
/// histograms of any workout that came from an imported file. Builds a 5-bucket intensity histogram
/// (seconds) with a samples / structure / sessionAvg / unclassified breakdown that sums to the total.
/// Sample-derived seconds are measured; the other three are estimates, and samples win outright for a
/// covered workout.
/// </summary>
public static class TimeInZoneCalculator
{
    private const int ZoneCount = 5;

    public static TimeInZoneResponse Compute(
        IReadOnlyList<Workout> workouts,
        IReadOnlyDictionary<Guid, PlannedWorkout> structures,
        ZonesResponse zones,
        int? maxHr,
        IReadOnlyDictionary<Guid, IReadOnlyList<ZoneHistogramEntry>> sampleHistograms)
    {
        var zoneSeconds = new int[ZoneCount + 1]; // index 1..5
        var sampleSeconds = 0;
        var structureSeconds = 0;
        var sessionAvgSeconds = 0;
        var unclassifiedSeconds = 0;

        foreach (var workout in workouts)
        {
            // 0. samples — an imported file's measured per-zone histogram (ADR-0010 §5). It wins outright
            // over structure and sessionAvg: those are estimates of the same time. A histogram that sums
            // to zero is treated as absent (the file carried no usable signal) and the workout falls
            // through to the estimate chain below.
            if (sampleHistograms.TryGetValue(workout.Id, out var histogram) && histogram.Count > 0)
            {
                var measured = 0;
                foreach (var bucket in histogram)
                {
                    // Out-of-range zone numbers are ignored, not clamped: ZoneHistogramCalculator already
                    // collapsed to 1..5, so anything else is corrupt data and must not fold into bucket 5.
                    if (bucket.ZoneNumber >= 1 && bucket.ZoneNumber <= ZoneCount && bucket.Seconds > 0)
                    {
                        zoneSeconds[bucket.ZoneNumber] += bucket.Seconds;
                        measured += bucket.Seconds;
                    }
                }

                if (measured > 0)
                {
                    sampleSeconds += measured;
                    // Samples with no usable signal are not measured time; attribute the remainder of the
                    // session honestly rather than shrinking the athlete's total training time.
                    unclassifiedSeconds += Math.Max(0, (workout.ActualDurationSeconds ?? 0) - measured);
                    continue;
                }
            }

            var planned = workout.PlannedWorkoutId is { } pid
                          && structures.TryGetValue(pid, out var p)
                          && p.Blocks.Count > 0
                ? p
                : null;

            if (planned is not null)
            {
                // 1. structure — attribute each planned step's duration to its zone.
                var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == workout.Sport);
                foreach (var block in planned.Blocks)
                {
                    var repeats = Math.Max(block.Repeats, 1);
                    foreach (var step in block.Steps)
                    {
                        var seconds = (step.DurationSeconds ?? 0) * repeats;
                        if (seconds <= 0)
                        {
                            continue; // distance-only / zero-duration steps contribute no known time
                        }

                        if (ClassifyStep(step, sportZones) is { } zone)
                        {
                            zoneSeconds[zone] += seconds;
                            structureSeconds += seconds;
                        }
                        else
                        {
                            unclassifiedSeconds += seconds;
                        }
                    }
                }
            }
            else if (workout.AvgHr is { } hr && hr > 0 && maxHr is { } max && max > 0)
            {
                // 2. sessionAvg — the whole session goes to one bucket via coarse %HRmax.
                var seconds = workout.ActualDurationSeconds ?? 0;
                if (seconds > 0)
                {
                    zoneSeconds[HrZone(hr, max)] += seconds;
                    sessionAvgSeconds += seconds;
                }
            }
            else
            {
                // 3. unclassified — no structure, no usable AvgHr (incl. strength).
                unclassifiedSeconds += workout.ActualDurationSeconds ?? 0;
            }
        }

        var zoneList = Enumerable.Range(1, ZoneCount)
            .Select(z => new ZoneTimeDto { ZoneNumber = z, Seconds = zoneSeconds[z] })
            .ToList();

        return new TimeInZoneResponse
        {
            Zones = zoneList,
            MethodBreakdown = new ZoneTimeMethodBreakdownDto
            {
                SampleSeconds = sampleSeconds,
                StructureSeconds = structureSeconds,
                SessionAvgSeconds = sessionAvgSeconds,
                UnclassifiedSeconds = unclassifiedSeconds
            },
            TotalSeconds = sampleSeconds + structureSeconds + sessionAvgSeconds + unclassifiedSeconds
        };
    }

    // A planned step → intensity bucket 1..5: TargetZone (collapsed via min(z,5)), else the raw target's
    // midpoint resolved against the sport's zone bands, else null (unclassified — incl. HR-only steps).
    private static int? ClassifyStep(WorkoutStep step, SportZonesResponse? sportZones)
    {
        if (step.TargetZone is { } tz && tz > 0)
        {
            return Math.Min(tz, ZoneCount);
        }

        if (sportZones is null)
        {
            return null;
        }

        var value = sportZones.Metric switch
        {
            ZoneMetric.Power => Midpoint(step.TargetPowerLow, step.TargetPowerHigh),
            ZoneMetric.Pace => Midpoint(step.TargetPaceLow, step.TargetPaceHigh),
            _ => null
        };

        if (value is not { } v)
        {
            return null;
        }

        var band = sportZones.Zones.FirstOrDefault(z => v >= z.LowerBound && (z.UpperBound is null || v < z.UpperBound));
        return band is null ? null : Math.Min(band.ZoneNumber, ZoneCount);
    }

    // Coarse %HRmax 5-zone scheme (ADR-0007 §4): <60% Z1, <70% Z2, <80% Z3, <90% Z4, ≥90% Z5.
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

    private static decimal? Midpoint(int? low, int? high)
    {
        if (low is { } l && high is { } h) return (l + h) / 2m;
        if (low is { } lo) return lo;
        if (high is { } hi) return hi;
        return null;
    }
}
