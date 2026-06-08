using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Read-side, Id-bearing step.
public class WorkoutStepResponse
{
    public Guid Id { get; set; }
    public int OrderIndex { get; set; }
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
