using FluentValidation;

namespace Bryk.Application.Onboarding.Validators;

public class OnboardingRequiredRequestValidator : AbstractValidator<OnboardingRequiredRequest>
{
    public OnboardingRequiredRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Gender)
            .IsInEnum();

        RuleFor(x => x.DateOfBirth)
            .GreaterThan(new DateOnly(1900, 1, 1))
            .Must(dob =>
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var age = today.Year - dob.Year;
                if (dob > today.AddYears(-age))
                    age--;
                return age >= 13;
            })
            .WithMessage("Athlete must be at least 13 years old.");

        RuleFor(x => x.HeightCm)
            .GreaterThan(0m)
            .LessThan(300m);

        RuleFor(x => x.WeightKg)
            .GreaterThan(0m)
            .LessThan(500m);

        RuleFor(x => x.YearsTraining)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(80);

        RuleFor(x => x.TypicalWeeklyHours)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(168m);

        RuleFor(x => x.Methodology)
            .IsInEnum();
    }
}
