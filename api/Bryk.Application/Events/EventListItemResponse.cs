using Bryk.Domain.Entities;

namespace Bryk.Application.Events;

// GET-only shape: all EventResponse fields plus the reverse-EventId linked plan(s) (display-only —
// the plan<->event write path waits for Phase 18's plan PUT). GET /events and GET /events/{id} both
// return this shape (single item for the by-id route).
public class EventListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public Sport? Sport { get; set; }
    public TriathlonDistance? TriathlonDistance { get; set; }
    public string? CustomDistanceName { get; set; }
    public EventPriority Priority { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<LinkedPlanDto> LinkedPlans { get; set; } = new List<LinkedPlanDto>();
}
