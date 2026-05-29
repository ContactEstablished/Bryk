using Bryk.Application.Onboarding;

namespace Bryk.Application.Profile;

// Read-side mirror of OnboardingGoalsRequest. Reuses the existing EventDto and GoalDto.
public class ProfileGoalsResponse
{
    public List<EventDto> Events { get; set; } = new();
    public List<GoalDto> Goals { get; set; } = new();
}
