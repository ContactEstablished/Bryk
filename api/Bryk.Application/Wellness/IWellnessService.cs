namespace Bryk.Application.Wellness;

/// <summary>
/// The whole wellness surface (ADR-0011 §2). Athlete identity always comes from
/// <see cref="Common.ICurrentUserService"/>; this service never reads or writes
/// <see cref="Bryk.Domain.Entities.Athlete"/> (§1).
/// </summary>
public interface IWellnessService
{
    /// <summary>
    /// Creates or replaces the athlete's entry for <paramref name="date"/>. The route date always wins
    /// over the body's. PUT replaces the whole day: a metric omitted from the request is cleared, not
    /// preserved. Idempotent — re-submitting the same day updates the existing row rather than adding a
    /// second one. 400 on an invalid date or an out-of-range/all-null body.
    /// </summary>
    Task<WellnessEntryResponse> UpsertAsync(DateOnly date, WellnessEntryRequest request, CancellationToken ct = default);

    /// <summary>
    /// The athlete's entries in <c>[from, to]</c>, sparse and ascending by date. Both bounds are
    /// required; <c>from ≤ to</c>, span ≤ 400 days, <c>to</c> not in the future (else 400).
    /// </summary>
    Task<IReadOnlyList<WellnessEntryResponse>> GetRangeAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// The dashboard's one call: 7-day averages ending today, deltas versus the prior 7, and a sparse
    /// 14-day daily series. No parameters — the window is always anchored on today (UTC).
    /// </summary>
    Task<WellnessSummaryResponse> GetSummaryAsync(CancellationToken ct = default);
}
