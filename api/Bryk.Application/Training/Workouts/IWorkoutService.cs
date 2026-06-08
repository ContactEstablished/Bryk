namespace Bryk.Application.Training.Workouts;

/// <summary>
/// Logs and reads executed workouts (ADR-0005 §4–6). Athlete identity comes from
/// <see cref="Common.ICurrentUserService"/>; ownership is asserted on the denormalized AthleteId and the
/// linked planned workout. Methods throw <see cref="System.Collections.Generic.KeyNotFoundException"/>
/// (→ 404) when a workout or linked planned workout is missing or belongs to another athlete.
/// </summary>
public interface IWorkoutService
{
    /// <summary>
    /// Logs a completed workout: validates, optionally verifies + seeds from a planned workout, computes
    /// actual load from the captured actuals, and persists in one commit. Throws
    /// <see cref="Exceptions.ValidationException"/> (→ 400) on invalid input.
    /// </summary>
    Task<WorkoutResponse> LogAsync(LogWorkoutRequest request, CancellationToken ct = default);

    /// <summary>Returns a completed workout with its step results; 404 if missing or foreign.</summary>
    Task<WorkoutResponse> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the current athlete's most recent completed workouts (newest first).</summary>
    Task<IReadOnlyList<WorkoutResponse>> GetRecentAsync(int take, CancellationToken ct = default);
}
