using FluentValidation;

namespace Bryk.Application.Wellness.Validators;

/// <summary>
/// Entry rules for a single wellness day. Every bound is the ROADMAP's Phase 20 number, inclusive, and
/// only applies when the metric is present — partial entries are the norm.
///
/// The two <c>Date</c> rules are not decorative. <c>Program.cs:32–33</c> sets
/// <c>SuppressModelStateInvalidFilter = true</c>, so a route segment that fails to bind produces no
/// 400: the parameter silently arrives as <c>default(DateOnly)</c> and the action still executes. The
/// <c>{date:datetime}</c> route constraint on <c>WellnessController.PutAsync</c> is the first line of
/// defence (a non-date segment 404s before binding); this validator is the second (a well-formed
/// segment that still fails <c>DateOnly</c> binding arrives as <c>0001-01-01</c> and is rejected 400).
/// Neither layer alone is sufficient.
///
/// Every message names its own field because
/// <see cref="Common.Validation.ValidationExtensions.ValidateOrThrowAsync{T}"/> collects
/// <c>ErrorMessage</c> only and drops the property name (the ActivityFileUploadRequestValidator
/// convention).
/// </summary>
public class WellnessEntryRequestValidator : AbstractValidator<WellnessEntryRequest>
{
    public WellnessEntryRequestValidator()
    {
        RuleFor(x => x.Date)
            .Must(d => d != default)
            .WithMessage("Date: A valid date is required (yyyy-MM-dd).");

        // Guarded on "not default" so a default(DateOnly) produces ONE message (the "valid date" one),
        // not two.
        RuleFor(x => x.Date)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date: A wellness entry cannot be in the future.")
            .When(x => x.Date != default);

        RuleFor(x => x.SleepHours)
            .InclusiveBetween(0m, 16m)
            .WithMessage("SleepHours: Sleep must be between 0 and 16 hours.")
            .When(x => x.SleepHours.HasValue);

        RuleFor(x => x.SleepQuality)
            .InclusiveBetween(1, 5)
            .WithMessage("SleepQuality: Sleep quality must be between 1 and 5.")
            .When(x => x.SleepQuality.HasValue);

        RuleFor(x => x.RestingHr)
            .InclusiveBetween(25, 120)
            .WithMessage("RestingHr: Resting HR must be between 25 and 120 bpm.")
            .When(x => x.RestingHr.HasValue);

        RuleFor(x => x.WeightKg)
            .InclusiveBetween(30m, 250m)
            .WithMessage("WeightKg: Weight must be between 30 and 250 kg.")
            .When(x => x.WeightKg.HasValue);

        RuleFor(x => x.Soreness)
            .InclusiveBetween(1, 10)
            .WithMessage("Soreness: Soreness must be between 1 and 10.")
            .When(x => x.Soreness.HasValue);

        RuleFor(x => x.HrvMs)
            .InclusiveBetween(10, 250)
            .WithMessage("HrvMs: HRV must be between 10 and 250 ms.")
            .When(x => x.HrvMs.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes: Notes must be 1000 characters or fewer.")
            .When(x => x.Notes != null);

        RuleFor(x => x)
            .Must(HasAtLeastOneMetric)
            .WithMessage("Entry: At least one metric is required.");
    }

    // Notes deliberately does NOT count as a metric: a row carrying only prose contributes to no tile
    // and no average, and the ROADMAP's rule is ">= 1 metric present".
    private static bool HasAtLeastOneMetric(WellnessEntryRequest r) =>
        r.SleepHours.HasValue
        || r.SleepQuality.HasValue
        || r.RestingHr.HasValue
        || r.WeightKg.HasValue
        || r.Soreness.HasValue
        || r.HrvMs.HasValue;
}
