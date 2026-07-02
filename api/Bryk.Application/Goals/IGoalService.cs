using Bryk.Application.Onboarding;

namespace Bryk.Application.Goals;

/// <summary>
/// Per-item create / update / delete plus list read for the current athlete's goals.
/// Athlete identity is resolved from <see cref="Common.ICurrentUserService"/> — never from
/// a caller parameter. <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/> throw
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (mapped to 404 by the
/// global exception middleware) when the goal does not exist or belongs to another athlete.
/// <see cref="GetAllAsync"/> does not throw — it returns an empty list for a fresh athlete.
/// </summary>
public interface IGoalService
{
    /// <summary>The current athlete's goals, ordered by target date ascending (nulls last), each with
    /// computed days-remaining and status (see <see cref="GoalProgress"/>).</summary>
    Task<IReadOnlyList<GoalListItemResponse>> GetAllAsync(CancellationToken ct = default);

    Task<GoalResponse> CreateAsync(GoalDto request, CancellationToken ct = default);
    Task<GoalResponse> UpdateAsync(Guid id, GoalDto request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
