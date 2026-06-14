namespace Bryk.Application.Analytics;

// One ISO week of training load (ADR-0007 §3). PlannedLoad = Σ scheduled PlannedWorkout EffectiveLoad;
// ActualLoad = Σ completed Workout EffectiveLoad; RollingAverage = trailing 4-week mean of ActualLoad.
public class WeeklyLoadWeekDto
{
    public DateOnly WeekStart { get; set; }
    public decimal PlannedLoad { get; set; }
    public decimal ActualLoad { get; set; }
    public decimal RollingAverage { get; set; }
}

// The optimal weekly-load band (ADR-0007 §1): [0.8, 1.3] × the trailing 4-week mean actual load. Null
// (omitted) for a fresh athlete with no actual load — honest, not a [0,0] band that reads as a real range.
public class OptimalBandDto
{
    public decimal Lower { get; set; }
    public decimal Upper { get; set; }
}

// The weekly-load read shape: the N ISO weeks (oldest → newest) plus the single horizontal optimal band.
public class WeeklyLoadResponse
{
    public IReadOnlyList<WeeklyLoadWeekDto> Weeks { get; set; } = new List<WeeklyLoadWeekDto>();
    public OptimalBandDto? OptimalBand { get; set; }
}
