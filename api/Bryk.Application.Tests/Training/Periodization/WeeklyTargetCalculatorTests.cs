using Bryk.Application.Training.Periodization;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training.Periodization;

public class WeeklyTargetCalculatorTests
{
    // Fixture Monday (2026-01-01 is a Thursday) — shared across the 12-week worked example (ADR-0009 §2)
    // and its derived cases (Tasks-18-1).
    private static readonly DateOnly Mon = new(2026, 1, 5);

    [Fact]
    public void Compute_TwelveWeekThreeBuildOneRecoveryWithRaceWeek_MatchesAdrWorkedExample()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 3, 29),
            Baseline: 200.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: new DateOnly(2026, 3, 28));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Should().HaveCount(12);
        result[0].WeekStart.Should().Be(new DateOnly(2026, 1, 5));
        result[11].WeekStart.Should().Be(new DateOnly(2026, 3, 23));

        result.Select(r => r.TargetLoad).Should().Equal(
            200.00m, 214.00m, 228.98m, 137.39m, 245.01m, 262.16m, 280.51m,
            168.31m, 300.15m, 321.16m, 257.73m, 171.82m);

        result.Select(r => r.IsRecoveryWeek).Should().Equal(
            false, false, false, true, false, false, false, true, false, false, false, false);

        result.Select(r => r.IsTaperWeek).Should().Equal(
            false, false, false, false, false, false, false, false, false, false, true, true);
    }

    [Fact]
    public void Compute_EventWeekThatIsAlsoACadenceRecoveryWeek_TapersInsteadOfScaling()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 3, 29),
            Baseline: 200.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: new DateOnly(2026, 3, 28));

        var result = WeeklyTargetCalculator.Compute(input);

        result[11].IsTaperWeek.Should().BeTrue();
        result[11].IsRecoveryWeek.Should().BeFalse();
        result[11].TargetLoad.Should().Be(171.82m);
        result[11].TargetLoad.Should().NotBe(206.18m); // the 60% recovery rule alone would have produced this
    }

    [Fact]
    public void Compute_NullBaseline_ReturnsEmpty()
    {
        var input = new WeeklyTargetInput(Mon, Mon.AddDays(7), null, 3, 1, 60.0m, null);

        WeeklyTargetCalculator.Compute(input).Should().BeEmpty();
    }

    [Fact]
    public void Compute_ZeroBaseline_ReturnsEmpty()
    {
        var input = new WeeklyTargetInput(Mon, Mon.AddDays(7), 0m, 3, 1, 60.0m, null);

        WeeklyTargetCalculator.Compute(input).Should().BeEmpty();
    }

    [Fact]
    public void Compute_NoCadenceFields_RampsEveryWeekAtTheCap()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 1),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(200.00m, 214.00m, 228.98m, 245.01m);
        result.Should().OnlyContain(r => !r.IsRecoveryWeek && !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_RecoveryPercentageNull_TreatsEveryWeekAsBuild()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 1),
            Baseline: 200.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(200.00m, 214.00m, 228.98m, 245.01m);
        result.Should().OnlyContain(r => !r.IsRecoveryWeek && !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_NoLinkedEvent_ProducesNoTaperWeeks()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 8),
            Baseline: 100.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(100.00m, 107.00m, 114.49m, 68.69m, 122.50m);
        result.Select(r => r.IsRecoveryWeek).Should().Equal(false, false, false, true, false);
        result.Should().OnlyContain(r => !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_EventOutsideThePlanWindow_ProducesNoTaperWeeks()
    {
        // Same window/baseline/cadence as the previous case; EventDate is one day past End — byte-identical
        // result (the same literals pinned above), proving the out-of-window event is fully ignored.
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 8),
            Baseline: 100.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: new DateOnly(2026, 2, 9));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(100.00m, 107.00m, 114.49m, 68.69m, 122.50m);
        result.Select(r => r.IsRecoveryWeek).Should().Equal(false, false, false, true, false);
        result.Should().OnlyContain(r => !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_TwoWeekPlanWithEventInFinalWeek_IsAllTaper()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 1, 18),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: new DateOnly(2026, 1, 17));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(150.00m, 107.00m);
        result.Should().OnlyContain(r => r.IsTaperWeek && !r.IsRecoveryWeek);
    }

    [Fact]
    public void Compute_SingleWeekPlanWithEvent_HalvesTheOnlyWeek()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 1, 11),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: new DateOnly(2026, 1, 7));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Should().ContainSingle();
        result[0].TargetLoad.Should().Be(100.00m);
        result[0].IsTaperWeek.Should().BeTrue();
    }

    [Fact]
    public void Compute_MidWeekStartDate_AnchorsTheFirstWeekOnThePrecedingMonday()
    {
        var input = new WeeklyTargetInput(
            StartDate: new DateOnly(2026, 1, 7),
            EndDate: new DateOnly(2026, 1, 20),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.WeekStart).Should().Equal(
            new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 19));
        result.Select(r => r.TargetLoad).Should().Equal(200.00m, 214.00m, 228.98m);
        result.Should().OnlyContain(r => r.WeekStart.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public void Compute_FourConsecutiveBuildWeeks_StayUnderTheAcwrCeiling()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 1),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result[3].TargetLoad.Should().Be(Math.Round(result[2].TargetLoad * 1.07m, 2));
        result[3].TargetLoad.Should().BeLessThanOrEqualTo(262.00m); // = 1.31 × 200, ADR-0009 §1 made executable
    }

    [Fact]
    public void Compute_EndDateBeforeStartDate_ReturnsEmpty()
    {
        var input = new WeeklyTargetInput(Mon, Mon.AddDays(-1), 200.00m, 3, 1, 60.0m, null);

        WeeklyTargetCalculator.Compute(input).Should().BeEmpty();
    }
}
