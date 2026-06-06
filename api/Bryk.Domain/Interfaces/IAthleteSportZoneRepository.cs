using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for <see cref="AthleteSportZone"/> overrides. Reads are scoped to the current athlete
/// by the service; this contract exposes a per-athlete fetch plus add/remove staging.
/// </summary>
public interface IAthleteSportZoneRepository
{
    /// <summary>
    /// Returns all zone overrides for an athlete (every sport and metric). Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<AthleteSportZone>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="AthleteSportZone"/> override for insertion. Does NOT call SaveChanges.
    /// </summary>
    Task AddAsync(AthleteSportZone entity, CancellationToken ct = default);

    /// <summary>
    /// Stages an existing <see cref="AthleteSportZone"/> override for deletion. Does NOT call SaveChanges.
    /// </summary>
    void Remove(AthleteSportZone entity);
}
