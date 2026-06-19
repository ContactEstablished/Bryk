using FluentValidation;

namespace Bryk.Application.Calendar;

/// <summary>
/// Range rules for the calendar feed (ADR-0008 §1): both bounds required (after the service applies
/// defaults), <c>from ≤ to</c>, span ≤ 62 days. <c>to</c> may be in the future — the calendar shows
/// future planned workouts.
/// </summary>
public class CalendarFeedRequestValidator : AbstractValidator<CalendarFeedRequest>
{
    private const int MaxRangeDays = 62;

    public CalendarFeedRequestValidator()
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
                .Must(x => x.To!.Value.DayNumber - x.From!.Value.DayNumber + 1 <= MaxRangeDays)
                .WithMessage("range must be 62 days or fewer.");
        });
    }
}
