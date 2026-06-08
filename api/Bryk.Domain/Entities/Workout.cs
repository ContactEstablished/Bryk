using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Workout : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public Sport Sport { get; set; }
    public DateOnly CompletedDate { get; set; }

    // Session-level execution actuals (ADR-0005 §4). All nullable — manual entry stays light.
    public int? ActualDurationSeconds { get; set; }
    public int? ActualDistanceMeters { get; set; }
    public int? AvgHr { get; set; }
    public int? MaxHr { get; set; }
    public decimal? ComputedLoad { get; set; }
    public decimal? LoadOverride { get; set; }
    public decimal? Rpe { get; set; }
    public string? Notes { get; set; }

    public ICollection<WorkoutStepResult> StepResults { get; set; } = new List<WorkoutStepResult>();
    public PlannedWorkout? PlannedWorkout { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
