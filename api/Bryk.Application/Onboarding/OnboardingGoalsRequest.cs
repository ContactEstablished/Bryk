namespace Bryk.Application.Onboarding;

public class OnboardingGoalsRequest
{
    public List<EventDto> Events { get; set; } = new();
    public List<GoalDto> Goals { get; set; } = new();
}
