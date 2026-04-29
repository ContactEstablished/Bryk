using FluentValidation;

namespace Bryk.Application.Onboarding.Validators;

public class OnboardingGoalsRequestValidator : AbstractValidator<OnboardingGoalsRequest>
{
    public OnboardingGoalsRequestValidator()
    {
        RuleForEach(x => x.Events)
            .SetValidator(new EventDtoValidator());

        RuleForEach(x => x.Goals)
            .SetValidator(new GoalDtoValidator());
    }
}
