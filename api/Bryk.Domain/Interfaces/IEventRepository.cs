using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the Event entity.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Loads the <see cref="Event"/> entity only — no navigation properties included.
    /// Uses no-tracking since the caller is expected to use the result for display purposes.
    /// </summary>
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Event"/> entities for a given athlete, ordered by
    /// <see cref="Event.EventDate"/> ascending (upcoming events first).
    /// Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<Event>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Event"/> entities — entity only, no includes.
    /// Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="Event"/> for insertion. Does NOT call SaveChanges.
    /// </summary>
    Task AddAsync(Event entity, CancellationToken ct = default);

    /// <summary>
    /// Stages an existing <see cref="Event"/> for update. Does NOT call SaveChanges.
    /// </summary>
    void Update(Event entity);

    /// <summary>
    /// Stages an existing <see cref="Event"/> for deletion. Does NOT call SaveChanges.
    /// </summary>
    void Delete(Event entity);
}
