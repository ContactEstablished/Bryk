using Bryk.Application.ActivityFiles;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.ActivityFiles;

public class ZoneHistogramCalculatorTests
{
    private static readonly DateTime Start = new(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc);

    private static ParsedActivity Activity(params ActivitySample[] samples) =>
        new(Sport.Run, Start, null, null, null, null, null, null, samples);

    private static SportZonesResponse PowerZones() => new()
    {
        Sport = Sport.Bike,
        Metric = ZoneMetric.Power,
        Zones = new List<ZoneDto>
        {
            new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 150m },
            new() { ZoneNumber = 2, LowerBound = 150m, UpperBound = 200m },
            new() { ZoneNumber = 3, LowerBound = 200m, UpperBound = 250m },
            new() { ZoneNumber = 4, LowerBound = 250m, UpperBound = 300m },
            new() { ZoneNumber = 5, LowerBound = 300m, UpperBound = null },
        }
    };

    private static SportZonesResponse PowerZonesWithSixAndSeven() => new()
    {
        Sport = Sport.Bike,
        Metric = ZoneMetric.Power,
        Zones = new List<ZoneDto>
        {
            new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 150m },
            new() { ZoneNumber = 2, LowerBound = 150m, UpperBound = 200m },
            new() { ZoneNumber = 3, LowerBound = 200m, UpperBound = 250m },
            new() { ZoneNumber = 4, LowerBound = 250m, UpperBound = 300m },
            new() { ZoneNumber = 5, LowerBound = 300m, UpperBound = 350m },
            new() { ZoneNumber = 6, LowerBound = 350m, UpperBound = 400m },
            new() { ZoneNumber = 7, LowerBound = 400m, UpperBound = null },
        }
    };

    private static SportZonesResponse PaceZones() => new()
    {
        Sport = Sport.Run,
        Metric = ZoneMetric.Pace,
        Zones = new List<ZoneDto>
        {
            new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 240m },
            new() { ZoneNumber = 2, LowerBound = 240m, UpperBound = 300m },
            new() { ZoneNumber = 3, LowerBound = 300m, UpperBound = 360m },
            new() { ZoneNumber = 4, LowerBound = 360m, UpperBound = 420m },
            new() { ZoneNumber = 5, LowerBound = 420m, UpperBound = null },
        }
    };

    [Fact]
    public void Compute_AlwaysReturnsFiveBucketsOrderedOneToFive()
    {
        var result = ZoneHistogramCalculator.Compute(Activity(), null, null);

        result.Select(r => r.ZoneNumber).Should().Equal(1, 2, 3, 4, 5);
        result.Should().OnlyContain(r => r.Seconds == 0);
    }

    [Fact]
    public void Compute_PowerSamples_BucketByBand()
    {
        var activity = Activity(
            new ActivitySample(0, null, 100, null),
            new ActivitySample(60, null, 175, null),
            new ActivitySample(120, null, 225, null),
            new ActivitySample(180, null, 275, null),
            new ActivitySample(240, null, 275, null)); // trailing sample — contributes nothing itself,
                                                       // but gives the Z4 sample above a gap to bound it

        var result = ZoneHistogramCalculator.Compute(activity, PowerZones(), null);

        result.Single(r => r.ZoneNumber == 1).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 2).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 4).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 5).Seconds.Should().Be(0); // no sample reaches Z5's 300 W floor
    }

    [Fact]
    public void Compute_BikeZoneSixAndSeven_CollapseIntoBucketFive()
    {
        var activity = Activity(
            new ActivitySample(0, null, 450, null), // lands in Z7
            new ActivitySample(60, null, 450, null));

        var result = ZoneHistogramCalculator.Compute(activity, PowerZonesWithSixAndSeven(), null);

        result.Single(r => r.ZoneNumber == 5).Seconds.Should().Be(60);
    }

    [Fact]
    public void Compute_PaceMetricUsesTheSamePredicateAsTimeInZone()
    {
        var activity = Activity(
            new ActivitySample(0, null, null, 300), // exactly Z3's LowerBound (inclusive)
            new ActivitySample(60, null, null, 239), // just below Z1's UpperBound of 240 (exclusive)
            new ActivitySample(120, null, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, PaceZones(), null);

        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 1).Seconds.Should().Be(60);
    }

    [Fact]
    public void Compute_NoZones_FallsBackToPercentOfMaxHr()
    {
        var activity = Activity(
            new ActivitySample(0, 119, null, null),   // 59.5% → Z1
            new ActivitySample(20, 120, null, null),  // exactly 60% → Z2 (boundary lands in the higher bucket)
            new ActivitySample(40, 140, null, null),  // exactly 70% → Z3
            new ActivitySample(60, 160, null, null),  // exactly 80% → Z4
            new ActivitySample(80, 180, null, null),  // exactly 90% → Z5
            new ActivitySample(100, 190, null, null)); // trailing sample — contributes nothing

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: 200);

        result.Single(r => r.ZoneNumber == 1).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 2).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 4).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 5).Seconds.Should().Be(20);
    }

    [Fact]
    public void Compute_GapLongerThanSixtySeconds_IsClampedToSixty()
    {
        var activity = Activity(
            new ActivitySample(0, 150, null, null),
            new ActivitySample(600, 150, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: 200); // 150/200 = 75% → Z3

        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(60);
        result.Sum(r => r.Seconds).Should().Be(60);
    }

    [Fact]
    public void Compute_LastSampleContributesZeroSeconds()
    {
        var activity = Activity(
            new ActivitySample(0, 150, null, null),
            new ActivitySample(45, 150, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: 200);

        result.Sum(r => r.Seconds).Should().Be(45);
    }

    [Fact]
    public void Compute_SamplesWithNoUsableSignal_AreDroppedFromEveryBucket()
    {
        var activity = Activity(
            new ActivitySample(0, null, null, null),
            new ActivitySample(30, null, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: null);

        // The histogram sum (0) is less than the 30-second session — correct per ADR-0007 §4's honesty
        // rule: 19-6 counts only what is measured, never fabricates coverage.
        result.Should().OnlyContain(r => r.Seconds == 0);
    }
}
