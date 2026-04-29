using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Goal : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public GoalType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }

    public Athlete Athlete { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
