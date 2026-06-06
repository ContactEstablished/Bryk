using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Workout : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public Sport Sport { get; set; }
    public DateOnly CompletedDate { get; set; }

    public PlannedWorkout? PlannedWorkout { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
