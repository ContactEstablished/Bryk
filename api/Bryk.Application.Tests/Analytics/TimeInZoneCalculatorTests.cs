using Bryk.Application.Analytics;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Analytics;

public class TimeInZoneCalculatorTests
{
    private static readonly DateOnly Day = new(2026, 6, 1);

    private static WorkoutStep Step(int? durationSeconds, int? targetZone, int? powerLow = null, int? powerHigh = null) => new()
    {
        DurationSeconds = durationSeconds,
        TargetZone = targetZone,
        TargetPowerLow = powerLow,
        TargetPowerHigh = powerHigh
    };

    private static PlannedWorkout Planned(Guid id, int repeats, params WorkoutStep[] steps) => new()
    {
        Id = id,
        Blocks = new List<WorkoutBlock> { new() { Repeats = repeats, Steps = steps.ToList() } }
    };

    private static Workout Workout(Sport sport, Guid? plannedId = null, int? avgHr = null, int? duration = null) => new()
    {
        Id = Guid.NewGuid(),
        Sport = sport,
        CompletedDate = Day,
        PlannedWorkoutId = plannedId,
        AvgHr = avgHr,
        ActualDurationSeconds = duration
    };

    private static readonly ZonesResponse NoZones = new();

    [Fact]
    public void StructuredWorkout_AttributesStepDurationsToTargetZones()
    {
        var pid = Guid.NewGuid();
        var planned = Planned(pid, repeats: 1, Step(600, 2), Step(480, 4));
        var workouts = new[] { Workout(Sport.Bike, plannedId: pid) };
        var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = planned };

        var result = TimeInZoneCalculator.Compute(workouts, structures, NoZones, maxHr: 190);

        result.Zones.Single(z => z.ZoneNumber == 2).Seconds.Should().Be(600);
        result.Zones.Single(z => z.ZoneNumber == 4).Seconds.Should().Be(480);
        result.MethodBreakdown.StructureSeconds.Should().Be(1080);
        result.TotalSeconds.Should().Be(1080);
    }

    [Fact]
    public void StructuredBike_Z6Z7CollapseToBucket5_AndRepeatsMultiply()
    {
        var pid = Guid.NewGuid();
        var planned = Planned(pid, repeats: 4, Step(120, 6), Step(60, 7)); // 4×(120+60) all → bucket 5
        var workouts = new[] { Workout(Sport.Bike, plannedId: pid) };
        var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = planned };

        var result = TimeInZoneCalculator.Compute(workouts, structures, NoZones, maxHr: 190);

        result.Zones.Single(z => z.ZoneNumber == 5).Seconds.Should().Be(4 * (120 + 60));
        result.MethodBreakdown.StructureSeconds.Should().Be(720);
    }

    [Fact]
    public void RawTarget_ResolvesAgainstZoneBands_WhenNoTargetZone()
    {
        var pid = Guid.NewGuid();
        var planned = Planned(pid, repeats: 1, Step(300, targetZone: null, powerLow: 210, powerHigh: 230)); // midpoint 220
        var workouts = new[] { Workout(Sport.Bike, plannedId: pid) };
        var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = planned };
        var zones = new ZonesResponse
        {
            Sports = new List<SportZonesResponse>
            {
                new()
                {
                    Sport = Sport.Bike,
                    Metric = ZoneMetric.Power,
                    Zones = new List<ZoneDto>
                    {
                        new() { ZoneNumber = 2, LowerBound = 150m, UpperBound = 200m },
                        new() { ZoneNumber = 3, LowerBound = 200m, UpperBound = 250m }, // 220 lands here
                    }
                }
            }
        };

        var result = TimeInZoneCalculator.Compute(workouts, structures, zones, maxHr: 190);

        result.Zones.Single(z => z.ZoneNumber == 3).Seconds.Should().Be(300);
        result.MethodBreakdown.StructureSeconds.Should().Be(300);
    }

    [Theory]
    [InlineData(150, 190, 3)] // 0.789 → Z3
    [InlineData(160, 200, 4)] // exactly 0.80 → Z4 (boundary: < 0.80 is false)
    [InlineData(180, 190, 5)] // 0.947 → Z5
    public void UnlinkedWorkout_ClassifiesWholeSessionByHrMax(int avgHr, int maxHr, int expectedZone)
    {
        var workouts = new[] { Workout(Sport.Run, avgHr: avgHr, duration: 3600) };

        var result = TimeInZoneCalculator.Compute(workouts, new Dictionary<Guid, PlannedWorkout>(), NoZones, maxHr);

        result.Zones.Single(z => z.ZoneNumber == expectedZone).Seconds.Should().Be(3600);
        result.MethodBreakdown.SessionAvgSeconds.Should().Be(3600);
        result.MethodBreakdown.StructureSeconds.Should().Be(0);
    }

    [Fact]
    public void NoStructureNoHr_AndStrength_AreUnclassified()
    {
        var workouts = new[]
        {
            Workout(Sport.Run, avgHr: null, duration: 1800),       // no HR
            Workout(Sport.Strength, avgHr: null, duration: 2400),  // strength, no zones
        };

        var result = TimeInZoneCalculator.Compute(workouts, new Dictionary<Guid, PlannedWorkout>(), NoZones, maxHr: 190);

        result.MethodBreakdown.UnclassifiedSeconds.Should().Be(4200);
        result.Zones.Sum(z => z.Seconds).Should().Be(0);
        result.TotalSeconds.Should().Be(4200);
    }

    [Fact]
    public void MethodSeconds_SumToTotal_AndZonesEqualTotalMinusUnclassified()
    {
        var pid = Guid.NewGuid();
        var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = Planned(pid, 1, Step(600, 2)) };
        var workouts = new[]
        {
            Workout(Sport.Bike, plannedId: pid),                  // structure 600
            Workout(Sport.Run, avgHr: 150, duration: 1800),       // sessionAvg 1800
            Workout(Sport.Run, avgHr: null, duration: 900),       // unclassified 900
        };

        var result = TimeInZoneCalculator.Compute(workouts, structures, NoZones, maxHr: 190);

        var b = result.MethodBreakdown;
        (b.StructureSeconds + b.SessionAvgSeconds + b.UnclassifiedSeconds).Should().Be(result.TotalSeconds);
        result.Zones.Sum(z => z.Seconds).Should().Be(result.TotalSeconds - b.UnclassifiedSeconds);
    }
}
