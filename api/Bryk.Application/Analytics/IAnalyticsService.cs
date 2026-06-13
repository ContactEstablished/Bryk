namespace Bryk.Application.Analytics;

/// <summary>
/// Compute-on-read analytics for the current athlete (ADR-0006): the zero-filled daily-load series and
/// the Performance Management Chart (CTL/ATL/TSB) + ACWR. Athlete identity resolves from
/// <see cref="Common.ICurrentUserService"/>; there is no persistence (no snapshot table). The range is
/// validated (both bounds required, <c>from ≤ to</c>, ≤ 400 days, no future <c>to</c>).
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Zero-filled daily EffectiveLoad over <c>[from, to]</c> (gap days contribute 0).</summary>
    Task<IReadOnlyList<DailyLoadDto>> GetDailyLoadAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// The PMC series over <c>[from, to]</c> plus a <c>current</c> summary for the range's last day
    /// (today's-equivalent for the dashboard's <c>to = today</c> call). <c>current</c> is null for an
    /// athlete with no workout history; its ACWR is null under 28 days of history.
    /// </summary>
    Task<PmcResponse> GetPmcAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
