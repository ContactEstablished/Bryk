using Bryk.Application.Analytics;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Analytics;

public class AcwrCalculatorTests
{
    private static List<DailyLoadDto> Series(DateOnly start, params decimal[] loads) =>
        loads.Select((l, i) => new DailyLoadDto { Date = start.AddDays(i), Load = l }).ToList();

    // ACWR is the service's job to feed an override-resolved series; these tests feed loads directly and
    // verify only the ratio + the insufficiency rule (override resolution is covered in 14-2).

    [Fact]
    public void NoFirstWorkout_ReturnsNull()
    {
        var eval = new DateOnly(2026, 2, 10);
        var series = Series(eval.AddDays(-39), Enumerable.Repeat(50m, 40).ToArray());

        AcwrCalculator.Compute(series, eval, null).Should().BeNull();
    }

    [Fact]
    public void FewerThan28DaysHistory_ReturnsNull()
    {
        // first workout = eval − 26 → 27 days of history (eval − first + 1 = 27) → null.
        var eval = new DateOnly(2026, 2, 1);
        var first = eval.AddDays(-26);
        var series = Series(first, Enumerable.Repeat(50m, 27).ToArray());

        AcwrCalculator.Compute(series, eval, first).Should().BeNull();
    }

    [Fact]
    public void Exactly28DaysHistory_Computes()
    {
        // first workout = eval − 27 → exactly 28 days of history → a value. Constant load → ACWR 1.00.
        var eval = new DateOnly(2026, 2, 1);
        var first = eval.AddDays(-27);
        var series = Series(first, Enumerable.Repeat(50m, 28).ToArray());

        AcwrCalculator.Compute(series, eval, first).Should().Be(1.00m);
    }

    [Fact]
    public void ZeroChronic_ReturnsNull()
    {
        // 28 days of history but all-zero load → chronic mean 0 → null (no 0/0).
        var eval = new DateOnly(2026, 2, 1);
        var first = eval.AddDays(-27);
        var series = Series(first, Enumerable.Repeat(0m, 28).ToArray());

        AcwrCalculator.Compute(series, eval, first).Should().BeNull();
    }

    [Fact]
    public void AcuteAboveChronic_RatioGreaterThanOne()
    {
        // 28-day series: 21 days at 20 TSS, then 7 days at 80 TSS.
        // acute (last 7) = 80; chronic (28) = (21·20 + 7·80)/28 = 980/28 = 35; ACWR = 80/35 = 2.29.
        var eval = new DateOnly(2026, 2, 1);
        var first = eval.AddDays(-27);
        var loads = Enumerable.Repeat(20m, 21).Concat(Enumerable.Repeat(80m, 7)).ToArray();
        var series = Series(first, loads);

        AcwrCalculator.Compute(series, eval, first).Should().Be(2.29m);
    }
}
