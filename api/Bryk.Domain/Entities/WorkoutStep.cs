using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// One prescribed effort within a WorkoutBlock (ADR-0004 §2/§3). A single shared shape for both cardio
// and strength — no subtypes. Which columns are meaningful is discriminated by the parent
// PlannedWorkout.Sport, enforced by the 10-4 validator, not the schema: cardio steps use
// duration/distance + zone/power/HR/pace targets; strength steps use sets/reps/load/Rpe. A step may
// carry a TargetZone and/or explicit raw target ranges (a single value sets Low only, or Low == High).
public class WorkoutStep : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Guid WorkoutBlockId { get; set; }
    public int OrderIndex { get; set; }
    public StepIntent Intent { get; set; }
    public string? Title { get; set; }

    // Exactly one of these drives the step (validator-enforced in 10-4).
    public int? DurationSeconds { get; set; }
    public int? DistanceMeters { get; set; }

    // Intensity targets — all nullable. TargetZone is resolved against the workout's Sport zones (10-1).
    public int? TargetZone { get; set; }
    public int? TargetPowerLow { get; set; }
    public int? TargetPowerHigh { get; set; }
    public int? TargetHrLow { get; set; }
    public int? TargetHrHigh { get; set; }
    public int? TargetPaceLow { get; set; }
    public int? TargetPaceHigh { get; set; }

    // Strength — used when the parent workout's Sport is Strength.
    public int? Sets { get; set; }
    public int? Reps { get; set; }
    public decimal? LoadKg { get; set; }
    public decimal? Rpe { get; set; }

    public WorkoutBlock WorkoutBlock { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
