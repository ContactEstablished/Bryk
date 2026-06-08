using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// An ordered, optionally-repeated group of steps within a PlannedWorkout (ADR-0004 §2). Two-level
// only: a block's Repeats expresses interval sets ("4× [work / recovery]" = one block, Repeats = 4,
// two steps); recursive block-in-block nesting is out of Phase-10 scope. AthleteId is denormalized +
// indexed with no FK to Athlete (ADR-0003 pattern); ownership cascades PlannedWorkout → block → step.
public class WorkoutBlock : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Guid PlannedWorkoutId { get; set; }
    public int OrderIndex { get; set; }
    public int Repeats { get; set; }

    public ICollection<WorkoutStep> Steps { get; set; } = new List<WorkoutStep>();
    public PlannedWorkout PlannedWorkout { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
