using Bryk.Application.Goals;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Goals;

public class GoalProgressTests
{
    private static readonly DateOnly Today = new(2026, 7, 1);

    [Fact]
    public void NullTargetDate_ReturnsNoDate()
    {
        var (daysRemaining, status) = GoalProgress.Compute(null, Today);

        daysRemaining.Should().BeNull();
        status.Should().Be(GoalStatus.NoDate);
    }

    [Fact]
    public void TargetIsToday_ReturnsZeroDaysDueSoon()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today, Today);

        daysRemaining.Should().Be(0);
        status.Should().Be(GoalStatus.DueSoon);
    }

    [Fact]
    public void TargetIsTodayPlus14_ReturnsDueSoonBoundaryInclusive()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today.AddDays(14), Today);

        daysRemaining.Should().Be(14);
        status.Should().Be(GoalStatus.DueSoon);
    }

    [Fact]
    public void TargetIsTodayPlus15_ReturnsUpcoming()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today.AddDays(15), Today);

        daysRemaining.Should().Be(15);
        status.Should().Be(GoalStatus.Upcoming);
    }

    [Fact]
    public void TargetIsYesterday_ReturnsOverdue()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today.AddDays(-1), Today);

        daysRemaining.Should().Be(-1);
        status.Should().Be(GoalStatus.Overdue);
    }
}
