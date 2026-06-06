namespace Bryk.Application.Zones;

// One training zone's numeric band. UpperBound null = open-ended top (power Z7) or, for pace,
// the open-ended slow end (Z1). IsOverride tells the UI whether this band is the computed default
// or a saved athlete override (so it can offer "reset to computed").
public class ZoneDto
{
    public int ZoneNumber { get; set; }
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public bool IsOverride { get; set; }
}
