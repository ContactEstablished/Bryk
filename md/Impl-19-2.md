# Impl 19-2 — Build order: `IActivityFileParser` + `ParsedActivity` + TCX/GPX parsers + zone-histogram math

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-19-2.md`.
**Decision lock:** ADR-0010 §1 (parsers sit behind one Application abstraction; `.tcx`/`.gpx` use
`System.Xml.Linq`, no package — `Garmin.FIT.Sdk` is Task 19-3's alone), §5 (the histogram is a JSON
column on `ActivityFile`, reported as method `samples` — this task only produces the bucket shape, it
does not serialize or persist it), §6 (no per-second sample persistence — `ParsedActivity.Samples` is
in-memory only) — all three written by Task 19-1, cited here by content, not by re-deriving them;
ADR-0007 §4 (the 5-bucket collapse and the coarse `%HRmax` scheme this task must reproduce character-for-
character from `TimeInZoneCalculator.cs`).
**Scope:** Backend only, almost entirely pure. **No package** (`System.Xml.Linq` is in the BCL — do not
touch any `.csproj` other than `Bryk.API.Tests.csproj`'s fixture-glob `ItemGroup`), **no migration, no
service, no controller, no `Program.cs` line, no UI.** Nothing written in this task is resolvable from DI
or reachable over HTTP until Task 19-4 — that is expected, not dead code to wire up early.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. One commit at the end with the message in `Tasks-19-2.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`.
- `dotnet build api/Bryk.sln` green. Record the exact warning count — it must be **16** (9× NU1903
  `System.Security.Cryptography.Xml` design-time + the two pre-existing
  `WorkoutsControllerTests.cs:121,150` nullable warnings + the rest) and must not grow at any step below.
- `dotnet test api/Bryk.sln` green. Record the exact starting count. The phase baseline is **262**
  (173 `Bryk.Application.Tests` + 89 `Bryk.API.Tests`); this task's own contract says the true starting
  number is "262 plus whatever 19-1 added." **Do not hardcode 262** as the pre-this-task count — read
  the actual number the run prints and use that as the base for Step 12's arithmetic. (19-1's own test
  list, `ActivityFileRepositoryTests.cs`, is 5 facts, so 267 is the likely number, but confirm, don't
  assume.)
- **Hard dependency check — Task 19-1 must have landed and been committed before this task starts.**
  Confirm all of the following exist; if any is missing, **STOP** — go run Task 19-1's build order first,
  do not attempt to stub or fast-forward `ActivityFileFormat` yourself:
  - `api/Bryk.Domain/Entities/Enums/ActivityFileFormat.cs` — enum `Fit = 1, Tcx = 2, Gpx = 3`, namespace
    `Bryk.Domain.Entities` (confirmed convention: `Sport.cs` lives in `Entities/Enums/` but declares
    `namespace Bryk.Domain.Entities;` — every enum in this codebase does this; `ActivityFileFormat` is no
    exception, and `IActivityFileParser.cs` below needs `using Bryk.Domain.Entities;`, not
    `Bryk.Domain.Entities.Enums`).
  - `md/decisions/0010-activity-file-import.md` exists and is `Accepted`.
  - `api/Bryk.Domain/Entities/ActivityFile.cs` and `IActivityFileRepository.cs` exist (contract only —
    this task never calls either).
- Confirm the fresh surface this task creates does **not** yet exist:
  `api/Bryk.Application/ActivityFiles/`, `api/Bryk.Infrastructure/ActivityFiles/`,
  `api/Bryk.Application.Tests/ActivityFiles/`, `api/Bryk.API.Tests/ActivityFiles/`,
  `api/Bryk.API.Tests/Fixtures/ActivityFiles/`.
- Re-read `md/Tasks-19-2.md` in full. Open in editor, and confirm the exact shapes cited below (all
  verified this session — if any has drifted, stop and re-read before writing code):
  - `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs` — **read only, do not edit, owned by Task
    19-6.** `ZoneCount = 5` (L14); `ClassifyStep`'s band predicate at L122:
    `v >= z.LowerBound && (z.UpperBound is null || v < z.UpperBound)`; `Math.Min(band.ZoneNumber,
    ZoneCount)` at L123; `HrZone` at L127–138 (`pct switch { < 0.60m => 1, < 0.70m => 2, < 0.80m => 3,
    < 0.90m => 4, _ => 5 }`).
  - `api/Bryk.Application/Zones/SportZonesResponse.cs` — `{ Sport, Metric, Zones: IReadOnlyList<ZoneDto> }`.
  - `api/Bryk.Application/Zones/ZoneDto.cs` — `{ ZoneNumber: int, LowerBound: decimal, UpperBound:
    decimal?, IsOverride: bool }`. `UpperBound == null` = open-ended top.
  - `api/Bryk.Application/Analytics/AnalyticsService.cs:147–156` (`ToSummary`) — the pace convention:
    `unitMeters = Run ? 1000m : 100m; avgPace = dur / (dist / unitMeters);` — sec-per-km for Run, sec-per-
    100 m for Swim. This task's `ResolvePace` reproduces exactly this shape (duration over distance-in-
    units), not its inverse.
  - `api/Bryk.Application/Training/Load/LoadCalculator.cs:91–126` (`ActualCardioTss`) — **read only,
    frozen for Phase 19.** Confirms the four values a synthetic `WorkoutStepResult` will need later
    (19-4's job, not this task's): `AvgPower`, `AvgPace`, `AvgHr`, `ActualDurationSeconds`/
    `ActualDistanceMeters`.
  - `api/Bryk.Application/Exceptions/ValidationException.cs` — `ValidationException(IEnumerable<string>
    errors)`, exposes `Errors`. Constructed directly with `new[] { "File: ..." }` in this task — no
    validator, no `ValidateOrThrowAsync` call site (there is nothing to validate against a schema; the
    "validation" here is XML well-formedness and track-data presence).
  - `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs:33–47` — confirms
    `Bryk.Application.Exceptions.ValidationException` → 400 with `errors[]`; no middleware edit needed or
    permitted in this task.
  - `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs` — the pure-calculator
    convention to mirror for `ZoneHistogramCalculator`: `public static class`, private `const`
    thresholds, one public entry point, XML `<summary>` naming the ADR section, no `DateTime.UtcNow`,
    no I/O.
  - `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs` — layout to mirror: private
    factory helpers at the top, one `[Fact]`/`[Theory]` per pinned case, FluentAssertions, exact values,
    no tolerance ranges except where the task itself calls for one (the GPX haversine test).
  - `api/Bryk.API.Tests/Bryk.API.Tests.csproj` — currently **no** `<ItemGroup>` for content files; this
    task adds the first one. `ProjectReference` to `Bryk.API` (which references `Infrastructure`) is what
    makes this the only test project that can `new TcxActivityParser()`/`new GpxActivityParser()`.
  - `api/Bryk.Application.Tests/Bryk.Application.Tests.csproj` — `ProjectReference` to `Bryk.Application`
    **only**. Confirms `ZoneHistogramCalculatorTests.cs` must not try to construct a parser.
- Confirm `Bryk.Domain.Entities.ZoneMetric` is `Power = 1, Hr = 2, Pace = 3` — reused verbatim, no new
  enum introduced by this task.

## Step 1 — `ParsedActivity.cs` (+ `ActivitySample`)

**New file** `api/Bryk.Application/ActivityFiles/ParsedActivity.cs` (new folder). No dependents yet, so
this lands before the interface that returns it — mirrors `WeeklyTargetDto` landing before
`WeeklyTargetCalculator` in Task 18-1.

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

/// <summary>
/// The result of parsing one activity file (ADR-0010 §1/§6): session aggregates plus an in-memory
/// sample series. <see cref="Samples"/> is never persisted (ADR-0010 §6) — <see cref="ZoneHistogramCalculator"/>
/// reduces it to a 5-bucket histogram that Task 19-4 does persist. Deliberately carries no zone buckets
/// itself: bucketing needs the athlete's zones, an Application/service concern, not a parser concern.
///
/// Cross-format resolution rules — identical in <see cref="TcxActivityParser"/>, <see cref="GpxActivityParser"/>
/// and Task 19-3's FIT parser, stated once here:
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
```

