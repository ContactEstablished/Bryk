using FluentValidation;

namespace Bryk.Application.Training.Validators;

public class TrainingPlanUpdateRequestValidator : AbstractValidator<TrainingPlanUpdateRequest>
{
    public TrainingPlanUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Methodology)
            .IsInEnum();

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.BuildWeeks)
            .InclusiveBetween(1, 8)
            .When(x => x.BuildWeeks.HasValue);

        RuleFor(x => x.RecoveryWeeks)
            .GreaterThanOrEqualTo(1)
            .When(x => x.RecoveryWeeks.HasValue);

        RuleFor(x => x.RecoveryWeekPercentage)
            .InclusiveBetween(30m, 90m)
            .When(x => x.RecoveryWeekPercentage.HasValue);
    }
}
