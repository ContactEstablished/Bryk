using Bryk.Application.Analytics;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Analytics;

public class PeaksCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 6, 14);

    private static PeakWorkoutSummary Summary(
        DateOnly date, Sport sport, decimal? load = null, int? duration = null,
        int? distance = null, decimal? pace = null, decimal? power = null) => new()
    {
        WorkoutId = Guid.NewGuid(),
        Date = date,
        Sport = sport,
        Load = load,
        DurationSeconds = duration,
        DistanceMeters = distance,
        AvgPaceSecPerUnit = pace,
        AvgPowerWatts = power
    };

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        PeaksCalculator.Compute(new List<PeakWorkoutSummary>(), Today).Should().BeEmpty();
    }

    [Fact]
    public void Load_PicksMax_WithSecondBestAsPrevious()
    {
        var summaries = new List<PeakWorkoutSummary>
        {
            Summary(Today.AddDays(-5), Sport.Bike, load: 80m),
            Summary(Today.AddDays(-3), Sport.Run, load: 120m),  // best
            Summary(Today.AddDays(-1), Sport.Swim, load: 100m), // second
        };

        var load = PeaksCalculator.Compute(summaries, Today).Single(r => r.Kind == PeakKind.Load);

        load.Value.Should().Be(120m);
        load.Sport.Should().Be(Sport.Run);
        load.PreviousValue.Should().Be(100m);
    }

    [Fact]
    public void Pace_PicksFastest_LowerIsBetter()
    {
        var summaries = new List<PeakWorkoutSummary>
        {
            Summary(Today.AddDays(-2), Sport.Run, pace: 300m), // 5:00/km — fastest
            Summary(Today.AddDays(-1), Sport.Run, pace: 330m), // 5:30/km
        };

        var pace = PeaksCalculator.Compute(summaries, Today).Single(r => r.Kind == PeakKind.Pace);

        pace.Value.Should().Be(300m);
        pace.PreviousValue.Should().Be(330m); // the next-fastest, not the slowest by magnitude
    }

    [Fact]
    public void Pace_IsPerSport_RunAndSwimDoNotCompare()
    {
        // Swim sec/100 m (136) is numerically smaller than run sec/km (300) but the units differ — they must
        // not collapse into one record. Expect one Pace record per sport, each with its own value/sport.
        var summaries = new List<PeakWorkoutSummary>
        {
            Summary(Today, Sport.Run, pace: 300m),
            Summary(Today, Sport.Swim, pace: 136m),
        };

        var paces = PeaksCalculator.Compute(summaries, Today).Where(r => r.Kind == PeakKind.Pace).ToList();

        paces.Should().HaveCount(2);
        paces.Should().Contain(r => r.Sport == Sport.Run && r.Value == 300m);
        paces.Should().Contain(r => r.Sport == Sport.Swim && r.Value == 136m);
    }

    [Fact]
    public void IsRecent_BoundaryIs90DaysInclusive()
    {
        var summaries = new List<PeakWorkoutSummary>
        {
            Summary(Today.AddDays(-89), Sport.Bike, power: 250m), // within 90 days
            Summary(Today.AddDays(-90), Sport.Bike, power: 300m), // best, but older than 90 days
        };

        var power = PeaksCalculator.Compute(summaries, Today).Single(r => r.Kind == PeakKind.Power);

        power.Value.Should().Be(300m);
        power.IsRecent.Should().BeFalse(); // the record itself is 90 days old
    }

    [Fact]
    public void SingleSample_HasNoPreviousValue()
    {
        var summaries = new List<PeakWorkoutSummary> { Summary(Today, Sport.Run, duration: 3600) };

        var duration = PeaksCalculator.Compute(summaries, Today).Single(r => r.Kind == PeakKind.Duration);

        duration.Value.Should().Be(3600m);
        duration.PreviousValue.Should().BeNull();
        duration.IsRecent.Should().BeTrue();
    }

    [Fact]
    public void KindWithNoData_IsAbsent()
    {
        // Run workouts only, no power → no Power record; a 0/negative metric never qualifies.
        var summaries = new List<PeakWorkoutSummary>
        {
            Summary(Today, Sport.Run, load: 50m, duration: 1800, power: 0m),
        };

        var records = PeaksCalculator.Compute(summaries, Today);

        records.Should().Contain(r => r.Kind == PeakKind.Load);
        records.Should().NotContain(r => r.Kind == PeakKind.Power);
        records.Should().NotContain(r => r.Kind == PeakKind.Distance); // no distance set
    }
}