Notes:
- Both records live in **one file**, mirroring `WeeklyTargetInput` sharing `WeeklyTargetCalculator.cs`.
- The `<see cref="ZoneHistogramCalculator"/>`, `<see cref="TcxActivityParser"/>`, `<see
  cref="GpxActivityParser"/>` cross-references in the doc comment will show as unresolved `<see cref>`
  warnings only if those types don't exist yet at build time — they don't, yet, in this step. `<see
  cref>` failures are **not** compiler errors (they're XML-doc warnings gated behind
  `GenerateDocumentationFile`, which this project does not set), so this does not break the build. Confirm
  by building immediately below.
- No `using Bryk.Application.Zones;` needed here — this file has no zone-shaped field.

**Verify:** `dotnet build api/Bryk.sln` green, 16 warnings (unreferenced new file, trivial).

## Step 2 — `IActivityFileParser.cs`

**New file** `api/Bryk.Application/ActivityFiles/IActivityFileParser.cs`.

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

/// <summary>
/// One activity-file format's parser (ADR-0010 §1). The service (Task 19-4) selects an implementation by
/// matching <see cref="Format"/>; <see cref="TcxActivityParser"/> and <see cref="GpxActivityParser"/> are
/// the two <see cref="System.Xml.Linq"/>-only implementations this task ships (Task 19-3 adds a third,
/// FIT, behind the same interface — the SDK dependency it needs never leaks past
/// <c>Bryk.Infrastructure</c>).
/// </summary>
public interface IActivityFileParser
{
    /// <summary>The file format this parser handles.</summary>
    ActivityFileFormat Format { get; }

    /// <summary>
    /// Parses one activity file into its session aggregates plus an in-memory sample series
    /// (<see cref="ParsedActivity"/> — samples are never persisted, ADR-0010 §6). Throws
    /// <see cref="Exceptions.ValidationException"/> with a single <c>"File: ..."</c> message when the
    /// content is malformed or carries no track data; the caller does not catch it (the global middleware
    /// maps it to 400) and must not have staged anything before calling. Does not dispose
    /// <paramref name="content"/> — that is the caller's stream.
    /// </summary>
    Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default);
}
```

**Verify:** `dotnet build api/Bryk.sln` green, 16 warnings.

## Step 3 — `ZoneHistogramEntry.cs`

**New file** `api/Bryk.Application/ActivityFiles/ZoneHistogramEntry.cs`.

```csharp
namespace Bryk.Application.ActivityFiles;

/// <summary>
/// One bucket of the derived per-zone seconds histogram (ADR-0010 §5). <see cref="ZoneNumber"/> is 1..5,
/// matching <c>ZoneTimeDto</c>'s buckets so Task 19-6 can add sample-derived and estimate-derived seconds
/// together. <b>This is the persisted JSON's element shape</b> (serialized by Task 19-4, deserialized by
/// Task 19-6) — changing it after Phase 19 ships is a data-format change, not a refactor.
/// </summary>
public sealed record ZoneHistogramEntry(int ZoneNumber, int Seconds);
```

**Verify:** `dotnet build api/Bryk.sln` green, 16 warnings.

## Step 4 — `ZoneHistogramCalculator.cs`

**New file** `api/Bryk.Application/ActivityFiles/ZoneHistogramCalculator.cs`. This is the pure math from
`Tasks-19-2.md`'s algorithm, transcribed exactly — the band lookup is **character-identical** to
`TimeInZoneCalculator.cs:122` (same predicate, same `Math.Min(z, 5)` collapse, same `HrZone` thresholds),
duplicated locally per that task's explicit instruction not to refactor the original into a shared helper.

```csharp
using Bryk.Application.Zones;

namespace Bryk.Application.ActivityFiles;

/// <summary>
/// Pure sample-to-bucket math (ADR-0010 §5, ADR-0007 §4). Pure: no I/O, no <see cref="DateTime.UtcNow"/>.
/// Always returns exactly five entries, <see cref="ZoneHistogramEntry.ZoneNumber"/> 1..5 ascending, even
/// when every bucket is 0. Per-sample duration is the gap to the next sample clamped to
/// <see cref="MaxSampleGapSeconds"/> — a paused/gapped file cannot dump an hour into one bucket — and the
/// last sample always contributes 0 (there is no following sample to bound it).
///
/// The band predicate and the coarse %HRmax fallback are deliberately duplicated from
/// <see cref="Analytics.TimeInZoneCalculator"/> rather than shared: that file belongs to Task 19-6, and
/// Tasks-19-2 records the ~10-line duplication as tech debt rather than coupling the two tasks' files.
/// </summary>
public static class ZoneHistogramCalculator
{
    private const int ZoneCount = 5;
    private const int MaxSampleGapSeconds = 60;

    public static IReadOnlyList<ZoneHistogramEntry> Compute(
        ParsedActivity activity,
        SportZonesResponse? sportZones,
        int? maxHr)
    {
        var seconds = new int[ZoneCount + 1]; // index 1..5; index 0 unused
        var samples = activity.Samples;

        for (var i = 0; i < samples.Count; i++)
        {
            // The last sample has no following sample to bound it, so it contributes 0 seconds.
            var duration = i + 1 < samples.Count
                ? Math.Clamp(samples[i + 1].ElapsedSeconds - samples[i].ElapsedSeconds, 0, MaxSampleGapSeconds)
                : 0;

            if (duration <= 0)
            {
                continue;
            }

            if (ResolveZone(samples[i], sportZones, maxHr) is { } zone)
            {
                seconds[zone] += duration;
            }
            // else: no usable signal on this sample — those seconds are dropped (ADR-0007 §4's honesty
            // rule: the histogram sums to less than the session duration when coverage is partial).
        }

        return Enumerable.Range(1, ZoneCount)
            .Select(z => new ZoneHistogramEntry(z, seconds[z]))
            .ToList();
    }

    // First match wins (Tasks-19-2 §3): Power metric+value, then Pace metric+value, then coarse %HRmax.
    // Once a branch is taken on metric+value presence, a missed band lookup inside that branch means "no
    // bucket" — it does NOT fall through to the next branch (mirrors TimeInZoneCalculator.ClassifyStep,
    // where a null band also returns null rather than trying another provenance).
    private static int? ResolveZone(ActivitySample sample, SportZonesResponse? sportZones, int? maxHr)
    {
        if (sportZones is { Metric: ZoneMetric.Power } && sample.Power is { } power)
        {
            return BandZone(power, sportZones.Zones);
        }

        if (sportZones is { Metric: ZoneMetric.Pace } && sample.PaceSecPerUnit is { } pace)
        {
            return BandZone(pace, sportZones.Zones);
        }

        if (sample.Hr is { } hr && hr > 0 && maxHr is { } max && max > 0)
        {
            return HrZone(hr, max);
        }

        return null;
    }

