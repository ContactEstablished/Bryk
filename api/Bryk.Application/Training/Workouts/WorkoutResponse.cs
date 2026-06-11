using Bryk.Domain.Entities;

namespace Bryk.Application.Training.Workouts;

// Read-side, Id-bearing. EffectiveLoad = LoadOverride ?? ComputedLoad; IsLoadOverride when LoadOverride set.
public class WorkoutResponse
{
    public Guid Id { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    // Plan that owns the linked PlannedWorkout. Populated only on the single-workout detail read so the
    // client can reach GET .../structure for planned-vs-actual; null on list reads and unlinked workouts.
    public Guid? TrainingPlanId { get; set; }
    public Sport Sport { get; set; }
    public DateOnly CompletedDate { get; set; }
    public int? ActualDurationSeconds { get; set; }
    public int? ActualDistanceMeters { get; set; }
    public int? AvgHr { get; set; }
    public int? MaxHr { get; set; }
    public decimal? ComputedLoad { get; set; }
    public decimal? LoadOverride { get; set; }
    public decimal? EffectiveLoad { get; set; }
    public bool IsLoadOverride { get; set; }
    public decimal? Rpe { get; set; }
    public string? Notes { get; set; }
    public List<WorkoutStepResultResponse> StepResults { get; set; } = new();
}

public class WorkoutStepResultResponse
{
    public Guid Id { get; set; }
    public Guid? WorkoutStepId { get; set; }
    public int OrderIndex { get; set; }
    public int? ActualDurationSeconds { get; set; }
    public int? ActualDistanceMeters { get; set; }
    public int? AvgPower { get; set; }
    public int? AvgHr { get; set; }
    public int? AvgPace { get; set; }
    public decimal? Rpe { get; set; }
}
