using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Equipment : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Sport? Sport { get; set; }
    public EquipmentType Type { get; set; }
    public string? Notes { get; set; }

    public Athlete Athlete { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
