using Bryk.Application.DTOs.Exercise;
using FluentValidation;

namespace Bryk.Application.Validators;

public class CreateExerciseValidator : AbstractValidator<CreateExerciseDto>
{
    private static readonly string[] ValidSportTypes = ["Bike", "Run", "Swim", "Strength", "Other"];

    public CreateExerciseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.SportType)
            .NotEmpty()
            .Must(v => ValidSportTypes.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SportType must be one of: {string.Join(", ", ValidSportTypes)}");

        RuleFor(x => x.TssValue)
            .InclusiveBetween(1, 5000);

        When(x => x.DurationMinutes.HasValue, () =>
        {
            RuleFor(x => x.DurationMinutes!.Value)
                .InclusiveBetween(1, 1440);
        });

        When(x => !string.IsNullOrEmpty(x.IntensityZone), () =>
        {
            RuleFor(x => x.IntensityZone)
                .MaximumLength(50);
        });

        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(1000);
        });
    }
}

public class UpdateExerciseValidator : AbstractValidator<UpdateExerciseDto>
{
    private static readonly string[] ValidSportTypes = ["Bike", "Run", "Swim", "Strength", "Other"];

    public UpdateExerciseValidator()
    {
        When(x => !string.IsNullOrEmpty(x.Name), () =>
        {
            RuleFor(x => x.Name)
                .MaximumLength(200);
        });

        When(x => !string.IsNullOrEmpty(x.SportType), () =>
        {
            RuleFor(x => x.SportType)
                .Must(v => ValidSportTypes.Contains(v, StringComparer.OrdinalIgnoreCase))
                .WithMessage($"SportType must be one of: {string.Join(", ", ValidSportTypes)}");
        });

        When(x => x.TssValue.HasValue, () =>
        {
            RuleFor(x => x.TssValue!.Value)
                .InclusiveBetween(1, 5000);
        });

        When(x => x.DurationMinutes.HasValue, () =>
        {
            RuleFor(x => x.DurationMinutes!.Value)
                .InclusiveBetween(1, 1440);
        });

        When(x => !string.IsNullOrEmpty(x.IntensityZone), () =>
        {
            RuleFor(x => x.IntensityZone)
                .MaximumLength(50);
        });

        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(1000);
        });
    }
}
