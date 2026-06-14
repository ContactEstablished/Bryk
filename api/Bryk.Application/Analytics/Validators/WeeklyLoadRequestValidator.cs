using FluentValidation;

namespace Bryk.Application.Analytics.Validators;

// Weeks span for the weekly-load endpoint (ADR-0007 §3): 1–26.
public class WeeklyLoadRequestValidator : AbstractValidator<WeeklyLoadRequest>
{
    public WeeklyLoadRequestValidator()
    {
        RuleFor(x => x.Weeks)
            .InclusiveBetween(1, 26).WithMessage("weeks must be between 1 and 26.");
    }
}
