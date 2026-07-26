using Asp.Versioning;
using Bryk.Application.Wellness;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WellnessController(IWellnessService wellnessService) : ControllerBase
{
    /// <summary>
    /// Creates or replaces the current athlete's wellness entry for <paramref name="date"/>. Returns
    /// <b>200</b> for both create and update: the URL is client-chosen and the call is idempotent, so
    /// the response is identical whichever branch ran. PUT replaces the whole day — a metric omitted
    /// from the body is cleared.
    ///
    /// The date is guarded twice on purpose. <c>SuppressModelStateInvalidFilter</c> is on app-wide
    /// (<c>Program.cs:32–33</c>), so a route segment that fails to bind produces no 400 — it arrives as
    /// <c>default(DateOnly)</c> and the action still runs. The <c>:datetime</c> route constraint makes a
    /// non-date segment a <b>404</b> before any binding happens; a segment that satisfies the constraint
    /// but still fails <c>DateOnly</c> binding falls through to the validator's <c>default</c> rule and
    /// returns <b>400</b>. Both layers are required; neither alone is sufficient.
    ///
    /// 400 also for a future date, an out-of-range metric, or a body with no metric at all.
    /// </summary>
    [HttpPut("{date:datetime}")]
    public async Task<IActionResult> PutAsync(DateOnly date, [FromBody] WellnessEntryRequest request, CancellationToken cancellationToken)
    {
        WellnessEntryResponse result = await wellnessService.UpsertAsync(date, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the current athlete's wellness entries in <c>[from, to]</c> — sparse (days with no entry
    /// are simply absent) and ascending by date. Both bounds are required; the range must be ≤ 400 days,
    /// <c>from ≤ to</c>, and <c>to</c> not in the future (else 400).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRangeAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WellnessEntryResponse> result = await wellnessService.GetRangeAsync(from, to, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the dashboard summary in one call: per-metric 7-day averages ending today, deltas versus
    /// the prior 7 days, and a sparse 14-day daily series for the sparklines. Always 200 — an athlete
    /// with no entries gets null averages and <c>hasAnyEntries: false</c>, never zeros.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        WellnessSummaryResponse result = await wellnessService.GetSummaryAsync(cancellationToken);
        return Ok(result);
    }
}
