using Asp.Versioning;
using Bryk.Application.Analytics;
using Bryk.Domain.Entities;
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

    /// <summary>
    /// Returns per-ISO-week planned vs actual training load over the last <paramref name="weeks"/>
    /// Monday-anchored weeks (default 8; an explicit value outside 1–26 returns 400), each with the 4-week
    /// rolling average, plus the single optimal band (<c>[0.8, 1.3] ×</c> the trailing-4-week mean actual
    /// load; null for a fresh athlete).
    /// </summary>
    [HttpGet("weekly-load")]
    public async Task<IActionResult> GetWeeklyLoadAsync(
        [FromQuery] int? weeks,
        CancellationToken cancellationToken)
    {
        WeeklyLoadResponse result = await analyticsService.GetWeeklyLoadAsync(weeks, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the current athlete's session-level personal records — highest load, longest duration and
    /// distance, fastest average pace (run/swim), highest average power (bike) — optionally filtered to
    /// <paramref name="sport"/>. Compute-on-read and session-level only (not sample-derived peaks).
    /// </summary>
    [HttpGet("peaks")]
    public async Task<IActionResult> GetPeaksAsync(
        [FromQuery] Sport? sport,
        CancellationToken cancellationToken)
    {
        PeaksResponse result = await analyticsService.GetPeaksAsync(sport, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a coarse, <b>estimated</b> time-in-zone histogram (seconds per intensity bucket) over
    /// <c>[from, to]</c>, optionally filtered to <paramref name="sport"/>, with a structure / session-HR /
    /// unclassified method breakdown that sums to the total. Estimated until file import (Phase 19) supplies
    /// real samples. Same range rules as the PMC endpoints.
    /// </summary>
    [HttpGet("time-in-zone")]
    public async Task<IActionResult> GetTimeInZoneAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Sport? sport,
        CancellationToken cancellationToken)
    {
        TimeInZoneResponse result = await analyticsService.GetTimeInZoneAsync(from, to, sport, cancellationToken);
        return Ok(result);
    }
}
