using Asp.Versioning;
using Bryk.Application.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
{
    /// <summary>
    /// Returns the current athlete's zero-filled daily training load (Σ EffectiveLoad per day) over
    /// <c>[from, to]</c>. Both dates are required; the range must be ≤ 400 days, <c>from ≤ to</c>, and
    /// <c>to</c> not in the future (else 400).
    /// </summary>
    [HttpGet("daily-load")]
    public async Task<IActionResult> GetDailyLoadAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DailyLoadDto> result = await analyticsService.GetDailyLoadAsync(from, to, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the Performance Management Chart (daily load + CTL/ATL/TSB) over <c>[from, to]</c> plus a
    /// <c>current</c> summary for the range's last day (today's-equivalent for a <c>to = today</c> request:
    /// CTL/ATL/TSB and ACWR). <c>current</c> is null for an athlete with no workout history; its ACWR is
    /// null under 28 days of history. Same range rules as daily-load.
    /// </summary>
    [HttpGet("pmc")]
    public async Task<IActionResult> GetPmcAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        PmcResponse result = await analyticsService.GetPmcAsync(from, to, cancellationToken);
        return Ok(result);
    }
}
