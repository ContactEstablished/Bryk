using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the Equipment entity.
/// </summary>
public interface IEquipmentRepository
{
    /// <summary>
    /// Loads the <see cref="Equipment"/> entity only — no navigation properties included.
    /// Uses no-tracking since the caller is expected to use the result for display purposes.
    /// </summary>
    Task<Equipment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Equipment"/> entities for a given athlete, ordered by
    /// <see cref="Equipment.Type"/> ascending.
    /// Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<Equipment>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Equipment"/> entities — entity only, no includes.
    /// Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="Equipment"/> for insertion. Does NOT call SaveChanges.
    /// </summary>
    Task AddAsync(Equipment entity, CancellationToken ct = default);

    /// <summary>
    /// Stages an existing <see cref="Equipment"/> for update. Does NOT call SaveChanges.
    /// </summary>
    void Update(Equipment entity);

    /// <summary>
    /// Stages an existing <see cref="Equipment"/> for deletion. Does NOT call SaveChanges.
    /// </summary>
    void Delete(Equipment entity);
}
