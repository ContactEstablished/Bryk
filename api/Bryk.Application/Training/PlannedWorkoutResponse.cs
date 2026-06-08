using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Read-side, Id-bearing, so the client can target per-item edit/delete. Carries TrainingPlanId
// so reads that flatten planned workouts across plans (the This Week card, Task 9-4) can
// reference the owning plan.
public class PlannedWorkoutResponse
{
    public Guid Id { get; set; }
    public Guid TrainingPlanId { get; set; }
    public Sport Sport { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PlannedDurationMinutes { get; set; }
    public decimal? PlannedLoad { get; set; }

    // Training load (ADR-0005 §3). ComputedLoad is the calculator result and is null on reads that don't
    // load Blocks; IsLoadOverride is true when PlannedLoad is set; EffectiveLoad = PlannedLoad ?? ComputedLoad.
    public decimal? ComputedLoad { get; set; }
    public decimal? EffectiveLoad { get; set; }
    public bool IsLoadOverride { get; set; }

    // Structured payload (ADR-0004 §2). Empty on the plan/This-Week reads, which don't load it;
    // populated by the structure endpoints (Task 10-4).
    public List<WorkoutBlockResponse> Blocks { get; set; } = new();
}
