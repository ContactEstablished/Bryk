namespace Bryk.Application.Calendar;

/// <summary>
/// Range query for the calendar feed. Both bounds required after the service applies defaults;
/// <c>From &lt;= To</c>; span ≤ 62 days. <c>To</c> may be in the future — the calendar shows future
/// planned workouts (unlike the analytics range validator).
/// </summary>
public class CalendarFeedRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
