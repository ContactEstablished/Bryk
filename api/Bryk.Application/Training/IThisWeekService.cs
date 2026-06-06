namespace Bryk.Application.Training;

/// <summary>
/// Read-only projection of the current athlete's planned workouts for the current week, shaped for
/// the dashboard This Week card. Athlete identity is resolved from <see cref="Common.ICurrentUserService"/>.
/// Dedicated read service (no <see cref="Domain.Interfaces.IUnitOfWork"/>) mirroring ProfileService,
/// keeping the authoring <see cref="ITrainingPlanService"/> focused. Returns an empty list (never 404)
/// when the athlete has nothing planned this week.
/// </summary>
public interface IThisWeekService
{
    Task<ThisWeekResponse> GetThisWeekAsync(CancellationToken ct = default);
}
