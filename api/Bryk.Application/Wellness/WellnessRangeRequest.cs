namespace Bryk.Application.Wellness;

// Range contract for GET /wellness. Nullable so the validator can require both ends explicitly —
// the controller binds optional query params (mirrors Analytics/AnalyticsRangeRequest.cs).
public class WellnessRangeRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
