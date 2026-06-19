using FluentValidation;

namespace Bryk.Application.Calendar.Validators;

public sealed class ScheduleRequestValidator : AbstractValidator<ScheduleRequest>
{
    public ScheduleRequestValidator()
    {
        // DateOnly is a non-nullable struct, so NotEmpty/NotNull are no-ops; the meaningful
        // rule (plan-window) can't run here — the validator can't see the plan. The window check
        // lives in the service, which throws ValidationException after loading the plan.
        // This validator exists for shape consistency.
    }
}
