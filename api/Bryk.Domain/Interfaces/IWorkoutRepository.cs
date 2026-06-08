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
    /// Returns the athlete's completed workouts whose <see cref="Workout.CompletedDate"/> is within
    /// [start, end] inclusive, newest first. Entity only (no step results). No-tracking.
    /// </summary>
    Task<IReadOnlyList<Workout>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default);

    /// <summary>Returns the athlete's most recent completed workouts (up to <paramref name="take"/>), newest first. No-tracking.</summary>
    Task<IReadOnlyList<Workout>> GetRecentByAthleteAsync(Guid athleteId, int take, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="Workout"/> (with its step results) for insertion. Does NOT call SaveChanges.</summary>
    Task AddAsync(Workout workout, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="Workout"/> for update. Does NOT call SaveChanges.</summary>
    void Update(Workout workout);

    /// <summary>Stages an existing <see cref="Workout"/> for deletion; its step results cascade. Does NOT call SaveChanges.</summary>
    void Delete(Workout workout);
}
