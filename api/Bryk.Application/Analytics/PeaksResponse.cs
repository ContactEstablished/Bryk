using Bryk.Domain.Entities;

namespace Bryk.Application.Analytics;

// The session-level record kinds (ADR-0007 §2). Session-level only — never a sample-derived duration-curve
// peak (those need per-sample series, Phase 19+).
public enum PeakKind
{
    Load = 1,
    Duration = 2,
    Distance = 3,
    Pace = 4,
    Power = 5
}

// One session-level personal record. Value's unit is implied by Kind + Sport (TSS / seconds / metres /
// sec-per-unit / watts). PreviousValue is the second-best of that kind (null when only one sample exists);
// IsRecent = the record was set within the last 90 days, so the UI can render a DeltaChip improvement.
public class PeakRecordDto
{
    public PeakKind Kind { get; set; }
    public Sport Sport { get; set; }
    public decimal Value { get; set; }
    public DateOnly AchievedDate { get; set; }
    public Guid AchievedWorkoutId { get; set; }
    public bool IsRecent { get; set; }
    public decimal? PreviousValue { get; set; }
}

public class PeaksResponse
{
    public IReadOnlyList<PeakRecordDto> Records { get; set; } = new List<PeakRecordDto>();
}
