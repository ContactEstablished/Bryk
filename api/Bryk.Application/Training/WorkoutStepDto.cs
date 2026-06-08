using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Write-side step (ADR-0004 §2/§3). Order is positional within the block. Which fields are valid is
// discriminated by the parent workout's Sport (validated server-side): cardio uses duration/distance
// + zone/power/HR/pace; strength uses sets/reps/load/RPE.
public class WorkoutStepDto
{
    public StepIntent Intent { get; set; }
    public string? Title { get; set; }
    public int? DurationSeconds { get; set; }
    public int? DistanceMeters { get; set; }
    public int? TargetZone { get; set; }
    public int? TargetPowerLow { get; set; }
    public int? TargetPowerHigh { get; set; }
    public int? TargetHrLow { get; set; }
    public int? TargetHrHigh { get; set; }
    public int? TargetPaceLow { get; set; }
    public int? TargetPaceHigh { get; set; }
    public int? Sets { get; set; }
    public int? Reps { get; set; }
    public decimal? LoadKg { get; set; }
    public decimal? Rpe { get; set; }
}
