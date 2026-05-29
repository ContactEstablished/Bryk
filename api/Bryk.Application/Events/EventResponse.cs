using Bryk.Domain.Entities;

namespace Bryk.Application.Events;

// Read-side shape for an Event, carrying its Id so the client can target
// per-item update / delete. Create/update request bodies use the Id-less EventDto.
public class EventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public Sport? Sport { get; set; }
    public TriathlonDistance? TriathlonDistance { get; set; }
    public string? CustomDistanceName { get; set; }
    public EventPriority Priority { get; set; }
    public string? Notes { get; set; }
}
