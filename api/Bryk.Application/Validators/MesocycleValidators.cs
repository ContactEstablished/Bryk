using Bryk.Application.DTOs.Mesocycle;
using FluentValidation;

namespace Bryk.Application.Validators;

public class CreateMesocycleValidator : AbstractValidator<CreateMesocycleDto>
{
    private static readonly HashSet<string> AllowedPatterns = ["Polarized", "Pyramidal", "Sweet Spot"];

    public CreateMesocycleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(DateTime.Today)
            .WithMessage("Start date must be today or in the future.");

        RuleFor(x => x.NumberOfWeeks)
            .InclusiveBetween(1, 52);

        RuleFor(x => x.StartingWeeklyTss)
            .InclusiveBetween(1, 5000);

        RuleFor(x => x.WeeklyIncreasePercentage)
            .InclusiveBetween(0, 20);

        RuleFor(x => x.BuildRecoveryRatio)
            .NotEmpty()
            .Matches(@"^\d:\d$")
            .WithMessage("Build recovery ratio must match the pattern 'n:n' (e.g. '3:1').");

        RuleFor(x => x.RecoveryWeekPercentage)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.WeeklyPatternType)
            .NotEmpty()
            .Must(x => AllowedPatterns.Contains(x))
            .WithMessage("Weekly pattern type must be one of: Polarized, Pyramidal, Sweet Spot.");
    }
}

public class UpdateMesocycleValidator : AbstractValidator<UpdateMesocycleDto>
{
    private static readonly HashSet<string> AllowedPatterns = ["Polarized", "Pyramidal", "Sweet Spot"];

    public UpdateMesocycleValidator()
    {
        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name)
                .MaximumLength(200);
        });

        When(x => x.NumberOfWeeks.HasValue, () =>
        {
            RuleFor(x => x.NumberOfWeeks!.Value)
                .InclusiveBetween(1, 52);
        });

        When(x => x.StartingWeeklyTss.HasValue, () =>
        {
            RuleFor(x => x.StartingWeeklyTss!.Value)
                .InclusiveBetween(1, 5000);
        });

        When(x => x.WeeklyIncreasePercentage.HasValue, () =>
        {
            RuleFor(x => x.WeeklyIncreasePercentage!.Value)
                .InclusiveBetween(0, 20);
        });

        When(x => x.BuildRecoveryRatio is not null, () =>
        {
            RuleFor(x => x.BuildRecoveryRatio)
                .Matches(@"^\d:\d$")
                .WithMessage("Build recovery ratio must match the pattern 'n:n' (e.g. '3:1').");
        });

        When(x => x.RecoveryWeekPercentage.HasValue, () =>
        {
            RuleFor(x => x.RecoveryWeekPercentage!.Value)
                .InclusiveBetween(1, 100);
        });

        When(x => x.WeeklyPatternType is not null, () =>
        {
            RuleFor(x => x.WeeklyPatternType)
                .Must(x => AllowedPatterns.Contains(x))
                .WithMessage("Weekly pattern type must be one of: Polarized, Pyramidal, Sweet Spot.");
        });
    }
}
