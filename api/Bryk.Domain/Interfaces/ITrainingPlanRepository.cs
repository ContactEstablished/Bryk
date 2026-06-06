using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the <see cref="TrainingPlan"/> aggregate. <see cref="PlannedWorkout"/>
/// children are edited through the plan; the service verifies ownership on the plan and keeps
/// <see cref="PlannedWorkout.AthleteId"/> in sync with the plan's owner before staging.
/// </summary>
public interface ITrainingPlanRepository
{
    /// <summary>
    /// Loads a single <see cref="TrainingPlan"/> with its <see cref="TrainingPlan.PlannedWorkouts"/>
    /// included. Uses no-tracking since the caller uses the result for display and ownership checks;
    /// mutations are staged through the explicit staging methods below.
    /// </summary>
    Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="TrainingPlan"/> entities for a given athlete, ordered by
    /// <see cref="TrainingPlan.StartDate"/> ascending. Entity only, no includes. Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="TrainingPlan"/> (with any seeded <see cref="PlannedWorkout"/> children)
    /// for insertion. Does NOT call SaveChanges.
    /// </summary>
    Task AddAsync(TrainingPlan entity, CancellationToken ct = default);

    /// <summary>
    /// Stages an existing <see cref="TrainingPlan"/> for update. Does NOT call SaveChanges.
    /// </summary>
    void Update(TrainingPlan entity);

    /// <summary>
    /// Stages an existing <see cref="TrainingPlan"/> for deletion. Its <see cref="PlannedWorkout"/>
    /// children cascade. Does NOT call SaveChanges.
    /// </summary>
    void Delete(TrainingPlan entity);

    /// <summary>
    /// Stages a new <see cref="PlannedWorkout"/> for insertion under its parent plan. Does NOT call SaveChanges.
    /// </summary>
    Task AddPlannedWorkoutAsync(PlannedWorkout plannedWorkout, CancellationToken ct = default);

    /// <summary>
    /// Stages an existing <see cref="PlannedWorkout"/> for update. Does NOT call SaveChanges.
    /// </summary>
    void UpdatePlannedWorkout(PlannedWorkout plannedWorkout);

    /// <summary>
    /// Stages an existing <see cref="PlannedWorkout"/> for deletion. Does NOT call SaveChanges.
    /// </summary>
    void RemovePlannedWorkout(PlannedWorkout plannedWorkout);
}
