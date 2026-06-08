using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// A per-step actual captured for an executed Workout (ADR-0005 §5). Every actual field is nullable so
// manual entry stays light; a post-v1 device importer fills them. WorkoutStepId optionally references
// the planned WorkoutStep it realizes — NoAction on delete so editing/deleting a plan never touches
// completed execution history. AthleteId is denormalized + indexed with no FK (ADR-0003/0004).
public class WorkoutStepResult : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Guid WorkoutId { get; set; }
    public Guid? WorkoutStepId { get; set; }
    public int OrderIndex { get; set; }

    public int? ActualDurationSeconds { get; set; }
    public int? ActualDistanceMeters { get; set; }
    public int? AvgPower { get; set; }
    public int? AvgHr { get; set; }
    public int? AvgPace { get; set; }
    public decimal? Rpe { get; set; }

    public Workout Workout { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
