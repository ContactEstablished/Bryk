using FluentValidation;

namespace Bryk.Application.Training.Workouts;

public class LogWorkoutRequestValidator : AbstractValidator<LogWorkoutRequest>
{
    public LogWorkoutRequestValidator()
    {
        RuleFor(x => x.Sport).IsInEnum();

        RuleFor(x => x.CompletedDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("CompletedDate cannot be in the future.");

        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes != null);
        RuleFor(x => x.Rpe).InclusiveBetween(0m, 10m).When(x => x.Rpe.HasValue);
        RuleFor(x => x.LoadOverride).GreaterThanOrEqualTo(0m).When(x => x.LoadOverride.HasValue);
        // Per-step actuals are intentionally nullable (partial entry is valid, ADR-0005 §5).
    }
}
