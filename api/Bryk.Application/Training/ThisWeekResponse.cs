namespace Bryk.Application.Training;

// Read model for the dashboard This Week card: the Mon–Sun (UTC) week window plus the athlete's
// planned workouts within it, flattened across all plans and ordered by date. Reuses
// PlannedWorkoutResponse (Task 9-3), whose TrainingPlanId tells the card which plan a session belongs to.
public class ThisWeekResponse
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public IReadOnlyList<PlannedWorkoutResponse> PlannedWorkouts { get; set; } = new List<PlannedWorkoutResponse>();
}
