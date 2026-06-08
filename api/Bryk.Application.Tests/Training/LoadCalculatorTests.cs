using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training;

public class LoadCalculatorTests
{
    private static PlannedWorkout Workout(Sport sport, params WorkoutBlock[] blocks) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = Guid.NewGuid(),
        Sport = sport,
        Blocks = blocks.ToList()
    };

    private static WorkoutBlock Block(int repeats, params WorkoutStep[] steps) => new()
    {
        Repeats = repeats,
        Steps = steps.ToList()
    };

    private static AthleteSportProfile Profile(decimal? threshold = null, decimal? lt2 = null) =>
        new() { ThresholdValue = threshold, Lt2 = lt2 };

    [Fact]
    public void Power_AtFtp_IsIfOne_HourGives100Tss()
    {
        var workout = Workout(Sport.Bike,
            Block(1, new WorkoutStep { DurationSeconds = 3600, TargetPowerLow = 250, TargetPowerHigh = 250 }));

        LoadCalculator.ComputePlannedLoad(workout, Profile(threshold: 250m), null).Should().Be(100m);
    }

    [Fact]
    public void Pace_FasterThanThreshold_UsesInverseIf()
    {
        // threshold pace 300 s/km, target 240 s/km → IF = 300/240 = 1.25 → 3600·1.25²/3600·100 = 156.25
        var workout = Workout(Sport.Run,
            Block(1, new WorkoutStep { DurationSeconds = 3600, TargetPaceLow = 240, TargetPaceHigh = 240 }));

        LoadCalculator.ComputePlannedLoad(workout, Profile(threshold: 300m), null).Should().Be(156.25m);
    }

    [Fact]
    public void Distance_OnlyStep_ConvertsToSecondsViaTargetPace()
    {
        // 5000 m at 300 s/km = 1500 s; IF 1 → 1500/3600·100 = 41.67
        var workout = Workout(Sport.Run,
            Block(1, new WorkoutStep { DistanceMeters = 5000, TargetPaceLow = 300, TargetPaceHigh = 300 }));

        LoadCalculator.ComputePlannedLoad(workout, Profile(threshold: 300m), null).Should().Be(41.67m);
    }

    [Fact]
    public void BlockRepeats_MultiplyTheStepSum()
    {
        // Two 1800 s @ FTP steps (50 TSS each = 100/block) × Repeats 4 = 400
        var workout = Workout(Sport.Bike,
            Block(4,
                new WorkoutStep { DurationSeconds = 1800, TargetPowerLow = 250, TargetPowerHigh = 250 },
                new WorkoutStep { DurationSeconds = 1800, TargetPowerLow = 250, TargetPowerHigh = 250 }));

        LoadCalculator.ComputePlannedLoad(workout, Profile(threshold: 250m), null).Should().Be(400m);
    }

    [Fact]
    public void ZoneOnly_ResolvesBandMidpointFromEffectiveZones()
    {
        // Z3 band 190–225 → mid 207.5; IF = 207.5/250 = 0.83 → 3600·0.83²/3600·100 = 68.89
        var zones = new SportZonesResponse
        {
            Sport = Sport.Bike,
            Metric = ZoneMetric.Power,
            Zones = new List<ZoneDto> { new() { ZoneNumber = 3, LowerBound = 190m, UpperBound = 225m } }
        };
        var workout = Workout(Sport.Bike,
            Block(1, new WorkoutStep { DurationSeconds = 3600, TargetZone = 3 }));

        LoadCalculator.ComputePlannedLoad(workout, Profile(threshold: 250m), zones).Should().Be(68.89m);
    }

    [Fact]
    public void HrTarget_UsesLt2AsThresholdHr()
    {
        // target HR 153, Lt2 170 → IF = 0.9 → 3600·0.81/3600·100 = 81
        var workout = Workout(Sport.Run,
            Block(1, new WorkoutStep { DurationSeconds = 3600, TargetHrLow = 153, TargetHrHigh = 153 }));

        LoadCalculator.ComputePlannedLoad(workout, Profile(lt2: 170m), null).Should().Be(81m);
    }

    [Fact]
    public void MissingThreshold_DegradesToZero()
    {
        var workout = Workout(Sport.Bike,
            Block(1, new WorkoutStep { DurationSeconds = 3600, TargetPowerLow = 250, TargetPowerHigh = 250 }));

        LoadCalculator.ComputePlannedLoad(workout, null, null).Should().Be(0m);
    }

    [Fact]
    public void NoBlocks_ReturnsNull()
    {
        LoadCalculator.ComputePlannedLoad(Workout(Sport.Run), Profile(threshold: 300m), null).Should().BeNull();
    }

    [Fact]
    public void Strength_TonnageWhenLoadPresent()
    {
        // 3×10×50 = 1500 kg × 0.007 = 10.5
        var workout = Workout(Sport.Strength,
            Block(1, new WorkoutStep { Sets = 3, Reps = 10, LoadKg = 50m }));

        LoadCalculator.ComputePlannedLoad(workout, null, null).Should().Be(10.5m);
    }

    [Fact]
    public void Strength_VolumeRpeWhenNoLoad()
    {
        // 3×10×7 = 210 × 0.05 = 10.5
        var workout = Workout(Sport.Strength,
            Block(1, new WorkoutStep { Sets = 3, Reps = 10, Rpe = 7m }));

        LoadCalculator.ComputePlannedLoad(workout, null, null).Should().Be(10.5m);
    }

    [Fact]
    public void ActualLoad_CardioFromStepResults_UsesAvgPower()
    {
        var workout = new Workout
        {
            Sport = Sport.Bike,
            StepResults = { new WorkoutStepResult { AvgPower = 250, ActualDurationSeconds = 3600 } }
        };

        LoadCalculator.ComputeActualLoad(workout, Profile(threshold: 250m)).Should().Be(100m);
    }

    [Fact]
    public void ActualLoad_Strength_UsesSessionRpeAndDuration()
    {
        // RPE 7 × 60 min × 0.165 = 69.3
        var workout = new Workout { Sport = Sport.Strength, Rpe = 7m, ActualDurationSeconds = 3600 };

        LoadCalculator.ComputeActualLoad(workout, null).Should().Be(69.3m);
    }
}
