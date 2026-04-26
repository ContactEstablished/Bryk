using Bryk.Application.DTOs.Day;
using FluentValidation;

namespace Bryk.Application.Validators;

public class UpdateDayValidator : AbstractValidator<UpdateDayDto>
{
    public UpdateDayValidator()
    {
        When(x => x.TargetTss.HasValue, () =>
        {
            RuleFor(x => x.TargetTss!.Value)
                .InclusiveBetween(1, 5000);
        });

        When(x => x.Notes is not null, () =>
        {
            RuleFor(x => x.Notes)
                .MaximumLength(1000);
        });
    }
}

public class UpdateDayExerciseValidator : AbstractValidator<UpdateDayExerciseDto>
{
    public UpdateDayExerciseValidator()
    {
        When(x => x.ActualTss.HasValue, () =>
        {
            RuleFor(x => x.ActualTss!.Value)
                .InclusiveBetween(0, 5000);
        });

        When(x => x.CaloriesBurned.HasValue, () =>
        {
            RuleFor(x => x.CaloriesBurned!.Value)
                .InclusiveBetween(0, 10000);
        });

        When(x => x.AverageHeartRate.HasValue, () =>
        {
            RuleFor(x => x.AverageHeartRate!.Value)
                .InclusiveBetween(30, 250);
        });

        When(x => x.MaxHeartRate.HasValue, () =>
        {
            RuleFor(x => x.MaxHeartRate!.Value)
                .InclusiveBetween(30, 250);
        });

        When(x => x.OrderIndex.HasValue, () =>
        {
            RuleFor(x => x.OrderIndex!.Value)
                .GreaterThanOrEqualTo(0);
        });
    }
}
