using FluentValidation;

namespace Bryk.Application.Training.Validators;

public class TrainingPlanRequestValidator : AbstractValidator<TrainingPlanRequest>
{
    public TrainingPlanRequestValidator()
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
            .GreaterThan(0)
            .When(x => x.BuildWeeks.HasValue);

        RuleFor(x => x.RecoveryWeeks)
            .GreaterThan(0)
            .When(x => x.RecoveryWeeks.HasValue);

        RuleFor(x => x.RecoveryWeekPercentage)
            .InclusiveBetween(0m, 100m)
            .When(x => x.RecoveryWeekPercentage.HasValue);

        RuleForEach(x => x.PlannedWorkouts)
            .SetValidator(new PlannedWorkoutDtoValidator())
            .When(x => x.PlannedWorkouts is not null);
    }
}
