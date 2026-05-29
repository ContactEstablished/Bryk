using Bryk.Application.Events;
using Bryk.Application.Goals;

namespace Bryk.Application.Profile;

// Read-side shape for the Goals section. Carries Id-bearing EventResponse / GoalResponse
// so the Profile editor can target per-item update / delete via the events / goals endpoints.
public class ProfileGoalsResponse
{
    public List<EventResponse> Events { get; set; } = new();
    public List<GoalResponse> Goals { get; set; } = new();
}
