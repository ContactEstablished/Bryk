namespace Bryk.Application.Analytics;

// Seconds spent in one coarse intensity bucket (ADR-0007 §4). ZoneNumber is 1..5 (the lowest common
// denominator across the sports' zone schemes; bike Z6/Z7 collapse to 5).
public class ZoneTimeDto
{
    public int ZoneNumber { get; set; }
    public int Seconds { get; set; }
}

// How the histogram's seconds were derived (ADR-0007 §4): planned structure for linked workouts, coarse
// session AvgHr otherwise, else unclassified. The three sum to TotalSeconds — the honest provenance behind
// the always-"estimated" badge (no sample-derived zone time until Phase 19 file import).
public class ZoneTimeMethodBreakdownDto
{
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
