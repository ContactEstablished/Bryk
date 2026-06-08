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
    /// Returns the athlete's <see cref="PlannedWorkout"/>s whose <see cref="PlannedWorkout.ScheduledDate"/>
    /// falls within [start, end] inclusive, across all the athlete's plans, ordered by date then sport.
    /// Single-table query on the denormalized, indexed <see cref="PlannedWorkout.AthleteId"/> (ADR-0003) —
    /// one round-trip, no join. Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default);

    /// <summary>
    /// Same range filter as <see cref="GetPlannedWorkoutsInRangeAsync"/> but with each workout's
    /// <see cref="PlannedWorkout.Blocks"/> (and their steps) included (split query, no-tracking), for
    /// callers that compute training load over the week (Task 11-2).
    /// </summary>
    Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default);

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

    /// <summary>
    /// Loads a single <see cref="PlannedWorkout"/> with its structured payload —
    /// <see cref="PlannedWorkout.Blocks"/> ordered by <see cref="WorkoutBlock.OrderIndex"/>, each with its
    /// <see cref="WorkoutBlock.Steps"/> ordered by <see cref="WorkoutStep.OrderIndex"/>. No-tracking, split
    /// query; the caller uses the result for display and ownership checks and stages mutations explicitly.
    /// </summary>
    Task<PlannedWorkout?> GetPlannedWorkoutWithStructureAsync(Guid plannedWorkoutId, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="WorkoutBlock"/> for insertion. Does NOT call SaveChanges.</summary>
    Task AddWorkoutBlockAsync(WorkoutBlock block, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="WorkoutBlock"/> for update. Does NOT call SaveChanges.</summary>
    void UpdateWorkoutBlock(WorkoutBlock block);

    /// <summary>
    /// Stages an existing <see cref="WorkoutBlock"/> for deletion; its <see cref="WorkoutBlock.Steps"/>
    /// cascade. Does NOT call SaveChanges.
    /// </summary>
    void RemoveWorkoutBlock(WorkoutBlock block);

    /// <summary>Stages a new <see cref="WorkoutStep"/> for insertion. Does NOT call SaveChanges.</summary>
    Task AddWorkoutStepAsync(WorkoutStep step, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="WorkoutStep"/> for update. Does NOT call SaveChanges.</summary>
    void UpdateWorkoutStep(WorkoutStep step);

    /// <summary>Stages an existing <see cref="WorkoutStep"/> for deletion. Does NOT call SaveChanges.</summary>
    void RemoveWorkoutStep(WorkoutStep step);
}
