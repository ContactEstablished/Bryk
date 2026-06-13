namespace Bryk.Application.Analytics;

// Shared range contract for the analytics endpoints (daily-load, pmc). Nullable so the validator can
// require both ends explicitly — the controller binds optional query params (ADR-0006 §7).
public class AnalyticsRangeRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
