using System.Text.Json;
using FluentValidation;

namespace Bryk.Application.Onboarding.Validators;

public class SportThresholdsDtoValidator : AbstractValidator<SportThresholdsDto>
{
    public SportThresholdsDtoValidator()
    {
        RuleFor(x => x.Sport)
            .IsInEnum();

        RuleFor(x => x.ThresholdValue)
            .GreaterThan(0m)
            .When(x => x.ThresholdValue.HasValue);

        RuleFor(x => x.Lt1)
            .GreaterThan(0m)
            .When(x => x.Lt1.HasValue);

        RuleFor(x => x.Lt2)
            .GreaterThan(0m)
            .When(x => x.Lt2.HasValue);

        RuleFor(x => x.Lt2)
            .GreaterThan(x => x.Lt1!.Value)
            .When(x => x.Lt1.HasValue && x.Lt2.HasValue)
            .WithMessage("Lt2 must be greater than Lt1 when both are provided.");

        RuleFor(x => x.CustomZonesJson)
            .Must(json =>
            {
                if (string.IsNullOrWhiteSpace(json))
                    return true;

                try
                {
                    JsonDocument.Parse(json);
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            })
            .WithMessage("CustomZonesJson must be valid JSON.")
            .When(x => x.CustomZonesJson != null);
    }
}
