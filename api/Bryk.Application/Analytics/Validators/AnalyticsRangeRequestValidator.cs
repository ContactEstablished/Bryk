using FluentValidation;

namespace Bryk.Application.Analytics.Validators;

/// <summary>
/// Range rules for the analytics endpoints (ADR-0006 §7): both bounds required, <c>from ≤ to</c>, span
/// ≤ 400 days, and <c>to</c> not in the future. "Today" is <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>,
/// the same source the event validators / ThisWeekService use.
/// </summary>
public class AnalyticsRangeRequestValidator : AbstractValidator<AnalyticsRangeRequest>
{
    private const int MaxRangeDays = 400;

    public AnalyticsRangeRequestValidator()
    {
        RuleFor(x => x.From)
            .NotNull().WithMessage("from is required.");

        RuleFor(x => x.To)
            .NotNull().WithMessage("to is required.");

        When(x => x.From.HasValue && x.To.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.From!.Value <= x.To!.Value)
                .WithMessage("from must be on or before to.");

            RuleFor(x => x)
                .Must(x => x.To!.Value.DayNumber - x.From!.Value.DayNumber <= MaxRangeDays)
                .WithMessage($"range cannot exceed {MaxRangeDays} days.");

            RuleFor(x => x.To)
                .Must(to => to!.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("to cannot be in the future.");
        });
    }
}
