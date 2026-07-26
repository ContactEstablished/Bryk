using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// One manually-entered wellness row per athlete per day (ADR-0011 §1/§2/§6), uniquely indexed on
// {AthleteId, Date}. EVERY metric is nullable because partial entries are the norm — an athlete who
// weighs in but owns no HRV strap must still be able to log the day. AthleteId is denormalized and
// indexed with no FK to Athlete, matching Workout/WorkoutStepResult/ActivityFile (ADR-0003/0004); the
// composite index's leading column covers athlete-scoped queries, so no second index is needed. This row
// is INDEPENDENT of Athlete.RestingHr / Athlete.WeightKg and never writes to them (ADR-0011 §1) — those
// stay the one-off onboarding self-report, this is the time series. SleepQuality (1-5) and Soreness
// (1-10) are plain int?, not enums: bounds are the validator's job (Task 20-2), the domain stores the
// number. CreatedAt/UpdatedAt are owned by AuditableEntityInterceptor and are NEVER set manually
// (CLAUDE.md).
public class DailyWellness : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public DateOnly Date { get; set; }

    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
