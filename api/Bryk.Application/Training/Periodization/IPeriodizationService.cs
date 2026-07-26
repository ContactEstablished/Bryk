namespace Bryk.Application.Training.Periodization;

/// <summary>
/// Compute-on-read weekly load targets for a training plan (ADR-0009). Athlete identity comes from
/// <see cref="Common.ICurrentUserService"/>. Throws
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (→ 404) when the plan is missing or
/// belongs to another athlete.
/// </summary>
public interface IPeriodizationService
{
    Task<WeeklyTargetsResponse> GetWeeklyTargetsAsync(Guid planId, CancellationToken ct = default);
}
