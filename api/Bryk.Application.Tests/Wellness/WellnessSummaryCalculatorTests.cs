using Bryk.Application.Wellness;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Wellness;

public class WellnessSummaryCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 7, 26);
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DailyWellness Entry(DateOnly date, decimal? sleepHours = null, int? restingHr = null) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = AthleteId,
        Date = date,
        SleepHours = sleepHours,
        RestingHr = restingHr
    };

    [Fact]
    public void Compute_NoEntries_ReturnsNullAveragesAndHasAnyEntriesFalse()
    {
        var result = WellnessSummaryCalculator.Compute([], Today);

        foreach (var metric in new[]
                 {
                     result.SleepHours, result.SleepQuality, result.RestingHr,
                     result.WeightKg, result.Soreness, result.HrvMs
                 })
        {
            metric.Average.Should().BeNull();
            metric.PriorAverage.Should().BeNull();
            metric.Delta.Should().BeNull();
            metric.DaysWithData.Should().Be(0);
        }

        result.HasAnyEntries.Should().BeFalse();
        result.Days.Should().BeEmpty();
    }

    [Fact]
    public void Compute_WindowBoundsAreTodayMinusSixAndTodayMinusThirteen()
    {
        var result = WellnessSummaryCalculator.Compute([], Today);

        result.To.Should().Be(new DateOnly(2026, 7, 26));
        result.From.Should().Be(new DateOnly(2026, 7, 20));
        result.PriorFrom.Should().Be(new DateOnly(2026, 7, 13));
    }

    [Fact]
    public void Compute_AveragesOnlyTheDaysThatCarryAValue()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 24), sleepHours: 8m),
            Entry(new DateOnly(2026, 7, 25), restingHr: 50),   // no sleep value on this day
            Entry(new DateOnly(2026, 7, 26), sleepHours: 7m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(7.5m); // not 5m — the missing day is missing, not a zero
        result.SleepHours.DaysWithData.Should().Be(2);
    }

    [Fact]
    public void Compute_RoundsAveragesToTwoDecimals()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 24), sleepHours: 7m),
            Entry(new DateOnly(2026, 7, 25), sleepHours: 7m),
            Entry(new DateOnly(2026, 7, 26), sleepHours: 8m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(7.33m);
    }

    [Fact]
    public void Compute_DeltaIsCurrentMinusPrior()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 15), sleepHours: 7m),  // prior window mean 7
            Entry(new DateOnly(2026, 7, 25), sleepHours: 8m),
            Entry(new DateOnly(2026, 7, 26), sleepHours: 7m)   // current window mean 7.5
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(7.5m);
        result.SleepHours.PriorAverage.Should().Be(7m);
        result.SleepHours.Delta.Should().Be(0.5m);
    }

    [Fact]
    public void Compute_DeltaIsNullWhenThePriorWindowHasNoData()
    {
        var entries = new[] { Entry(new DateOnly(2026, 7, 25), sleepHours: 8m) };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(8m);
        result.SleepHours.PriorAverage.Should().BeNull();
        result.SleepHours.Delta.Should().BeNull();
    }

    [Fact]
    public void Compute_IntegerMetricsAverageAsDecimal()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 25), restingHr: 48),
            Entry(new DateOnly(2026, 7, 26), restingHr: 49)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.RestingHr.Average.Should().Be(48.5m); // never integer-divided to 48
    }

    [Fact]
    public void Compute_DaysAreSparseAndAscending()
    {
        // Deliberately supplied newest-first — the calculator orders them.
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 26), sleepHours: 7m),
            Entry(new DateOnly(2026, 7, 13), sleepHours: 8m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.Days.Should().HaveCount(2);
        result.Days[0].Date.Should().Be(new DateOnly(2026, 7, 13));
        result.Days[1].Date.Should().Be(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public void Compute_IgnoresEntriesOutsideTheFourteenDayWindow()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 12), sleepHours: 3m), // one day before PriorFrom
            Entry(new DateOnly(2026, 7, 26), sleepHours: 8m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(8m);      // not 5.5m
        result.SleepHours.PriorAverage.Should().BeNull();
        result.Days.Should().ContainSingle();
        result.Days[0].Date.Should().Be(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public void Compute_TodayIsIncludedInTheCurrentWindow()
    {
        // The off-by-one guard: the current window is inclusive of today.
        var entries = new[] { Entry(Today, sleepHours: 6m) };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(6m);
        result.SleepHours.DaysWithData.Should().Be(1);
    }
}
