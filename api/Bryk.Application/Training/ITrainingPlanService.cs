using Bryk.Application.Calendar;

namespace Bryk.Application.Training;

/// <summary>
/// Authoring surface for the current athlete's training plans. Athlete identity is resolved from
/// <see cref="Common.ICurrentUserService"/> — never from a caller parameter. Planned workouts are
/// edited through the owning plan (ADR-0003 aggregate boundary). Methods throw
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (mapped to 404 by the global
/// exception middleware) when a plan or planned workout does not exist or belongs to another athlete.
/// </summary>
public interface ITrainingPlanService
{
    /// <summary>Creates a plan (and any planned workouts supplied) for the current athlete.</summary>
    Task<TrainingPlanResponse> CreateAsync(TrainingPlanRequest request, CancellationToken ct = default);

    /// <summary>Returns the current athlete's plans (summaries — planned workouts are returned by <see cref="GetByIdAsync"/>).</summary>
    Task<IReadOnlyList<TrainingPlanResponse>> GetByAthleteAsync(CancellationToken ct = default);

    /// <summary>Returns one plan with its planned workouts; 404 if missing or foreign.</summary>
    Task<TrainingPlanResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a planned workout to an owned plan; 404 if the plan is missing or foreign.</summary>
    Task<PlannedWorkoutResponse> AddPlannedWorkoutAsync(Guid planId, PlannedWorkoutDto request, CancellationToken ct = default);

    /// <summary>Updates a planned workout within an owned plan; 404 if the plan or planned workout is missing or foreign.</summary>
    Task<PlannedWorkoutResponse> UpdatePlannedWorkoutAsync(Guid planId, Guid plannedWorkoutId, PlannedWorkoutDto request, CancellationToken ct = default);

    /// <summary>Removes a planned workout from an owned plan; 404 if the plan or planned workout is missing or foreign.</summary>
    Task RemovePlannedWorkoutAsync(Guid planId, Guid plannedWorkoutId, CancellationToken ct = default);

    /// <summary>
    /// Moves a planned workout to a new scheduled date within the owning plan's window
    /// [StartDate, EndDate]. 400 (validation) if the date is outside the window; 404 if the plan or
    /// planned workout is missing or foreign (ADR-0008 §2). Returns Task (204 NoContent).
    /// </summary>
    Task RescheduleAsync(Guid planId, Guid plannedWorkoutId, ScheduleRequest request, CancellationToken ct = default);
}
