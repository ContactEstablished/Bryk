using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

/// <summary>
/// The result of parsing one activity file (ADR-0010 §1/§6): session aggregates plus an in-memory
/// sample series. <see cref="Samples"/> is never persisted (ADR-0010 §6) — <see cref="ZoneHistogramCalculator"/>
/// reduces it to a 5-bucket histogram that Task 19-4 does persist. Deliberately carries no zone buckets
/// itself: bucketing needs the athlete's zones, an Application/service concern, not a parser concern.
///
/// Cross-format resolution rules — identical in the TCX, GPX and FIT parsers, stated once here:
/// 1. Sport — (a) the format's own sport metadata when present and recognised; (b) otherwise
///    <see cref="Sport.Bike"/> when any sample carries a power value; (c) otherwise <see cref="Sport.Run"/>.
///    Deterministic, never throws.
/// 2. Session averages/max are always derived from the retained samples, never the file's own summary
///    elements: AvgHr/AvgPower are the arithmetic mean of the non-null in-range values rounded to the
///    nearest int; MaxHr is the max. This can differ by ±1 from the device's reported average — immaterial
///    for TSS.
/// 3. Duration/distance prefer the file's declared totals when present (TCX lap totals, FIT session
///    totals); otherwise derive from the last sample's <see cref="ActivitySample.ElapsedSeconds"/> / a
///    summed great-circle distance.
/// 4. AvgPace = DurationSeconds / (DistanceMeters / unit), rounded to the nearest int, only when Sport is
///    Run or Swim and both are &gt; 0; null otherwise. Unit is 1000 (m, Run) or 100 (m, Swim) — the same
///    convention as <see cref="Analytics.AnalyticsService"/>'s session-pace calculation.
/// 5. Zero retained samples → throw <see cref="Exceptions.ValidationException"/> with a single
///    <c>"File: The file contains no track data."</c> message.
/// 6. A future <see cref="StartTimeUtc"/> is left to the caller to reject (Task 19-4); parsers never read
///    the clock.
/// </summary>
/// <param name="Sport">Resolved per rule 1 above.</param>
/// <param name="StartTimeUtc">
/// The file's first timestamp, normalised to UTC. The eventual <c>Workout.CompletedDate</c> is this
/// value's UTC calendar date. No timezone handling in v1 — a Phase 21 candidate, not implemented here.
/// </param>
/// <param name="AvgPace">
/// Seconds per km (Run) or per 100 m (Swim); null for Bike/Strength/Triathlon (rule 4 above).
/// </param>
public sealed record ParsedActivity(
    Sport Sport,
    DateTime StartTimeUtc,
    int? DurationSeconds,
    int? DistanceMeters,
    int? AvgHr,
    int? MaxHr,
    int? AvgPower,
    int? AvgPace,
    IReadOnlyList<ActivitySample> Samples);

/// <summary>
/// One instant in a <see cref="ParsedActivity"/>'s sample series. Every numeric is nullable except
/// <see cref="ElapsedSeconds"/> — a point the file gives no value for carries nulls, not zeros.
/// </summary>
/// <param name="ElapsedSeconds">Seconds since <see cref="ParsedActivity.StartTimeUtc"/>, monotonically non-decreasing.</param>
public sealed record ActivitySample(int ElapsedSeconds, int? Hr, int? Power, int? PaceSecPerUnit);
