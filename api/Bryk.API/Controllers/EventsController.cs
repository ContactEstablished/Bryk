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
