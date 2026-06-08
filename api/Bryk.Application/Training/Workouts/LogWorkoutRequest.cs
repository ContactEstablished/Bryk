using Bryk.Domain.Entities;

namespace Bryk.Application.Training.Workouts;

// Write-side: log a completed workout (ADR-0005 §4). PlannedWorkoutId is optional (unplanned sessions
// are first-class); session + per-step actuals are all nullable so partial manual entry is valid and a
// post-v1 device importer fills the rest.
public class LogWorkoutRequest
{
    public Sport Sport { get; set; }
    public DateOnly CompletedDate { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public int? ActualDurationSeconds { get; set; }
    public int? ActualDistanceMeters { get; set; }
    public int? AvgHr { get; set; }
    public int? MaxHr { get; set; }
    public decimal? LoadOverride { get; set; }
    public decimal? Rpe { get; set; }
    public string? Notes { get; set; }
    public List<WorkoutStepResultDto>? StepResults { get; set; }
}

// Order is positional within the request. WorkoutStepId optionally links to the planned step realized.
public class WorkoutStepResultDto
{
    public Guid? WorkoutStepId { get; set; }
    public int? ActualDurationSeconds { get; set; }
    public int? ActualDistanceMeters { get; set; }
    public int? AvgPower { get; set; }
    public int? AvgHr { get; set; }
    public int? AvgPace { get; set; }
    public decimal? Rpe { get; set; }
}
