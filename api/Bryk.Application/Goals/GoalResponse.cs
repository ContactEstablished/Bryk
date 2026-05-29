using Bryk.Domain.Entities;

namespace Bryk.Application.Goals;

// Read-side shape for a Goal, carrying its Id so the client can target
// per-item update / delete. Create/update request bodies use the Id-less GoalDto.
public class GoalResponse
{
    public Guid Id { get; set; }
    public GoalType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }
}
