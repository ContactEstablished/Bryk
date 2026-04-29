using Bryk.Domain.Entities;

namespace Bryk.Application.Onboarding;

public class GoalDto
{
    public GoalType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }
}
