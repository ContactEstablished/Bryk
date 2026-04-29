namespace Bryk.Application.Onboarding;

public class OnboardingRecommendedRequest
{
    public int? RestingHr { get; set; }
    public int? MaxHr { get; set; }
    public List<SportThresholdsDto> SportThresholds { get; set; } = new();
}
