namespace Bryk.Application.Training.Periodization;

// Per-week merge shape returned by IPeriodizationService.GetWeeklyTargetsAsync.
//
// Asymmetry (deliberate, not a bug): PlannedLoad is scoped to THIS plan's own planned workouts
// (filtered on TrainingPlanId); ActualLoad is athlete-wide for the week, because a completed
// Workout carries no plan attribution (ADR-0005 / ADR-0007 treat actual load athlete-wide). Do
// not invent an attribution rule to make the two symmetric.
public class WeeklyTargetWeekDto
{
    public DateOnly WeekStart { get; set; }
    public decimal TargetLoad { get; set; }
    public bool IsRecoveryWeek { get; set; }
    public bool IsTaperWeek { get; set; }
    public decimal PlannedLoad { get; set; }
    public decimal ActualLoad { get; set; }
}
