namespace Bryk.Application.Calendar;

/// <summary>
/// The calendar feed for the current athlete (ADR-0008 §1): a merged, day-keyed view of planned
/// workouts, completed workouts, and events over a bounded range, with per-planned-workout compliance.
/// Athlete identity resolves from <see cref="Common.ICurrentUserService"/>; there is no athlete id in
/// the request. The range defaults to a 42-day window ending today (≤ 62-day cap).
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// The merged feed over <c>[from, to]</c> (defaults: today-41 → today). Range ≤ 62 days,
    /// <c>from ≤ to</c>; <c>to</c> may be in the future. One day per date in the range, even empty days.
    /// </summary>
    Task<CalendarFeedResponse> GetFeedAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
