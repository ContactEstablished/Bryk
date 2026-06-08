using Bryk.Domain.Entities;

namespace Bryk.Application.Zones;

// Single source of truth for how many training zones each sport's scheme has: bike = 7-zone power,
// run/swim = 5-zone pace, everything else (Strength, Triathlon) = none. Used by ZoneService for
// override-range checks and by the structured-workout validation to bound a step's TargetZone.
public static class ZoneScheme
{
    public static int Count(Sport sport) => sport switch
    {
        Sport.Bike => 7,
        Sport.Run or Sport.Swim => 5,
        _ => 0
    };
}
