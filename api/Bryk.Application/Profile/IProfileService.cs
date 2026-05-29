namespace Bryk.Application.Profile;

/// <summary>
/// Read-only access to the current athlete's saved onboarding data.
/// Athlete identity is resolved from <see cref="Common.ICurrentUserService"/> — never
/// from a caller parameter. Each method returns null when no Athlete row exists for the
/// current user (i.e. they have not completed onboarding's required step); the controller
/// maps that null to a 404.
/// </summary>
public interface IProfileService
{
    Task<ProfileRequiredResponse?> GetRequiredAsync(CancellationToken ct = default);
    Task<ProfileRecommendedResponse?> GetRecommendedAsync(CancellationToken ct = default);
    Task<ProfileGoalsResponse?> GetGoalsAsync(CancellationToken ct = default);
}
