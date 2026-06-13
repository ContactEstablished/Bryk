namespace Bryk.Application.Analytics;

// One day of training load. Load is Σ EffectiveLoad (LoadOverride ?? ComputedLoad) across the workouts
// sharing the date, 0 when there are none (zero days are load-bearing for EWMA decay — ADR-0006 §3).
// This is the shared, zero-filled input to PmcCalculator/AcwrCalculator and the daily-load read shape.
public class DailyLoadDto
{
    public DateOnly Date { get; set; }
    public decimal Load { get; set; }
}
