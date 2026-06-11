using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the executed-<see cref="Workout"/> aggregate (with its <see cref="WorkoutStepResult"/>
/// children). Reads are scoped to the current athlete by the service; staging methods do NOT call SaveChanges.
/// </summary>
public interface IWorkoutRepository
{
    /// <summary>Loads a <see cref="Workout"/> with its ordered <see cref="Workout.StepResults"/>. No-tracking.</summary>
    Task<Workout?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads a <see cref="Workout"/> with its <see cref="Workout.StepResults"/> <b>tracked</b>, for
    /// update (replace the step-result collection) and delete (cascade the children). Returns null if missing.
    /// </summary>
    Task<Workout?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the athlete's completed workouts whose <see cref="Workout.CompletedDate"/> is within
    /// [start, end] inclusive, newest first. Entity only (no step results). No-tracking.
    /// </summary>
    Task<IReadOnlyList<Workout>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default);

    /// <summary>
    /// Returns the athlete's completed workouts, newest first (<see cref="Workout.CompletedDate"/> desc,
    /// then <see cref="IAuditable.CreatedAt"/> desc), with optional <paramref name="from"/>/<paramref name="to"/>
    /// (inclusive <see cref="Workout.CompletedDate"/> bounds) and <paramref name="sport"/> filters, paged by
    /// <paramref name="skip"/>/<paramref name="take"/>. Entity only (no step results). No-tracking.
    /// </summary>
    Task<IReadOnlyList<Workout>> GetByAthleteFilteredAsync(Guid athleteId, DateOnly? from, DateOnly? to, Sport? sport, int skip, int take, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="Workout"/> (with its step results) for insertion. Does NOT call SaveChanges.</summary>
    Task AddAsync(Workout workout, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="Workout"/> for update. Does NOT call SaveChanges.</summary>
    void Update(Workout workout);

    /// <summary>Stages an existing <see cref="Workout"/> for deletion; its step results cascade. Does NOT call SaveChanges.</summary>
    void Delete(Workout workout);
}
