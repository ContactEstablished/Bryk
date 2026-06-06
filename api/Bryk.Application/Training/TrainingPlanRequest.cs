using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Write-side, Id-less. Methodology is per-plan (ADR-0003 decision 3); the client seeds it from
// the athlete's default. PlannedWorkouts is optional — sessions may be supplied at create time
// or added later via AddPlannedWorkoutAsync. No structured interval/sets payload (Phase 10).
public class TrainingPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public MethodologyChoice Methodology { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? EventId { get; set; }
    public int? BuildWeeks { get; set; }
    public int? RecoveryWeeks { get; set; }
    public decimal? RecoveryWeekPercentage { get; set; }
    public List<PlannedWorkoutDto>? PlannedWorkouts { get; set; }
}
