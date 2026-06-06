using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Read-side, Id-bearing, with Id-bearing planned-workout children. Children are populated on the
// single-plan read (GetByIdAsync); the list read (GetByAthleteAsync) returns plan summaries.
public class TrainingPlanResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MethodologyChoice Methodology { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? EventId { get; set; }
    public int? BuildWeeks { get; set; }
    public int? RecoveryWeeks { get; set; }
    public decimal? RecoveryWeekPercentage { get; set; }
    public IReadOnlyList<PlannedWorkoutResponse> PlannedWorkouts { get; set; } = new List<PlannedWorkoutResponse>();
}
