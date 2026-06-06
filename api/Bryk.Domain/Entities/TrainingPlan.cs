using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class TrainingPlan : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? EventId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public MethodologyChoice Methodology { get; set; }
    public int? BuildWeeks { get; set; }
    public int? RecoveryWeeks { get; set; }
    public decimal? RecoveryWeekPercentage { get; set; }

    public ICollection<PlannedWorkout> PlannedWorkouts { get; set; } = new List<PlannedWorkout>();
    public Event? Event { get; set; }
    public Athlete Athlete { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
