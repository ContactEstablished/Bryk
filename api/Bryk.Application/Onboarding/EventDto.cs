using Bryk.Domain.Entities;

namespace Bryk.Application.Onboarding;

public class EventDto
{
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public Sport? Sport { get; set; }
    public TriathlonDistance? TriathlonDistance { get; set; }
    public string? CustomDistanceName { get; set; }
    public EventPriority Priority { get; set; }
    public string? Notes { get; set; }
}
