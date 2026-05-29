using Bryk.Application.Onboarding;

namespace Bryk.Application.Profile;

// Read-side mirror of OnboardingRecommendedRequest. Reuses the existing SportThresholdsDto.
public class ProfileRecommendedResponse
{
    public int? RestingHr { get; set; }
    public int? MaxHr { get; set; }
    public List<SportThresholdsDto> SportThresholds { get; set; } = new();
}
