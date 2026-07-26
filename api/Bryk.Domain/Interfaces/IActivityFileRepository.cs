using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the <see cref="ActivityFile"/> row (ADR-0010). Staging methods do NOT call SaveChanges.
/// </summary>
public interface IActivityFileRepository
{
    /// <summary>Stages a new <see cref="ActivityFile"/> for insertion. Does NOT call SaveChanges.</summary>
    Task AddAsync(ActivityFile file, CancellationToken ct = default);

    /// <summary>
    /// Loads an <see cref="ActivityFile"/> <b>tracked</b> (including <see cref="ActivityFile.Content"/>),
    /// for commit (set <see cref="ActivityFile.ParsedWorkoutId"/> + the histogram) and discard. Null if missing.
    /// </summary>
    Task<ActivityFile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The athlete's <see cref="ActivityFile"/> rows whose <see cref="ActivityFile.ParsedWorkoutId"/> is in
    /// <paramref name="workoutIds"/> — the reverse "which workouts came from a file" lookup (ADR-0010 §4).
    /// <b>Never loads <see cref="ActivityFile.Content"/></b>; the returned instances carry an empty
    /// <c>Content</c>. No-tracking. An empty <paramref name="workoutIds"/> returns an empty list with no query.
    /// </summary>
    Task<IReadOnlyList<ActivityFile>> GetByParsedWorkoutIdsAsync(Guid athleteId, IEnumerable<Guid> workoutIds, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="ActivityFile"/> for deletion. Does NOT call SaveChanges.</summary>
    void Delete(ActivityFile file);
}
