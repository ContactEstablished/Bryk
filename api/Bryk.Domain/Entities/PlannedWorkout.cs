using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class PlannedWorkout : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Guid TrainingPlanId { get; set; }
    public Sport Sport { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PlannedDurationMinutes { get; set; }
    public decimal? PlannedLoad { get; set; }

    public TrainingPlan TrainingPlan { get; set; } = null!;
    public ICollection<WorkoutBlock> Blocks { get; set; } = new List<WorkoutBlock>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
