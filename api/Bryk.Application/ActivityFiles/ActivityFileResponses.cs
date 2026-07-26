using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

/// <summary>The parsed session actuals shown in the import preview.</summary>
public class ParsedActivityDto
{
    public Sport Sport { get; set; }
    public DateOnly CompletedDate { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public int? DistanceMeters { get; set; }
    public int? AvgHr { get; set; }
    public int? MaxHr { get; set; }
    public int? AvgPower { get; set; }
    public int? AvgPace { get; set; }
    public int SampleCount { get; set; }
}

/// <summary>
/// One planned workout the import could satisfy. <see cref="DayOffset"/> is
/// <c>ScheduledDate.DayNumber − CompletedDate.DayNumber</c>, so <c>-1 / 0 / +1</c> — it is what lets the
/// UI put same-day matches first and label the others.
/// </summary>
public class MatchCandidateDto
{
    public Guid PlannedWorkoutId { get; set; }
    public Guid TrainingPlanId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Sport Sport { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public decimal? PlannedLoad { get; set; }
    public int DayOffset { get; set; }
}

public class ActivityFileUploadResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ActivityFileFormat Format { get; set; }
    public int ByteSize { get; set; }
    public ParsedActivityDto Parsed { get; set; } = new();
    public decimal? ComputedLoad { get; set; }
    public IReadOnlyList<ZoneHistogramEntry> ZoneSeconds { get; set; } = new List<ZoneHistogramEntry>();
    public IReadOnlyList<MatchCandidateDto> MatchCandidates { get; set; } = new List<MatchCandidateDto>();
}

/// <summary>
/// Deliberately small: the client only needs <see cref="WorkoutId"/> to navigate to
/// <c>/workouts/{id}</c>, which then loads the workout through the existing <c>GET /workouts/{id}</c>.
/// </summary>
public class ActivityFileCommitResponse
{
    public Guid WorkoutId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public decimal? ComputedLoad { get; set; }
}

/// <summary>The "from file" badge's payload, resolved through the reverse link (ADR-0010 §4).</summary>
public class ActivityFileSourceResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ActivityFileFormat Format { get; set; }
    public DateTime UploadedAt { get; set; }
}
