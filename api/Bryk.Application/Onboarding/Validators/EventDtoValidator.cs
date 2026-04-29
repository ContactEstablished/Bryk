using FluentValidation;

namespace Bryk.Application.Onboarding.Validators;

public class EventDtoValidator : AbstractValidator<EventDto>
{
    public EventDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.EventDate)
            .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("EventDate must be today or in the future.");

        RuleFor(x => x.Sport)
            .IsInEnum()
            .When(x => x.Sport.HasValue);

        RuleFor(x => x.TriathlonDistance)
            .IsInEnum()
            .When(x => x.TriathlonDistance.HasValue);

        RuleFor(x => x.Priority)
            .IsInEnum();

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => x.Notes != null);
    }
}
