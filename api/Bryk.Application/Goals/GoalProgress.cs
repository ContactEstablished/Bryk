namespace Bryk.Application.Goals;

/// <summary>
/// Pure date-based goal progress (Tasks-17-1). No I/O, no DateTime.UtcNow — the caller passes
/// <paramref name="today"/> in (the calculators-take-today convention from
/// WeeklyLoadCalculator/PmcCalculator), so this is deterministic under test.
/// </summary>
public static class GoalProgress
{
    private const int DueSoonThresholdDays = 14;

    public static (int? DaysRemaining, GoalStatus Status) Compute(DateOnly? targetDate, DateOnly today)
    {
        if (targetDate is not { } target)
        {
            return (null, GoalStatus.NoDate);
        }

        var daysRemaining = target.DayNumber - today.DayNumber;

        var status = daysRemaining switch
        {
            < 0 => GoalStatus.Overdue,
            <= DueSoonThresholdDays => GoalStatus.DueSoon,
            _ => GoalStatus.Upcoming
        };

        return (daysRemaining, status);
    }
}
