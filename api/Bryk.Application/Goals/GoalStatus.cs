namespace Bryk.Application.Goals;

/// <summary>Date-based goal status, computed by <see cref="GoalProgress"/> (Tasks-17-1). Quantitative
/// (target-value) progress is deferred — see the ROADMAP Phase 17 decision.</summary>
public enum GoalStatus
{
    NoDate = 0,
    Upcoming = 1,
    DueSoon = 2,
    Overdue = 3
}
