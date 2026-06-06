using FluentValidation;

namespace Bryk.Application.Training.Validators;

public class PlannedWorkoutDtoValidator : AbstractValidator<PlannedWorkoutDto>
{
    public PlannedWorkoutDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description != null);

        RuleFor(x => x.Sport)
            .IsInEnum();

        RuleFor(x => x.ScheduledDate)
            .Must(date => date != default)
            .WithMessage("ScheduledDate is required.");

        RuleFor(x => x.PlannedDurationMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PlannedDurationMinutes.HasValue);

        RuleFor(x => x.PlannedLoad)
            .GreaterThanOrEqualTo(0m)
            .When(x => x.PlannedLoad.HasValue);
    }
}
