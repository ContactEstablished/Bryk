using FluentValidation;

namespace Bryk.Application.Onboarding.Validators;

public class OnboardingRecommendedRequestValidator : AbstractValidator<OnboardingRecommendedRequest>
{
    public OnboardingRecommendedRequestValidator()
    {
        RuleFor(x => x.RestingHr)
            .GreaterThan(0)
            .LessThan(150)
            .When(x => x.RestingHr.HasValue);

        RuleFor(x => x.MaxHr)
            .GreaterThan(0)
            .LessThan(250)
            .When(x => x.MaxHr.HasValue);

        RuleFor(x => x.MaxHr)
            .GreaterThan(x => x.RestingHr!.Value)
            .When(x => x.RestingHr.HasValue && x.MaxHr.HasValue)
            .WithMessage("MaxHr must be greater than RestingHr when both are provided.");

        RuleForEach(x => x.SportThresholds)
            .SetValidator(new SportThresholdsDtoValidator());
    }
}
