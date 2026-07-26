namespace Bryk.Application.ActivityFiles;

/// <summary>
/// One bucket of the derived per-zone seconds histogram (ADR-0010 §5). <see cref="ZoneNumber"/> is 1..5,
/// matching <c>ZoneTimeDto</c>'s buckets so Task 19-6 can add sample-derived and estimate-derived seconds
/// together. <b>This is the persisted JSON's element shape</b> (serialized by Task 19-4, deserialized by
/// Task 19-6) — changing it after Phase 19 ships is a data-format change, not a refactor.
/// </summary>
public sealed record ZoneHistogramEntry(int ZoneNumber, int Seconds);
