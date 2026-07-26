using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the <see cref="DailyWellness"/> row (ADR-0011 §2). Staging methods do NOT call
/// SaveChanges.
/// </summary>
public interface IDailyWellnessRepository
{
    /// <summary>
    /// Loads the athlete's <see cref="DailyWellness"/> row for <paramref name="date"/> <b>tracked</b>,
    /// for the per-day upsert (the service mutates the returned instance in place). Null if the day has
    /// no entry. Deliberately NOT no-tracking: the caller's write depends on change tracking.
    /// </summary>
    Task<DailyWellness?> GetByAthleteAndDateTrackedAsync(Guid athleteId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// The athlete's <see cref="DailyWellness"/> rows in <c>[from, to]</c> (both ends inclusive),
    /// ordered by <see cref="DailyWellness.Date"/> ascending. Sparse — days with no entry are simply
    /// absent. Uses no-tracking (display/aggregate read).
    /// </summary>
    Task<IReadOnlyList<DailyWellness>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="DailyWellness"/> for insertion. Does NOT call SaveChanges.</summary>
    Task AddAsync(DailyWellness entity, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="DailyWellness"/> for update. Does NOT call SaveChanges.</summary>
    void Update(DailyWellness entity);
}
