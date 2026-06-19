using Asp.Versioning;
using Bryk.Application.Calendar;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class CalendarController(ICalendarService calendarService) : ControllerBase
{
    /// <summary>
    /// Returns the current athlete's merged calendar feed (planned workouts + completed workouts + events)
    /// over <c>[from, to]</c>. Defaults: <c>from = today - 41</c>, <c>to = today</c> (a 42-day window).
    /// Range must be ≤ 62 days, <c>from ≤ to</c>; <c>to</c> may be in the future (the calendar shows
    /// future planned workouts). One day per date in the range — empty days are included (the grid needs
    /// every cell). Per-planned-workout compliance is classified per ADR-0008 §1 (grey/green/yellow/red +
    /// unplanned flag). Athlete identity resolves from <c>ICurrentUserService</c>, never from the query.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFeedAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        CalendarFeedResponse result = await calendarService.GetFeedAsync(from, to, cancellationToken);
        return Ok(result);
    }
}
