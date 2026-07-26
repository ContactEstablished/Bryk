namespace Bryk.Application.Analytics;

// Seconds spent in one coarse intensity bucket (ADR-0007 §4). ZoneNumber is 1..5 (the lowest common
// denominator across the sports' zone schemes; bike Z6/Z7 collapse to 5).
public class ZoneTimeDto
{
    public int ZoneNumber { get; set; }
    public int Seconds { get; set; }
}

// How the histogram's seconds were derived, in precedence order (ADR-0007 §4, ADR-0010 §5): an imported
// file's measured per-zone histogram first, then planned structure for linked workouts, then coarse
// session AvgHr, else unclassified. The four sum to TotalSeconds. SampleSeconds is the only one that is
// measured rather than estimated — it comes from a device file's samples bucketed against the athlete's
// own zones at commit — and it is what lets the UI's badge stop saying "estimated".
public class ZoneTimeMethodBreakdownDto
{
    public int SampleSeconds { get; set; }
    public int StructureSeconds { get; set; }
    public int SessionAvgSeconds { get; set; }
    public int UnclassifiedSeconds { get; set; }
}

// The time-in-zone read shape: a 5-bucket intensity histogram in seconds + the method breakdown + total.
public class TimeInZoneResponse
{
    public IReadOnlyList<ZoneTimeDto> Zones { get; set; } = new List<ZoneTimeDto>();
    public ZoneTimeMethodBreakdownDto MethodBreakdown { get; set; } = new();
    public int TotalSeconds { get; set; }
}
