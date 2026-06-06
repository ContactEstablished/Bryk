using Asp.Versioning;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ZonesController(IZoneService zoneService) : ControllerBase
{
    /// <summary>Returns the current athlete's effective training zones for every sport that has a threshold.</summary>
    [HttpGet]
    public async Task<IActionResult> GetZonesAsync(CancellationToken cancellationToken)
    {
        ZonesResponse result = await zoneService.GetZonesAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Replaces the current athlete's zone overrides for one sport and returns that sport's effective zones.</summary>
    [HttpPut("{sport}")]
    public async Task<IActionResult> SetOverridesAsync(Sport sport, [FromBody] ZoneOverrideRequest request, CancellationToken cancellationToken)
    {
        SportZonesResponse result = await zoneService.SetOverridesAsync(sport, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Clears the current athlete's zone overrides for one sport, reverting it to computed zones.</summary>
    [HttpDelete("{sport}")]
    public async Task<IActionResult> ResetOverridesAsync(Sport sport, CancellationToken cancellationToken)
    {
        await zoneService.ResetOverridesAsync(sport, cancellationToken);
        return NoContent();
    }
}
