using Bryk.Application.Analytics;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Analytics;

public class PmcCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    private static List<DailyLoadDto> Series(DateOnly start, params decimal[] loads) =>
        loads.Select((l, i) => new DailyLoadDto { Date = start.AddDays(i), Load = l }).ToList();

    [Fact]
    public void EmptySeries_ReturnsEmpty()
    {
        PmcCalculator.Compute(new List<DailyLoadDto>()).Should().BeEmpty();
    }

    [Fact]
    public void Day1_SeedsFromZero()
    {
        // Single 100-TSS day: TSB is yesterday's 0 − 0 = 0; CTL = 100/42 = 2.38; ATL = 100/7 = 14.29.
        var result = PmcCalculator.Compute(Series(Start, 100m));

        result.Should().HaveCount(1);
        result[0].Tsb.Should().Be(0m);
        result[0].Ctl.Should().Be(2.38m);
        result[0].Atl.Should().Be(14.29m);
        result[0].Load.Should().Be(100m);
    }

    [Fact]
    public void ZeroDay_DecaysCtlAndAtl_AndIsNotSkipped()
    {
        // Day 1: 100 TSS. Day 2: 0 TSS → both EWMAs decay toward 0 (load is below the running average).
        var result = PmcCalculator.Compute(Series(Start, 100m, 0m));

        result.Should().HaveCount(2);
        result[1].Ctl.Should().BeLessThan(result[0].Ctl);
        result[1].Atl.Should().BeLessThan(result[0].Atl);
        // The zero day is real data — it keeps its own dated point.
        result[1].Date.Should().Be(Start.AddDays(1));
        result[1].Load.Should().Be(0m);
    }

    [Fact]
    public void Tsb_UsesYesterdaysValues()
    {
        // Day-2 TSB must equal day-1's CTL − ATL (yesterday's values, not today's).
        var result = PmcCalculator.Compute(Series(Start, 100m, 100m));

        result[1].Tsb.Should().Be(Math.Round(result[0].Ctl - result[0].Atl, 2));
    }

    [Fact]
    public void ConstantLoad_CtlConvergesToward100()
    {
        // Constant 100 TSS/day for 120 days: CTL approaches 100 (≈94.45 by day 120); ATL converges faster
        // (≈100). The worked example from the ROADMAP math conventions.
        var result = PmcCalculator.Compute(Series(Start, Enumerable.Repeat(100m, 120).ToArray()));

        var last = result[^1];
        last.Ctl.Should().BeGreaterThan(94m).And.BeLessThan(100m);
        last.Atl.Should().BeGreaterThan(99m).And.BeLessThanOrEqualTo(100m);
    }
}
