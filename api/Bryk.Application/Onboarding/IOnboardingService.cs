namespace Bryk.Application.Onboarding;

public interface IOnboardingService
{
    /// <summary>
    /// Submits the required onboarding step. Upsert semantics: creates a new Athlete if none
    /// exists for the current user, or updates the existing athlete's required fields in place.
    /// Does not touch RestingHr, MaxHr, or SportProfiles — those belong to the recommended step.
    /// </summary>
    Task SubmitRequiredAsync(OnboardingRequiredRequest request, CancellationToken ct = default);

    /// <summary>
    /// Submits the recommended onboarding step. Upsert semantics: adds new or updates existing
    /// SportProfiles for the current athlete, and sets heart-rate fields. Throws
    /// <see cref="InvalidOperationException"/> if the required step has not been completed.
    /// </summary>
    Task SubmitRecommendedAsync(OnboardingRecommendedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Submits the goals onboarding step. Append semantics: new events and goals are added
    /// without modifying or deleting existing ones. Throws at the database level (FK constraint)
    /// if the athlete does not yet exist.
    /// </summary>
    Task SubmitGoalsAsync(OnboardingGoalsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns completion status for all three onboarding steps. Required is complete when the
    /// athlete exists (non-nullable required fields make existence equivalent to completion).
    /// Recommended is complete when at least one sport profile exists (HR-only does not count).
    /// Goals is complete when at least one event or goal exists.
    /// </summary>
    Task<OnboardingStatusResponse> GetStatusAsync(CancellationToken ct = default);
}
