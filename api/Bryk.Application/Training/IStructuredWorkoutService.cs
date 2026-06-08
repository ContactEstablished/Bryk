namespace Bryk.Application.Training;

/// <summary>
/// Reads and edits a planned workout's structured payload (blocks + steps) through the owning
/// <see cref="Bryk.Domain.Entities.TrainingPlan"/> aggregate (ADR-0004 §4). Athlete identity comes from
/// <see cref="Common.ICurrentUserService"/>; ownership is asserted on the denormalized
/// <see cref="Bryk.Domain.Entities.PlannedWorkout.AthleteId"/> and the route's plan. Methods throw
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (→ 404) when the workout does not exist,
/// belongs to another plan, or belongs to another athlete.
/// </summary>
public interface IStructuredWorkoutService
{
    /// <summary>Returns the planned workout with its blocks and steps (ordered); 404 if missing or foreign.</summary>
    Task<PlannedWorkoutResponse> GetStructureAsync(Guid planId, Guid plannedWorkoutId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the planned workout's structured payload with the supplied blocks/steps and returns it.
    /// 404 if missing or foreign; throws <see cref="Exceptions.ValidationException"/> (→ 400) on invalid input.
    /// </summary>
    Task<PlannedWorkoutResponse> SetStructureAsync(Guid planId, Guid plannedWorkoutId, WorkoutStructureRequest request, CancellationToken ct = default);
}
