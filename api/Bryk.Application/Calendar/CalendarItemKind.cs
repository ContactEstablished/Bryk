namespace Bryk.Application.Calendar;

/// <summary>
/// The kind of <see cref="CalendarItemDto"/>: a planned workout, a completed workout, or an event.
/// </summary>
public enum CalendarItemKind
{
    Planned = 1,
    Completed = 2,
    Event = 3
}
