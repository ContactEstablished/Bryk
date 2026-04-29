using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Event : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public Sport? Sport { get; set; }
    public TriathlonDistance? TriathlonDistance { get; set; }
    public EventPriority Priority { get; set; }
    public string? Notes { get; set; }

    public Athlete Athlete { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
