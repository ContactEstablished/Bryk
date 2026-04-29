using FluentValidation;

namespace Bryk.Application.Onboarding.Validators;

public class GoalDtoValidator : AbstractValidator<GoalDto>
{
    public GoalDtoValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.TargetDate)
            .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("TargetDate must be today or in the future.")
            .When(x => x.TargetDate.HasValue);
    }
}
