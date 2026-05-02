using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the Athlete aggregate root.
/// AthleteSportProfile children are persisted through this repository — they do not have their own.
/// </summary>
public interface IAthleteRepository
{
    /// <summary>
    /// Loads the <see cref="Athlete"/> entity only — no navigation properties included.
    /// Uses no-tracking since the caller is expected to use the result for display purposes.
    /// </summary>
    Task<Athlete?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads the <see cref="Athlete"/> and its <see cref="Athlete.SportProfiles"/> children.
    /// Does NOT load Events, Goals, or Equipment.
    /// Uses change tracking so the caller can mutate sport profiles during onboarding.
    /// </summary>
    Task<Athlete?> GetWithSportProfilesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads the <see cref="Athlete"/> with all navigation collections:
    /// <see cref="Athlete.SportProfiles"/>, <see cref="Athlete.Events"/>,
    /// <see cref="Athlete.Goals"/>, and <see cref="Athlete.Equipment"/>.
    /// Intended for the full athlete profile screen. Uses no-tracking.
    /// </summary>
    Task<Athlete?> GetFullProfileAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Athlete"/> entities — entity only, no includes.
    /// Uses no-tracking.
    /// </summary>
    Task<IReadOnlyList<Athlete>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="Athlete"/> for insertion. Does NOT call SaveChanges.
    /// </summary>
    Task AddAsync(Athlete athlete, CancellationToken ct = default);

    /// <summary>
    /// Stages an existing <see cref="Athlete"/> for update. Does NOT call SaveChanges.
    /// </summary>
    void Update(Athlete athlete);

    /// <summary>
    /// Stages an existing <see cref="Athlete"/> for deletion. Does NOT call SaveChanges.
    /// </summary>
    void Delete(Athlete athlete);

    /// <summary>
    /// Looks up a single <see cref="AthleteSportProfile"/> by athlete and sport.
    /// Uses change tracking so the caller can mutate the returned entity and stage an update.
    /// Returns null if no profile exists for this athlete + sport combination.
    /// </summary>
    Task<AthleteSportProfile?> GetSportProfileAsync(Guid athleteId, Sport sport, CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="AthleteSportProfile"/> for insertion. Does NOT call SaveChanges.
    /// </summary>
    void AddSportProfile(AthleteSportProfile profile);

    /// <summary>
    /// Stages an existing <see cref="AthleteSportProfile"/> for update. Does NOT call SaveChanges.
    /// </summary>
    void UpdateSportProfile(AthleteSportProfile profile);
}
