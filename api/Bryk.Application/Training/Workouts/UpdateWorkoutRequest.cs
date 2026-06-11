using Bryk.Domain.Entities;

namespace Bryk.Application.Training.Workouts;

// Replace-style edit of a completed workout (Task 13-1). Same writable shape as LogWorkoutRequest:
// the session actuals and the whole step-result list are replaced. ComputedLoad is recomputed from
// the supplied actuals on every update; LoadOverride is written through verbatim (the edit form
// pre-fills it, so it survives a round-trip; blanking the field clears the override).
public class UpdateWorkoutRequest
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
