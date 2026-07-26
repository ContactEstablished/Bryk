namespace Bryk.Application.Training;

// Read model for the dashboard This Week card: the Mon–Sun (UTC) week window plus the athlete's
// planned workouts within it, flattened across all plans and ordered by date. Reuses
// PlannedWorkoutResponse (Task 9-3), whose TrainingPlanId tells the card which plan a session belongs to.
public class ThisWeekResponse
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    // Σ of each workout's effective load over the week (ADR-0005 §3). Null effective loads count as 0.
    public decimal WeeklyLoad { get; set; }
    // The week's load target from the athlete's active plan (ADR-0009). Null when no plan covers today,
    // or when the plan has no usable baseline — the card then renders exactly as it did before Phase 18.
    public decimal? TargetLoad { get; set; }
    // Σ EffectiveLoad (LoadOverride ?? ComputedLoad) of the athlete's completed workouts in the week.
    public decimal ActualLoad { get; set; }
    public IReadOnlyList<PlannedWorkoutResponse> PlannedWorkouts { get; set; } = new List<PlannedWorkoutResponse>();
}
