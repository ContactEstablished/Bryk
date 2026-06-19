namespace Bryk.Application.Calendar;

/// <summary>
/// One day in the calendar feed. Items ordered: events first (by <see cref="CalendarItemDto.Priority"/>),
/// then planned (by <see cref="CalendarItemDto.Title"/>), then unplanned completions (by title).
/// Matched planned+completed pairs are NOT merged.
/// </summary>
public class CalendarDayDto
{
    public DateOnly Date { get; set; }

    public IReadOnlyList<CalendarItemDto> Items { get; set; } = Array.Empty<CalendarItemDto>();
}