    // Character-identical to TimeInZoneCalculator.cs:122's predicate — do not invert for pace, do not
    // change the comparison operators. Task 19-6 unions this histogram with that calculator's output, so
    // the two must be commensurable.
    private static int? BandZone(int value, IReadOnlyList<ZoneDto> zones)
    {
        var band = zones.FirstOrDefault(z => value >= z.LowerBound && (z.UpperBound is null || value < z.UpperBound));
        return band is null ? null : Math.Min(band.ZoneNumber, ZoneCount);
    }

    // Coarse %HRmax 5-zone scheme, duplicated from TimeInZoneCalculator.cs:127–138 verbatim (ADR-0007 §4).
    private static int HrZone(int avgHr, int maxHr)
    {
        var pct = (decimal)avgHr / maxHr;
        return pct switch
        {
            < 0.60m => 1,
            < 0.70m => 2,
            < 0.80m => 3,
            < 0.90m => 4,
            _ => 5
        };
    }
}
```

Note: `BandZone(int value, ...)` compares an `int` against `decimal LowerBound`/`UpperBound?` — C#
implicitly widens `int` to `decimal` in that comparison, so no explicit cast is needed (unlike the
`WeeklyTargetCalculator` decimal-only arithmetic in Task 18-1).

**Verify:** `dotnet build api/Bryk.sln` green, 16 warnings (still unreferenced outside its own file).

## Step 5 — Unit tests: `ZoneHistogramCalculatorTests.cs`

**New file** `api/Bryk.Application.Tests/ActivityFiles/ZoneHistogramCalculatorTests.cs` (new folder). No
stubs, no host — pure calls against `ZoneHistogramCalculator.Compute`. One `[Fact]` per pinned case from
`Tasks-19-2.md`'s "Test expectations" section — 8 total.

```csharp
using Bryk.Application.ActivityFiles;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.ActivityFiles;

public class ZoneHistogramCalculatorTests
{
    private static readonly DateTime Start = new(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc);

    private static ParsedActivity Activity(params ActivitySample[] samples) =>
        new(Sport.Run, Start, null, null, null, null, null, null, samples);

    private static SportZonesResponse PowerZones() => new()
    {
        Sport = Sport.Bike,
        Metric = ZoneMetric.Power,
        Zones = new List<ZoneDto>
        {
            new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 150m },
            new() { ZoneNumber = 2, LowerBound = 150m, UpperBound = 200m },
            new() { ZoneNumber = 3, LowerBound = 200m, UpperBound = 250m },
            new() { ZoneNumber = 4, LowerBound = 250m, UpperBound = 300m },
            new() { ZoneNumber = 5, LowerBound = 300m, UpperBound = null },
        }
    };

    private static SportZonesResponse PowerZonesWithSixAndSeven() => new()
    {
        Sport = Sport.Bike,
        Metric = ZoneMetric.Power,
        Zones = new List<ZoneDto>
        {
            new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 150m },
            new() { ZoneNumber = 2, LowerBound = 150m, UpperBound = 200m },
            new() { ZoneNumber = 3, LowerBound = 200m, UpperBound = 250m },
            new() { ZoneNumber = 4, LowerBound = 250m, UpperBound = 300m },
            new() { ZoneNumber = 5, LowerBound = 300m, UpperBound = 350m },
            new() { ZoneNumber = 6, LowerBound = 350m, UpperBound = 400m },
            new() { ZoneNumber = 7, LowerBound = 400m, UpperBound = null },
        }
    };

    private static SportZonesResponse PaceZones() => new()
    {
        Sport = Sport.Run,
        Metric = ZoneMetric.Pace,
        Zones = new List<ZoneDto>
        {
            new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 240m },
            new() { ZoneNumber = 2, LowerBound = 240m, UpperBound = 300m },
            new() { ZoneNumber = 3, LowerBound = 300m, UpperBound = 360m },
            new() { ZoneNumber = 4, LowerBound = 360m, UpperBound = 420m },
            new() { ZoneNumber = 5, LowerBound = 420m, UpperBound = null },
        }
    };

    [Fact]
    public void Compute_AlwaysReturnsFiveBucketsOrderedOneToFive()
    {
        var result = ZoneHistogramCalculator.Compute(Activity(), null, null);

        result.Select(r => r.ZoneNumber).Should().Equal(1, 2, 3, 4, 5);
        result.Should().OnlyContain(r => r.Seconds == 0);
    }

    [Fact]
    public void Compute_PowerSamples_BucketByBand()
    {
        var activity = Activity(
            new ActivitySample(0, null, 100, null),
            new ActivitySample(60, null, 175, null),
            new ActivitySample(120, null, 225, null),
            new ActivitySample(180, null, 275, null));

        var result = ZoneHistogramCalculator.Compute(activity, PowerZones(), null);

        result.Single(r => r.ZoneNumber == 1).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 2).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 4).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 5).Seconds.Should().Be(0); // last sample contributes nothing
    }

    [Fact]
    public void Compute_BikeZoneSixAndSeven_CollapseIntoBucketFive()
    {
        var activity = Activity(
            new ActivitySample(0, null, 450, null), // lands in Z7
            new ActivitySample(60, null, 450, null));

        var result = ZoneHistogramCalculator.Compute(activity, PowerZonesWithSixAndSeven(), null);

        result.Single(r => r.ZoneNumber == 5).Seconds.Should().Be(60);
    }

    [Fact]
    public void Compute_PaceMetricUsesTheSamePredicateAsTimeInZone()
    {
        var activity = Activity(
            new ActivitySample(0, null, null, 300), // exactly Z3's LowerBound (inclusive)
            new ActivitySample(60, null, null, 239), // just below Z1's UpperBound of 240 (exclusive)
            new ActivitySample(120, null, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, PaceZones(), null);

        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(60);
        result.Single(r => r.ZoneNumber == 1).Seconds.Should().Be(60);
    }

    [Fact]
    public void Compute_NoZones_FallsBackToPercentOfMaxHr()
    {
        var activity = Activity(
            new ActivitySample(0, 119, null, null),   // 59.5% → Z1
            new ActivitySample(20, 120, null, null),  // exactly 60% → Z2 (boundary lands in the higher bucket)
            new ActivitySample(40, 140, null, null),  // exactly 70% → Z3
            new ActivitySample(60, 160, null, null),  // exactly 80% → Z4
            new ActivitySample(80, 180, null, null),  // exactly 90% → Z5
            new ActivitySample(100, 190, null, null)); // trailing sample — contributes nothing

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: 200);

        result.Single(r => r.ZoneNumber == 1).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 2).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 4).Seconds.Should().Be(20);
        result.Single(r => r.ZoneNumber == 5).Seconds.Should().Be(20);
    }

    [Fact]
    public void Compute_GapLongerThanSixtySeconds_IsClampedToSixty()
    {
        var activity = Activity(
            new ActivitySample(0, 150, null, null),
            new ActivitySample(600, 150, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: 200); // 150/200 = 75% → Z3

        result.Single(r => r.ZoneNumber == 3).Seconds.Should().Be(60);
        result.Sum(r => r.Seconds).Should().Be(60);
    }

    [Fact]
    public void Compute_LastSampleContributesZeroSeconds()
    {
        var activity = Activity(
            new ActivitySample(0, 150, null, null),
            new ActivitySample(45, 150, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: 200);

        result.Sum(r => r.Seconds).Should().Be(45);
    }

    [Fact]
    public void Compute_SamplesWithNoUsableSignal_AreDroppedFromEveryBucket()
    {
        var activity = Activity(
            new ActivitySample(0, null, null, null),
            new ActivitySample(30, null, null, null));

        var result = ZoneHistogramCalculator.Compute(activity, null, maxHr: null);

        // The histogram sum (0) is less than the 30-second session — correct per ADR-0007 §4's honesty
        // rule: 19-6 counts only what is measured, never fabricates coverage.
        result.Should().OnlyContain(r => r.Seconds == 0);
    }
}
```

**Verify:**
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln --filter FullyQualifiedName~ZoneHistogramCalculatorTests
```
Build green, 16 warnings (unchanged). All 8 facts pass by name:
`Compute_AlwaysReturnsFiveBucketsOrderedOneToFive`, `Compute_PowerSamples_BucketByBand`,
`Compute_BikeZoneSixAndSeven_CollapseIntoBucketFive`, `Compute_PaceMetricUsesTheSamePredicateAsTimeInZone`,
`Compute_NoZones_FallsBackToPercentOfMaxHr`, `Compute_GapLongerThanSixtySeconds_IsClampedToSixty`,
`Compute_LastSampleContributesZeroSeconds`, `Compute_SamplesWithNoUsableSignal_AreDroppedFromEveryBucket`.

## Step 6 — `ActivitySampleBounds.cs`

**New file** `api/Bryk.Infrastructure/ActivityFiles/ActivitySampleBounds.cs` (new folder). This is the
**only** file in this task under `Bryk.Infrastructure` before the parsers — it has zero dependents in
`Bryk.Application`, so it lands first there and is verified standalone.

```csharp
namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// Sample sanity at the parse boundary (ROADMAP's "sample sanity (HR 30–230 etc.)"), so a corrupt device
/// spike never reaches the service. Out-of-range values become null on that sample — the sample itself is
/// retained (its elapsed time still counts toward duration), it simply contributes nothing to the average,
/// the max, or the histogram bucket. Task 19-4 does not own sample sanity; Task 19-3's FIT parser reuses
/// this type read-only rather than redeclaring the constants.
/// </summary>
internal static class ActivitySampleBounds
{
    public const int MinHr = 30;
    public const int MaxHr = 230;
    public const int MaxPowerWatts = 2000;

    public static int? Hr(int? value) => value is { } v && v >= MinHr && v <= MaxHr ? v : null;
    public static int? Power(int? value) => value is { } v && v >= 0 && v <= MaxPowerWatts ? v : null;
}
```

`internal` — no sibling task outside `Bryk.Infrastructure` needs it (19-3's FIT parser lives in the same
project/assembly, so `internal` is still visible to it).

**Verify:** `dotnet build api/Bryk.sln` green, 16 warnings.

## Step 7 — `TcxActivityParser.cs`

**New file** `api/Bryk.Infrastructure/ActivityFiles/TcxActivityParser.cs`. `System.Xml.Linq` only — no
package reference anywhere. Two-pass parse: pass 1 reads every trackpoint's raw `(Time, Hr, Power,
CumulativeDistance)` tuple (applying `ActivitySampleBounds` immediately, so out-of-range values are
already null by the time sport resolution and averaging run); pass 2 resolves `Sport` (needed to decide
whether per-sample pace applies at all) and then builds the final `ActivitySample` list with elapsed
seconds and pace.

```csharp
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;

namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// Parses a Garmin Training Center XML (.tcx) activity file into a <see cref="ParsedActivity"/> using
/// <see cref="System.Xml.Linq"/> — no package (ADR-0010 §1). Implements the cross-format resolution rules
/// documented on <see cref="ParsedActivity"/> (sport, session averages, duration/distance, pace).
/// </summary>
public class TcxActivityParser : IActivityFileParser
{
    private static readonly XNamespace Tcx = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
    private static readonly XNamespace Tpx = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

    public ActivityFileFormat Format => ActivityFileFormat.Tcx;

    public async Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default)
    {
        XDocument doc;
        try
        {
            doc = await XDocument.LoadAsync(content, LoadOptions.None, ct);
        }
        catch (XmlException)
        {
            throw new ValidationException(new[] { "File: The .tcx file could not be parsed." });
        }

        if (doc.Root is not { } root || root.Name != Tcx + "TrainingCenterDatabase")
        {
            throw new ValidationException(new[] { "File: The file is not a valid .tcx activity." });
        }

        try
        {
            var activityElement = root.Descendants(Tcx + "Activity").FirstOrDefault();
            var laps = activityElement?.Elements(Tcx + "Lap").ToList() ?? new List<XElement>();
            var trackpoints = laps.SelectMany(lap => lap.Descendants(Tcx + "Trackpoint")).ToList();

            // Pass 1 — raw per-trackpoint values. A trackpoint with no <Time> is skipped entirely.
            var raw = new List<(DateTime Time, int? Hr, int? Power, int? CumulativeDistance)>();
            foreach (var tp in trackpoints)
            {
                var timeText = tp.Element(Tcx + "Time")?.Value;
                if (string.IsNullOrWhiteSpace(timeText))
                {
                    continue;
                }

                var time = DateTime.Parse(timeText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                var hrText = tp.Element(Tcx + "HeartRateBpm")?.Element(Tcx + "Value")?.Value;
                var hr = ActivitySampleBounds.Hr(ParseIntOrNull(hrText));

                var wattsText = tp.Element(Tcx + "Extensions")?.Element(Tpx + "TPX")?.Element(Tpx + "Watts")?.Value;
                var power = ActivitySampleBounds.Power(ParseIntOrNull(wattsText));

                var distance = ParseIntOrNull(tp.Element(Tcx + "DistanceMeters")?.Value);

                raw.Add((time, hr, power, distance));
            }

            if (raw.Count == 0)
            {
                throw new ValidationException(new[] { "File: The file contains no track data." });
            }

            var startTimeUtc = raw[0].Time;
            var sport = ResolveSport(activityElement?.Attribute("Sport")?.Value, raw.Select(r => r.Power));

            // Pass 2 — elapsed seconds + per-sample pace (Run/Swim only), from the cumulative-distance
            // delta between consecutive trackpoints.
            var samples = new List<ActivitySample>(raw.Count);
            for (var i = 0; i < raw.Count; i++)
            {
                var elapsedSeconds = (int)Math.Round((raw[i].Time - startTimeUtc).TotalSeconds);
                samples.Add(new ActivitySample(elapsedSeconds, raw[i].Hr, raw[i].Power, SamplePace(sport, raw, i)));
            }

            var lapDurations = laps.Select(l => ParseDoubleOrNull(l.Element(Tcx + "TotalTimeSeconds")?.Value))
                .Where(v => v is not null).Select(v => v!.Value).ToList();
            var durationSeconds = lapDurations.Count > 0 ? (int)Math.Round(lapDurations.Sum()) : samples[^1].ElapsedSeconds;

            var lapDistances = laps.Select(l => ParseDoubleOrNull(l.Element(Tcx + "DistanceMeters")?.Value))
                .Where(v => v is not null).Select(v => v!.Value).ToList();
            var distanceMeters = lapDistances.Count > 0 ? (int)Math.Round(lapDistances.Sum()) : raw[^1].CumulativeDistance;

            var avgHr = Average(samples.Select(s => s.Hr));
            var maxHr = Max(samples.Select(s => s.Hr));
            var avgPower = Average(samples.Select(s => s.Power));
            var avgPace = ResolvePace(sport, durationSeconds, distanceMeters);

            return new ParsedActivity(sport, startTimeUtc, durationSeconds, distanceMeters, avgHr, maxHr, avgPower, avgPace, samples);
        }
        catch (FormatException)
        {
            throw new ValidationException(new[] { "File: The .tcx file could not be parsed." });
        }
    }

    // §Sport fallback chain (ParsedActivity.cs rule 1): recognised file metadata → Bike if any sample
    // carries power → Run. "Other"/absent both fall through to the same default arm.
    private static Sport ResolveSport(string? sportAttribute, IEnumerable<int?> powers) => sportAttribute switch
    {
        "Running" => Sport.Run,
        "Biking" => Sport.Bike,
        "Swimming" => Sport.Swim,
        _ => powers.Any(p => p is not null) ? Sport.Bike : Sport.Run
    };

    private static int? SamplePace(Sport sport, List<(DateTime Time, int? Hr, int? Power, int? CumulativeDistance)> raw, int i)
    {
        if (i == 0 || (sport != Sport.Run && sport != Sport.Swim))
        {
            return null;
        }

        if (raw[i].CumulativeDistance is not { } distance || raw[i - 1].CumulativeDistance is not { } previous || distance <= previous)
        {
            return null;
        }

        var deltaSeconds = (raw[i].Time - raw[i - 1].Time).TotalSeconds;
        var unit = sport == Sport.Run ? 1000m : 100m;
        return (int)Math.Round((decimal)deltaSeconds / ((distance - previous) / unit));
    }

    // Rule 4: DurationSeconds / (DistanceMeters / unit), Run/Swim only, both > 0.
    private static int? ResolvePace(Sport sport, int? durationSeconds, int? distanceMeters)
    {
        if ((sport != Sport.Run && sport != Sport.Swim) || durationSeconds is not { } dur || dur <= 0
            || distanceMeters is not { } dist || dist <= 0)
        {
            return null;
        }

        var unit = sport == Sport.Run ? 1000m : 100m;
        return (int)Math.Round((decimal)dur / (dist / unit));
    }

    private static int? Average(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : (int)Math.Round(present.Average());
    }

    private static int? Max(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : present.Max();
    }

    private static int? ParseIntOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : (int)Math.Round(double.Parse(text, CultureInfo.InvariantCulture));

    private static double? ParseDoubleOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : double.Parse(text, CultureInfo.InvariantCulture);
}
```

Rationale for the two-pass structure: `Sport` must be known before per-sample pace can be computed
(pace only applies to Run/Swim), but `Sport`'s own fallback (rule 1b) needs to inspect **all** samples'
power values first — so raw extraction (pass 1) must fully complete, feeding sport resolution, before
pass 2 can assign `PaceSecPerUnit`. Collapsing this into one pass would require resolving sport from a
partial sample set, which is wrong whenever the file's own `Sport` attribute is unrecognised and the
power-bearing samples come later in the file.

**Verify:** `dotnet build api/Bryk.sln` green, 16 warnings. (Unreferenced by any test yet — fixtures and
tests land in Steps 9–10.)

## Step 8 — `GpxActivityParser.cs`

**New file** `api/Bryk.Infrastructure/ActivityFiles/GpxActivityParser.cs`. Same two-pass shape as the TCX
parser, but distance is derived (haversine) rather than declared, and there is no power extension at all
in GPX 1.1.

```csharp
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;

namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// Parses a GPX 1.1 (.gpx) track into a <see cref="ParsedActivity"/> using <see cref="System.Xml.Linq"/>
/// — no package (ADR-0010 §1). GPX 1.1 carries no power extension: <see cref="ParsedActivity.AvgPower"/>
/// is always null here and no sample carries a power value — do not chase vendor power extensions in v1.
/// Implements the cross-format resolution rules documented on <see cref="ParsedActivity"/>.
/// </summary>
public class GpxActivityParser : IActivityFileParser
{
    private const double EarthRadiusMeters = 6371000d;

    private static readonly XNamespace Gpx = "http://www.topografix.com/GPX/1/1";
    private static readonly XNamespace Tpx1 = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    public ActivityFileFormat Format => ActivityFileFormat.Gpx;

    public async Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default)
    {
        XDocument doc;
        try
        {
            doc = await XDocument.LoadAsync(content, LoadOptions.None, ct);
        }
        catch (XmlException)
        {
            throw new ValidationException(new[] { "File: The .gpx file could not be parsed." });
        }

        if (doc.Root is not { } root || root.Name != Gpx + "gpx")
        {
            throw new ValidationException(new[] { "File: The file is not a valid .gpx activity." });
        }

        try
        {
            var track = root.Descendants(Gpx + "trk").FirstOrDefault();
            var trackType = track?.Element(Gpx + "type")?.Value;
            var trackpoints = track?.Descendants(Gpx + "trkpt").ToList() ?? new List<XElement>();

            // Pass 1 — raw per-point values. A point with no <time> is skipped entirely.
            var raw = new List<(DateTime Time, double Lat, double Lon, int? Hr)>();
            foreach (var pt in trackpoints)
            {
                var timeText = pt.Element(Gpx + "time")?.Value;
                if (string.IsNullOrWhiteSpace(timeText))
                {
                    continue;
                }

                var time = DateTime.Parse(timeText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                var lat = double.Parse(pt.Attribute("lat")!.Value, CultureInfo.InvariantCulture);
                var lon = double.Parse(pt.Attribute("lon")!.Value, CultureInfo.InvariantCulture);

                var hrText = pt.Element(Gpx + "extensions")?.Element(Tpx1 + "TrackPointExtension")?.Element(Tpx1 + "hr")?.Value;
                var hr = ActivitySampleBounds.Hr(string.IsNullOrWhiteSpace(hrText)
                    ? null
                    : (int)Math.Round(double.Parse(hrText, CultureInfo.InvariantCulture)));

                raw.Add((time, lat, lon, hr));
            }

            if (raw.Count == 0)
            {
                throw new ValidationException(new[] { "File: The file contains no track data." });
            }

            var startTimeUtc = raw[0].Time;
            var sport = ResolveSport(trackType);

            // Pass 2 — elapsed seconds + running haversine distance + per-sample pace (Run/Swim only).
            var samples = new List<ActivitySample>(raw.Count);
            var totalDistanceMeters = 0d;
            for (var i = 0; i < raw.Count; i++)
            {
                var elapsedSeconds = (int)Math.Round((raw[i].Time - startTimeUtc).TotalSeconds);
                int? pace = null;

                if (i > 0)
                {
                    var segmentMeters = Haversine(raw[i - 1].Lat, raw[i - 1].Lon, raw[i].Lat, raw[i].Lon);
                    totalDistanceMeters += segmentMeters;

                    if ((sport == Sport.Run || sport == Sport.Swim) && segmentMeters > 0)
                    {
                        var deltaSeconds = (raw[i].Time - raw[i - 1].Time).TotalSeconds;
                        var unit = sport == Sport.Run ? 1000d : 100d;
                        pace = (int)Math.Round(deltaSeconds / (segmentMeters / unit));
                    }
                }

                samples.Add(new ActivitySample(elapsedSeconds, raw[i].Hr, null, pace));
            }

            var durationSeconds = samples[^1].ElapsedSeconds;
            var distanceMeters = (int)Math.Round(totalDistanceMeters); // rounded once at the end, not per segment
            var avgHr = Average(samples.Select(s => s.Hr));
            var maxHr = Max(samples.Select(s => s.Hr));
            var avgPace = ResolvePace(sport, durationSeconds, distanceMeters);

            return new ParsedActivity(sport, startTimeUtc, durationSeconds, distanceMeters, avgHr, maxHr, null, avgPace, samples);
        }
        catch (FormatException)
        {
            throw new ValidationException(new[] { "File: The .gpx file could not be parsed." });
        }
    }

    // §Sport fallback chain: case-insensitive Contains on <type>. GPX 1.1 never carries a power sample
    // (no vendor power extension chased in v1), so rule 1b ("Bike if any sample carries power") can never
    // fire here — the fallback is always Run, made explicit rather than looping over an always-empty
    // power check.
    private static Sport ResolveSport(string? trackType)
    {
        if (!string.IsNullOrWhiteSpace(trackType))
        {
            if (trackType.Contains("run", StringComparison.OrdinalIgnoreCase)) return Sport.Run;
            if (trackType.Contains("bik", StringComparison.OrdinalIgnoreCase)
                || trackType.Contains("cycl", StringComparison.OrdinalIgnoreCase)
                || trackType.Contains("ride", StringComparison.OrdinalIgnoreCase)) return Sport.Bike;
            if (trackType.Contains("swim", StringComparison.OrdinalIgnoreCase)) return Sport.Swim;
        }

        return Sport.Run;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static int? ResolvePace(Sport sport, int? durationSeconds, int? distanceMeters)
    {
        if ((sport != Sport.Run && sport != Sport.Swim) || durationSeconds is not { } dur || dur <= 0
            || distanceMeters is not { } dist || dist <= 0)
        {
            return null;
        }

        var unit = sport == Sport.Run ? 1000d : 100d;
        return (int)Math.Round(dur / (dist / unit));
    }

    private static int? Average(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : (int)Math.Round(present.Average());
    }

    private static int? Max(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : present.Max();
    }
}
```

**Trap to avoid** (the kind of bug this parser is easy to introduce and hard to notice): the
`ParsedActivity` positional constructor is `(Sport, StartTimeUtc, DurationSeconds, DistanceMeters, AvgHr,
MaxHr, AvgPower, AvgPace, Samples)` — position 6 is `MaxHr`, position 7 is `AvgPower`. Passing `null` for
`AvgPower` at position 7 (as this parser must, GPX has none) while still passing the computed `maxHr`
variable at position 6 is correct; passing `null` at position 6 instead would silently zero out `MaxHr`
for every GPX import. Re-read the `return new ParsedActivity(...)` line above against this order before
moving on.

**Verify:** `dotnet build api/Bryk.sln` green, 16 warnings.

## Step 9 — Fixtures + `Bryk.API.Tests.csproj` glob

**New folder** `api/Bryk.API.Tests/Fixtures/ActivityFiles/`, three files, contents **exact** — the tests
in Steps 10–11 pin the derived numbers computed from these bytes.

**`api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-run.tcx`:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
  <Activities>
    <Activity Sport="Running">
      <Id>2026-06-01T06:00:00Z</Id>
      <Lap StartTime="2026-06-01T06:00:00Z">
        <TotalTimeSeconds>600</TotalTimeSeconds>
        <DistanceMeters>2000</DistanceMeters>
        <Track>
          <Trackpoint>
            <Time>2026-06-01T06:00:00Z</Time>
            <DistanceMeters>0</DistanceMeters>
            <HeartRateBpm><Value>120</Value></HeartRateBpm>
          </Trackpoint>
          <Trackpoint>
            <Time>2026-06-01T06:02:30Z</Time>
            <DistanceMeters>500</DistanceMeters>
            <HeartRateBpm><Value>140</Value></HeartRateBpm>
          </Trackpoint>
          <Trackpoint>
            <Time>2026-06-01T06:05:00Z</Time>
            <DistanceMeters>1000</DistanceMeters>
            <HeartRateBpm><Value>150</Value></HeartRateBpm>
          </Trackpoint>
          <Trackpoint>
            <Time>2026-06-01T06:07:30Z</Time>
            <DistanceMeters>1500</DistanceMeters>
            <HeartRateBpm><Value>160</Value></HeartRateBpm>
          </Trackpoint>
          <Trackpoint>
            <Time>2026-06-01T06:10:00Z</Time>
            <DistanceMeters>2000</DistanceMeters>
            <HeartRateBpm><Value>150</Value></HeartRateBpm>
          </Trackpoint>
        </Track>
      </Lap>
    </Activity>
  </Activities>
</TrainingCenterDatabase>
```
Expected (Step 10 pins these): `Sport == Run`, `DurationSeconds == 600` (lap total), `DistanceMeters ==
2000` (lap total), `AvgHr == 144` (mean of 120/140/150/160/150 = 720/5), `MaxHr == 160`, `AvgPower ==
null`, `AvgPace == 300` (600 / (2000/1000)), `Samples.Count == 5`, `StartTimeUtc ==
2026-06-01T06:00:00Z`.

**`api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.tcx`:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
  <Activities>
    <Activity Sport="Biking">
      <Id>2026-06-02T06:00:00Z</Id>
      <Lap StartTime="2026-06-02T06:00:00Z">
        <TotalTimeSeconds>3600</TotalTimeSeconds>
        <DistanceMeters>30000</DistanceMeters>
        <Track>
          <Trackpoint>
            <Time>2026-06-02T06:00:00Z</Time>
            <DistanceMeters>0</DistanceMeters>
            <HeartRateBpm><Value>130</Value></HeartRateBpm>
            <Extensions>
              <TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2">
                <Watts>200</Watts>
              </TPX>
            </Extensions>
          </Trackpoint>
          <Trackpoint>
            <Time>2026-06-02T06:20:00Z</Time>
            <DistanceMeters>7500</DistanceMeters>
            <HeartRateBpm><Value>145</Value></HeartRateBpm>
            <Extensions>
              <TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2">
                <Watts>220</Watts>
              </TPX>
            </Extensions>
          </Trackpoint>
          <Trackpoint>
            <Time>2026-06-02T06:40:00Z</Time>
            <DistanceMeters>15000</DistanceMeters>
            <HeartRateBpm><Value>150</Value></HeartRateBpm>
            <Extensions>
              <TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2">
                <Watts>240</Watts>
              </TPX>
            </Extensions>
          </Trackpoint>
          <Trackpoint>
            <Time>2026-06-02T07:00:00Z</Time>
            <DistanceMeters>30000</DistanceMeters>
            <HeartRateBpm><Value>140</Value></HeartRateBpm>
            <Extensions>
              <TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2">
                <Watts>180</Watts>
              </TPX>
            </Extensions>
          </Trackpoint>
        </Track>
      </Lap>
    </Activity>
  </Activities>
</TrainingCenterDatabase>
```
Expected: `Sport == Bike`, `DurationSeconds == 3600`, `DistanceMeters == 30000`, `AvgHr == 141` (mean of
130/145/150/140 = 565/4 = 141.25 → 141), `AvgPower == 210` (mean of 200/220/240/180 = 840/4), `AvgPace ==
null`, `Samples.Count == 4`.

**`api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-activity.gpx`:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="Bryk fixture" xmlns="http://www.topografix.com/GPX/1/1"
     xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
  <trk>
    <name>Sample Run</name>
    <type>running</type>
    <trkseg>
      <trkpt lat="40.000000" lon="-105.000000">
        <time>2026-06-03T06:00:00Z</time>
        <extensions>
          <gpxtpx:TrackPointExtension>
            <gpxtpx:hr>130</gpxtpx:hr>
          </gpxtpx:TrackPointExtension>
        </extensions>
      </trkpt>
      <trkpt lat="40.008993" lon="-105.000000">
        <time>2026-06-03T06:05:00Z</time>
        <extensions>
          <gpxtpx:TrackPointExtension>
            <gpxtpx:hr>140</gpxtpx:hr>
          </gpxtpx:TrackPointExtension>
        </extensions>
      </trkpt>
      <trkpt lat="40.017986" lon="-105.000000">
        <time>2026-06-03T06:10:00Z</time>
        <extensions>
          <gpxtpx:TrackPointExtension>
            <gpxtpx:hr>150</gpxtpx:hr>
          </gpxtpx:TrackPointExtension>
        </extensions>
      </trkpt>
    </trkseg>
  </trk>
</gpx>
```
Expected: `Sport == Run` (from `<type>running</type>`), `DurationSeconds == 600`, `DistanceMeters` in
`[1995, 2005]` (two ~0.008993°-latitude steps, each ≈ 999.98 m at `R = 6 371 000 m` — haversine, not
exact, hence the range), `AvgHr == 140` (mean of 130/140/150), `MaxHr == 150`, `AvgPace` in `[298, 302]`,
`AvgPower == null`.

**`api/Bryk.API.Tests/Bryk.API.Tests.csproj`** — add **one** new `ItemGroup`, after the existing
`ProjectReference` group, before `</Project>`:
```xml
  <ItemGroup>
    <None Update="Fixtures\ActivityFiles\**">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```
Written as a glob (not three explicit `<None>` entries) so Task 19-3 can drop its `.fit` fixture into the
same folder without touching this file again. SDK-style projects already include non-code files as
implicit `<None>` items by default, so `Update` (not `Include`) is correct here — it attaches the
`CopyToOutputDirectory` metadata to files the SDK has already discovered.

**Verify:** `dotnet build api/Bryk.sln` green, **still 16 warnings** — a content-only glob must not add
an MSBuild warning; if one appears (e.g. a duplicate-item warning), the glob is colliding with an
existing implicit item and needs `Remove` first, not suppression. Manually confirm (e.g.
`Get-ChildItem api\Bryk.API.Tests\bin\Debug\net10.0\Fixtures\ActivityFiles\` after a build) that all
three files land in the output directory.

## Step 10 — Tests: `TcxActivityParserTests.cs`

**New file** `api/Bryk.API.Tests/ActivityFiles/TcxActivityParserTests.cs` (new folder). These live in
`Bryk.API.Tests` because it is the only test project with a path to `Bryk.Infrastructure` — no
`BrykWebApplicationFactory`, no host; each test constructs `new TcxActivityParser()` directly and calls
`ParseAsync` against either a fixture stream or an inline XML string. 10 facts, exact values pinned by
`Tasks-19-2.md`.

```csharp
using System.Text;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.ActivityFiles;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class TcxActivityParserTests
{
    private static Stream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    private static Stream Inline(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public void Format_IsTcx()
    {
        new TcxActivityParser().Format.Should().Be(ActivityFileFormat.Tcx);
    }

    [Fact]
    public async Task ParseAsync_RunFixture_PinsSessionAggregates()
    {
        await using var stream = Fixture("sample-run.tcx");

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
        result.DurationSeconds.Should().Be(600);
        result.DistanceMeters.Should().Be(2000);
        result.AvgHr.Should().Be(144);
        result.MaxHr.Should().Be(160);
        result.AvgPower.Should().BeNull();
        result.AvgPace.Should().Be(300);
        result.Samples.Should().HaveCount(5);
        result.StartTimeUtc.Should().Be(new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ParseAsync_RideFixture_DetectsBikeAndAveragesPower()
    {
        await using var stream = Fixture("sample-ride.tcx");

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Bike);
        result.DurationSeconds.Should().Be(3600);
        result.DistanceMeters.Should().Be(30000);
        result.AvgHr.Should().Be(141);
        result.AvgPower.Should().Be(210);
        result.AvgPace.Should().BeNull();
        result.Samples.Should().HaveCount(4);
    }

    [Fact]
    public async Task ParseAsync_OutOfRangeHeartRate_IsDiscardedWithoutDroppingTheSample()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Running">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint><Time>2026-06-01T06:00:00Z</Time><HeartRateBpm><Value>10</Value></HeartRateBpm></Trackpoint>
                      <Trackpoint><Time>2026-06-01T06:01:00Z</Time><HeartRateBpm><Value>150</Value></HeartRateBpm></Trackpoint>
                      <Trackpoint><Time>2026-06-01T06:02:00Z</Time><HeartRateBpm><Value>300</Value></HeartRateBpm></Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.AvgHr.Should().Be(150);
        result.MaxHr.Should().Be(150);
        result.Samples.Should().HaveCount(3);
        result.Samples[0].Hr.Should().BeNull();
        result.Samples[^1].Hr.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_PowerAboveTwoThousandWatts_IsDiscarded()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Biking">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint>
                        <Time>2026-06-01T06:00:00Z</Time>
                        <Extensions><TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2"><Watts>5000</Watts></TPX></Extensions>
                      </Trackpoint>
                      <Trackpoint>
                        <Time>2026-06-01T06:01:00Z</Time>
                        <Extensions><TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2"><Watts>200</Watts></TPX></Extensions>
                      </Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.AvgPower.Should().Be(200);
    }

    [Fact]
    public async Task ParseAsync_MalformedXml_ThrowsValidationExceptionWithFilePrefix()
    {
        await using var stream = Inline("<not xml");

        var act = () => new TcxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task ParseAsync_WrongRootElement_ThrowsValidationException()
    {
        const string gpx = """
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg><trkpt lat="0" lon="0"><time>2026-01-01T00:00:00Z</time></trkpt></trkseg></trk>
            </gpx>
            """;
        await using var stream = Inline(gpx);

        var act = () => new TcxActivityParser().ParseAsync(stream);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ParseAsync_NoTrackpoints_ThrowsValidationException()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Running">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track></Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var act = () => new TcxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.Contains("no track data"));
    }

    [Fact]
    public async Task ParseAsync_UnknownSportAttributeWithPowerSamples_FallsBackToBike()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Other">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint>
                        <Time>2026-06-01T06:00:00Z</Time>
                        <Extensions><TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2"><Watts>150</Watts></TPX></Extensions>
                      </Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Bike);
    }

    [Fact]
    public async Task ParseAsync_UnknownSportAttributeWithoutPower_FallsBackToRun()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Other">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint><Time>2026-06-01T06:00:00Z</Time></Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
    }
}
```

**Verify:**
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln --filter FullyQualifiedName~TcxActivityParserTests
```
Build green, 16 warnings. All 10 facts pass by name (listed in `Tasks-19-2.md`'s Test expectations).
If `ParseAsync_RunFixture_PinsSessionAggregates` or `ParseAsync_RideFixture_DetectsBikeAndAveragesPower`
fails on a value, re-check the fixture file byte-for-byte against Step 9 before touching the parser code
— a mismatch here usually means the fixture drifted, not the math.

## Step 11 — Tests: `GpxActivityParserTests.cs`

**New file** `api/Bryk.API.Tests/ActivityFiles/GpxActivityParserTests.cs`. 7 facts.

```csharp
using System.Text;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.ActivityFiles;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class GpxActivityParserTests
{
    private static Stream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    private static Stream Inline(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public void Format_IsGpx()
    {
        new GpxActivityParser().Format.Should().Be(ActivityFileFormat.Gpx);
    }

    [Fact]
    public async Task ParseAsync_Fixture_DerivesDistanceFromHaversine()
    {
        // Great-circle arithmetic, not exact: two ~0.008993°-latitude steps at R = 6 371 000 m each
        // compute to ≈ 999.98 m, not precisely 1000 m — the only tolerance range in this suite.
        await using var stream = Fixture("sample-activity.gpx");

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.DistanceMeters.Should().BeInRange(1995, 2005);
    }

    [Fact]
    public async Task ParseAsync_Fixture_ReadsTheHeartRateExtension()
    {
        await using var stream = Fixture("sample-activity.gpx");

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.AvgHr.Should().Be(140);
        result.MaxHr.Should().Be(150);
    }

    [Fact]
    public async Task ParseAsync_Fixture_ResolvesRunFromTrackType()
    {
        await using var stream = Fixture("sample-activity.gpx");

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
        result.DurationSeconds.Should().Be(600);
        result.AvgPace.Should().BeInRange(298, 302);
        result.AvgPower.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_MissingTrackType_FallsBackToRun()
    {
        const string xml = """
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="40.000000" lon="-105.000000"><time>2026-01-01T00:00:00Z</time></trkpt>
                  <trkpt lat="40.001000" lon="-105.000000"><time>2026-01-01T00:01:00Z</time></trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;
        await using var stream = Inline(xml);

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
    }

    [Fact]
    public async Task ParseAsync_MalformedXml_ThrowsValidationExceptionWithFilePrefix()
    {
        await using var stream = Inline("<not xml");

        var act = () => new GpxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task ParseAsync_NoTrackPoints_ThrowsValidationException()
    {
        const string xml = """
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg></trkseg></trk>
            </gpx>
            """;
        await using var stream = Inline(xml);

        var act = () => new GpxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.Contains("no track data"));
    }
}
```

Note: 7 facts listed here matches `Tasks-19-2.md`'s count exactly (`Format_IsGpx` plus 6 `ParseAsync_*`).

**Verify:**
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln --filter FullyQualifiedName~GpxActivityParserTests
```
Build green, 16 warnings. All 7 facts pass by name.

## Step 12 — Final verification, smoke check, and commit

Run the full command set from `Tasks-19-2.md`:
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

- `dotnet build` — 0 errors, **16 warnings**, unchanged from the Step 0 baseline. If the count grew,
  find the new warning before doing anything else — the fixture glob and the new `.cs` files are the only
  suspects; neither should introduce one.
- `dotnet test api/Bryk.sln` — the Step 0 starting count **+ 25** (8 `ZoneHistogramCalculatorTests` + 10
  `TcxActivityParserTests` + 7 `GpxActivityParserTests`), zero failures, nothing else broke.
- `pnpm run build` / `pnpm exec vitest run --no-file-parallelism` — green, **exactly 252 / 56 files**,
  byte-for-byte unchanged (this task touches no UI file — if either number moved, something outside this
  task's scope changed; stop and investigate before committing).
- **Manual smoke check** (no host exists yet for these types, so this is a scratch-project or REPL-style
  check, not a live API call): construct `new TcxActivityParser()` and `new GpxActivityParser()` directly
  against each of the three committed fixtures and confirm the parsed `Sport`/`DurationSeconds`/
  `DistanceMeters` match Step 9's expected values one more time by eye — this is the same assertion the
  automated tests already make, done as a final gut-check before commit, not a substitute for it.
- `git add -A && git diff --cached --stat` — confirm **only** these files appear, all additions except the
  one edited `.csproj`:
  - `api/Bryk.Application/ActivityFiles/ParsedActivity.cs` (new)
  - `api/Bryk.Application/ActivityFiles/IActivityFileParser.cs` (new)
  - `api/Bryk.Application/ActivityFiles/ZoneHistogramEntry.cs` (new)
  - `api/Bryk.Application/ActivityFiles/ZoneHistogramCalculator.cs` (new)
  - `api/Bryk.Application.Tests/ActivityFiles/ZoneHistogramCalculatorTests.cs` (new)
  - `api/Bryk.Infrastructure/ActivityFiles/ActivitySampleBounds.cs` (new)
  - `api/Bryk.Infrastructure/ActivityFiles/TcxActivityParser.cs` (new)
  - `api/Bryk.Infrastructure/ActivityFiles/GpxActivityParser.cs` (new)
  - `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-run.tcx` (new)
  - `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.tcx` (new)
  - `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-activity.gpx` (new)
  - `api/Bryk.API.Tests/Bryk.API.Tests.csproj` (extended — one `ItemGroup` only)
  - `api/Bryk.API.Tests/ActivityFiles/TcxActivityParserTests.cs` (new)
  - `api/Bryk.API.Tests/ActivityFiles/GpxActivityParserTests.cs` (new)
  - If the diff shows `Bryk.Infrastructure.csproj`, `Program.cs`, `TimeInZoneCalculator.cs`,
    `TimeInZoneResponse.cs`, `AnalyticsService.cs`, `LoadCalculator.cs`, `Workout.cs`, any migration, or
    anything under `ui/` — **STOP**, that is scope creep beyond `Tasks-19-2.md`'s explicit non-goals.
- Confirm no `dotnet ef` command was run anywhere in this task, and no `PackageReference` was added to any
  `.csproj` — grep the diff for `<PackageReference` if in doubt.
- Commit with the message from `Tasks-19-2.md` (no AI co-author trailer — project convention):

```
feat: activity-file parsing boundary + TCX/GPX parsers + zone histogram

Fix the contract the rest of Phase 19 hangs off: IActivityFileParser in
Bryk.Application takes a stream and returns a ParsedActivity (sport, UTC
start, duration, distance, avg/max HR, avg power, avg pace, and an in-memory
sample series), so the FIT SDK that arrives in the next task never leaks past
Bryk.Infrastructure and the endpoints can be written against one interface
instead of three formats. Samples stay in memory only (ADR-0010 6).

TCX and GPX parse with System.Xml.Linq - no package. One set of resolution
rules covers all three formats: sport from file metadata, else Bike when any
sample carries power, else Run; session averages derived from the retained
samples; duration and distance from the file's declared totals when present;
pace as seconds per km (run) or per 100 m (swim), matching the existing
AnalyticsService convention. Sample sanity lives here, at the parse boundary,
so a corrupt spike never reaches the service: HR outside 30-230 and power
above 2000 W null the value but keep the sample's elapsed time. Malformed
content and empty tracks throw the existing Application ValidationException
with a "File:" message, which the global middleware already maps to 400 - no
new exception type and no middleware change.

ZoneHistogramCalculator is the pure sample-to-bucket math: it reproduces
ADR-0007 4's five buckets, the Math.Min(z,5) collapse, the shared band
predicate and the coarse %HRmax fallback verbatim so Task 19-6 can add
sample-derived and estimate-derived seconds together. Per-sample duration is
the gap to the next sample clamped to 60 s, so a paused file cannot dump an
hour into one bucket, and the final sample contributes nothing.

Three committed XML fixtures pin every aggregate; the csproj glob covers the
folder so the FIT fixture can land later without touching it.
```
