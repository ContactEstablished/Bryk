namespace Bryk.Application.Analytics;

// The weekly-load span (ADR-0007 §3). The controller/service defaults it to 8 when the query param is
// absent; the validator enforces 1–26 for an explicitly-supplied value.
public class WeeklyLoadRequest
{
    public int Weeks { get; set; }
}
