using Bryk.Application.Onboarding;

namespace Bryk.Application.Events;

/// <summary>
/// Per-item create / update / delete plus list / by-id reads for the current athlete's events.
/// Athlete identity is resolved from <see cref="Common.ICurrentUserService"/> — never from
/// a caller parameter. <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/> throw
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (mapped to 404 by the
/// global exception middleware) when the event does not exist or belongs to another athlete.
/// The read methods do not throw — <see cref="GetByIdAsync"/> returns null (controller maps to 404)
/// and <see cref="GetAllAsync"/> returns an empty list for a fresh athlete.
/// </summary>
public interface IEventService
{
    /// <summary>The current athlete's events, ordered by date ascending, each carrying its linked plan(s).
    /// When <paramref name="upcomingOnly"/> is true, filters to events dated today or later.</summary>
    Task<IReadOnlyList<EventListItemResponse>> GetAllAsync(bool upcomingOnly, CancellationToken ct = default);

    /// <summary>Returns null when the event does not exist or belongs to another athlete — the controller
    /// maps null to 404 (this is a GET; it does not throw KeyNotFoundException).</summary>
    Task<EventListItemResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EventResponse> CreateAsync(EventDto request, CancellationToken ct = default);
    Task<EventResponse> UpdateAsync(Guid id, EventDto request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
