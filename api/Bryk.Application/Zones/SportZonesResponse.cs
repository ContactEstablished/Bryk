using Bryk.Domain.Entities;

namespace Bryk.Application.Zones;

// One sport's effective zones (computed from thresholds, with overrides applied) and the metric
// they are expressed in (Power for bike, Pace for run/swim).
public class SportZonesResponse
{
    public Sport Sport { get; set; }
    public ZoneMetric Metric { get; set; }
    public IReadOnlyList<ZoneDto> Zones { get; set; } = new List<ZoneDto>();
}
