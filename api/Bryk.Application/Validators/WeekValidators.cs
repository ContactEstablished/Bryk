using Bryk.Application.DTOs.Week;
using FluentValidation;

namespace Bryk.Application.Validators;

public class UpdateWeekValidator : AbstractValidator<UpdateWeekDto>
{
    public UpdateWeekValidator()
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
