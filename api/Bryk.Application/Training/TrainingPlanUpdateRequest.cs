using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Metadata-only replace-style update — no PlannedWorkouts (children are edited through their own
// endpoints, ADR-0003 aggregate boundary; this DTO must never add, replace or delete them).
// RecoveryWeekPercentage is percent-scale (60.0m = 60%, ADR-0009 §6). EventId = null clears the link.
public class TrainingPlanUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public MethodologyChoice Methodology { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? EventId { get; set; }
    public int? BuildWeeks { get; set; }
    public int? RecoveryWeeks { get; set; }
    public decimal? RecoveryWeekPercentage { get; set; }
}
