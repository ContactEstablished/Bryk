using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// A per-athlete-per-sport training-zone override (ADR-0004 §1). Rows exist only where the athlete
// has customized a zone away from the value ZoneService computes from their thresholds; absence
// means "use the computed default". AthleteId is denormalized + indexed with no FK to Athlete,
// matching PlannedWorkout/Workout (avoids a SQL Server multiple-cascade-path diamond).
public class AthleteSportZone : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Sport Sport { get; set; }
    public int ZoneNumber { get; set; }
    public ZoneMetric Metric { get; set; }
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
