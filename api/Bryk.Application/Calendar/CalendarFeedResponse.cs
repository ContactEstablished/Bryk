namespace Bryk.Application.Calendar;

/// <summary>
/// The merged calendar feed over a bounded range. One <see cref="CalendarDayDto"/> per day in
/// <c>[RangeStart, RangeEnd]</c> inclusive, even empty days — the grid needs every cell.
/// </summary>
public class CalendarFeedResponse
{
    public DateOnly RangeStart { get; set; }
    public DateOnly RangeEnd { get; set; }

    public IReadOnlyList<CalendarDayDto> Days { get; set; } = Array.Empty<CalendarDayDto>();
}
