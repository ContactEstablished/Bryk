using FluentValidation;

namespace Bryk.Application.Training.Workouts;

// Mirrors LogWorkoutRequestValidator rule-for-rule (Task 13-1). Per-step actuals stay nullable
// (partial entry is valid, ADR-0005 §5).
public class UpdateWorkoutRequestValidator : AbstractValidator<UpdateWorkoutRequest>
{
    public UpdateWorkoutRequestValidator()
    {
        RuleFor(x => x.Sport).IsInEnum();

        RuleFor(x => x.CompletedDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("CompletedDate cannot be in the future.");

        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes != null);
        RuleFor(x => x.Rpe).InclusiveBetween(0m, 10m).When(x => x.Rpe.HasValue);
        RuleFor(x => x.LoadOverride).GreaterThanOrEqualTo(0m).When(x => x.LoadOverride.HasValue);
    }
}
