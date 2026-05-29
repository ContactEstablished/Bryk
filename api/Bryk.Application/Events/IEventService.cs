using Bryk.Application.Onboarding;

namespace Bryk.Application.Events;

/// <summary>
/// Per-item create / update / delete for the current athlete's events.
/// Athlete identity is resolved from <see cref="Common.ICurrentUserService"/> — never from
/// a caller parameter. <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/> throw
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> (mapped to 404 by the
/// global exception middleware) when the event does not exist or belongs to another athlete.
/// </summary>
public interface IEventService
{
    Task<EventResponse> CreateAsync(EventDto request, CancellationToken ct = default);
    Task<EventResponse> UpdateAsync(Guid id, EventDto request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
