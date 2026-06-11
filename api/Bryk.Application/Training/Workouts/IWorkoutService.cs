using Bryk.Domain.Entities;

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

    /// <summary>
    /// Replace-style update of a completed workout: validates, replaces the session actuals and the whole
    /// step-result list, recomputes actual load, and persists in one commit. 404 if the workout (or a
    /// supplied planned workout) is missing or foreign; <see cref="Exceptions.ValidationException"/> (→ 400)
    /// on invalid input.
    /// </summary>
    Task<WorkoutResponse> UpdateAsync(Guid id, UpdateWorkoutRequest request, CancellationToken ct = default);

    /// <summary>Hard-deletes a completed workout (step results cascade); 404 if missing or foreign.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns a completed workout with its step results; 404 if missing or foreign.</summary>
    Task<WorkoutResponse> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the current athlete's completed workouts, newest first, with optional <paramref name="from"/>/
    /// <paramref name="to"/> (inclusive <c>CompletedDate</c> bounds) and <paramref name="sport"/> filters,
    /// paged by <paramref name="skip"/> (≥ 0, default 0) and <paramref name="take"/> (clamped 1..100, default 20).
    /// </summary>
    Task<IReadOnlyList<WorkoutResponse>> GetWorkoutsAsync(DateOnly? from, DateOnly? to, Sport? sport, int? skip, int? take, CancellationToken ct = default);
}
