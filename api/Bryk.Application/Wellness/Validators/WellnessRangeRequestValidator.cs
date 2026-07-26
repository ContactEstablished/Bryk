using FluentValidation;

namespace Bryk.Application.Wellness.Validators;

/// <summary>
/// Range rules for <c>GET /wellness</c>: both bounds required, <c>from ≤ to</c>, span ≤ 400 days, and
/// <c>to</c> not in the future. Mirrors <see cref="Analytics.Validators.AnalyticsRangeRequestValidator"/>
/// member-for-member — same bound, same messages, same source of "today".
/// </summary>
public class WellnessRangeRequestValidator : AbstractValidator<WellnessRangeRequest>
{
    private const int MaxRangeDays = 400;

    public WellnessRangeRequestValidator()
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
