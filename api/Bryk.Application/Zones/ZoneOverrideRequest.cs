namespace Bryk.Application.Zones;

// Replaces the athlete's saved overrides for one sport's primary metric. The sport comes from the
// route and the metric is derived from it; an empty Zones list clears the overrides (the sport
// falls back to its computed zones).
public class ZoneOverrideRequest
{
    public List<ZoneBoundDto> Zones { get; set; } = new();
}

public class ZoneBoundDto
{
    public int ZoneNumber { get; set; }
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
}
