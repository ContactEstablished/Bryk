using Bryk.Application.Onboarding;

namespace Bryk.Application.Goals;

/// <summary>
/// Per-item create / update / delete for the current athlete's goals.
/// Athlete identity is resolved from <see cref="Common.ICurrentUserService"/> — never from
/// a caller parameter. <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/> throw
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (mapped to 404 by the
/// global exception middleware) when the goal does not exist or belongs to another athlete.
/// </summary>
public interface IGoalService
{
    Task<GoalResponse> CreateAsync(GoalDto request, CancellationToken ct = default);
    Task<GoalResponse> UpdateAsync(Guid id, GoalDto request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
