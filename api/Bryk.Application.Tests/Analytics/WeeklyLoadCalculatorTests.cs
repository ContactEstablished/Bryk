using Bryk.Application.Analytics;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Analytics;

public class WeeklyLoadCalculatorTests
{
    [Fact]
    public void EmptySeries_NoRollingAverages_NoBand()
    {
        var (rolling, band) = WeeklyLoadCalculator.Compute(new List<decimal>());

        rolling.Should().BeEmpty();
        band.Should().BeNull();
    }

    [Fact]
    public void RollingAverage_IsTrailing4WeekMean()
    {
        // [100,200,300,400,500]: rolling = [100, 150, 200, mean(100..400)=250, mean(200..500)=350].
        var (rolling, _) = WeeklyLoadCalculator.Compute(new List<decimal> { 100m, 200m, 300m, 400m, 500m });

        rolling.Should().Equal(100m, 150m, 200m, 250m, 350m);
    }

    [Fact]
    public void Band_Is_08_To_13_TimesLatestRollingAverage()
    {
        // Latest rolling average = 350 → band [0.8×350, 1.3×350] = [280, 455].
        var (_, band) = WeeklyLoadCalculator.Compute(new List<decimal> { 100m, 200m, 300m, 400m, 500m });

        band.Should().NotBeNull();
        band!.Lower.Should().Be(280m);
        band.Upper.Should().Be(455m);
    }

    [Fact]
    public void SingleWeek_RollingEqualsThatWeek_BandFromIt()
    {
        var (rolling, band) = WeeklyLoadCalculator.Compute(new List<decimal> { 120m });

        rolling.Should().Equal(120m);
        band!.Lower.Should().Be(96m);   // 0.8 × 120
        band.Upper.Should().Be(156m);   // 1.3 × 120
    }

    [Fact]
    public void AllZeroActuals_NoBand()
    {
        var (rolling, band) = WeeklyLoadCalculator.Compute(new List<decimal> { 0m, 0m, 0m });

        rolling.Should().Equal(0m, 0m, 0m);
        band.Should().BeNull(); // honesty — no fabricated [0,0] band
    }
}
