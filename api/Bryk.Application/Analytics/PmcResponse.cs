namespace Bryk.Application.Analytics;

// One day of the Performance Management Chart (ADR-0006 §4). Ctl/Atl are that day's post-update EWMA
// values; Tsb is yesterday's Ctl − Atl (the form convention).
public class PmcPointDto
{
    public DateOnly Date { get; set; }
    public decimal Load { get; set; }
    public decimal Ctl { get; set; }
    public decimal Atl { get; set; }
    public decimal Tsb { get; set; }
}

// The "current" summary for the requested range's last day (ADR-0006 §6). Acwr is null under 28 days of
// history. The whole summary is null (on PmcResponse) for an athlete with no workout history.
public class PmcSummaryDto
{
    public DateOnly Date { get; set; }
    public decimal Ctl { get; set; }
    public decimal Atl { get; set; }
    public decimal Tsb { get; set; }
    public decimal? Acwr { get; set; }
}

// The pmc read shape: the [from, to] series plus today's-equivalent current summary (null for a fresh
// athlete), so the dashboard needs a single call.
public class PmcResponse
{
    public IReadOnlyList<PmcPointDto> Series { get; set; } = new List<PmcPointDto>();
    public PmcSummaryDto? Current { get; set; }
}
