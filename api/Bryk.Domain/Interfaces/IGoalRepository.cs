using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the Goal entity.
/// </summary>
public interface IGoalRepository
{
    /// <summary>
    /// Loads the <see cref="Goal"/> entity only — no navigation properties included.
    /// Uses no-tracking since the caller is expected to use the result for display purposes.
    /// </summary>
    Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Goal"/> entities for a given athlete, ordered by
    /// <see cref="Goal.TargetDate"/> ascending with nulls last.
    /// Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<Goal>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Goal"/> entities — entity only, no includes.
    /// Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="Goal"/> for insertion. Does NOT call SaveChanges.
    /// </summary>
    Task AddAsync(Goal entity, CancellationToken ct = default);

    /// <summary>
    /// Stages an existing <see cref="Goal"/> for update. Does NOT call SaveChanges.
    /// </summary>
    void Update(Goal entity);

    /// <summary>
    /// Stages an existing <see cref="Goal"/> for deletion. Does NOT call SaveChanges.
    /// </summary>
    void Delete(Goal entity);

    /// <summary>
    /// Persists all staged changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
