using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Athlete : IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public decimal HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public int? RestingHr { get; set; }
    public int? MaxHr { get; set; }
    public int YearsTraining { get; set; }
    public decimal TypicalWeeklyHours { get; set; }
    public MethodologyChoice Methodology { get; set; }

    public ICollection<AthleteSportProfile> SportProfiles { get; set; } = new List<AthleteSportProfile>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
