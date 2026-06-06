using FluentValidation;

namespace Bryk.Application.Zones.Validators;

// Sport-agnostic per-band rules. The sport-specific zone-number ceiling (7 for bike, 5 for
// run/swim) and the threshold-exists check live in ZoneService, which knows the route's sport.
public class ZoneOverrideRequestValidator : AbstractValidator<ZoneOverrideRequest>
{
    public ZoneOverrideRequestValidator()
    {
        RuleForEach(x => x.Zones).ChildRules(zone =>
        {
            zone.RuleFor(b => b.ZoneNumber).GreaterThanOrEqualTo(1);
            zone.RuleFor(b => b.LowerBound).GreaterThanOrEqualTo(0);
            zone.RuleFor(b => b.UpperBound)
                .GreaterThan(b => b.LowerBound)
                .When(b => b.UpperBound.HasValue);
        });
    }
}
