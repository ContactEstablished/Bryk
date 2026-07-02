using Asp.Versioning;
using Bryk.Application.Events;
using Bryk.Application.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class EventsController(IEventService eventService) : ControllerBase
{
    /// <summary>
    /// Returns the current athlete's events ordered by
    /// <see cref="Bryk.Domain.Entities.Event.EventDate"/> ascending, each carrying its <c>Notes</c> and
    /// linked-plan ids/names (reverse EventId lookup, display-only). When <paramref name="upcoming"/> is
    /// true, filters to events whose date is today or later; defaults to false (all events).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] bool upcoming, CancellationToken cancellationToken)
    {
        IReadOnlyList<EventListItemResponse> result = await eventService.GetAllAsync(upcoming, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single event owned by the current athlete, with its linked plans. 404 if it does
    /// not exist or belongs to another athlete.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        EventListItemResponse? result = await eventService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Creates a new event for the current athlete.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] EventDto request, CancellationToken cancellationToken)
    {
        EventResponse result = await eventService.CreateAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>Updates an event owned by the current athlete. 404 if it does not exist or belongs to another athlete.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] EventDto request, CancellationToken cancellationToken)
    {
        EventResponse result = await eventService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes an event owned by the current athlete. 404 if it does not exist or belongs to another athlete.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await eventService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
