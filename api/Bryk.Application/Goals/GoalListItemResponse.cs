using Bryk.Domain.Entities;

namespace Bryk.Application.Goals;

// GET-only shape: all GoalResponse fields plus computed DaysRemaining + Status (GoalProgress.Compute).
// No TargetValue/Unit/CurrentValue — quantitative progress is deferred (ROADMAP Phase 17).
public class GoalListItemResponse
{
    public Guid Id { get; set; }
    public GoalType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }
    public int? DaysRemaining { get; set; }
    public GoalStatus Status { get; set; }
}
