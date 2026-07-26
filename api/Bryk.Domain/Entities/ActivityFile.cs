using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// Raw upload for the two-step activity-file import flow (ADR-0010 §2/§4/§5): the parsed preview and the
// eventual Workout commit are two separate calls, and the uploaded bytes have to live somewhere between
// them. Content is the raw file bytes (varbinary(max) — no filesystem path, no blob store, ADR-0010 §2).
// AthleteId is denormalized + indexed with no FK to Athlete, matching Workout/WorkoutStepResult
// (ADR-0003/0004). ParsedWorkoutId is a plain indexed Guid? with NO FK to Workout (ADR-0010 §4) — the
// reverse link the "from file" badge and the duplicate-commit guard both read; a deleted Workout must
// not cascade the uploaded file away, and there is no delete-path to reason about. ZoneHistogramJson
// holds the derived 5-bucket per-zone seconds histogram (ADR-0010 §5), written once at commit and null
// before it. UploadedAt is the domain-facing timestamp, set once by the service at insert
// (DateTime.UtcNow); CreatedAt/UpdatedAt stay owned by AuditableEntityInterceptor and are NEVER set
// manually (CLAUDE.md) — the redundancy between UploadedAt and CreatedAt is deliberate, not a mistake.
public class ActivityFile : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ActivityFileFormat Format { get; set; }
    public int ByteSize { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime UploadedAt { get; set; }
    public Guid? ParsedWorkoutId { get; set; }
    public string? ZoneHistogramJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
