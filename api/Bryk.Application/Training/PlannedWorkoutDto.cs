using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Write-side, Id-less. Base fields only — the structured interval/strength payload is Phase 10
// (ADR-0003 decision 2). PlannedLoad is a manually entered target, not the computed strength load.
public class PlannedWorkoutDto
{
    public Sport Sport { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PlannedDurationMinutes { get; set; }
    public decimal? PlannedLoad { get; set; }
}
