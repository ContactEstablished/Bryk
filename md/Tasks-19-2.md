# Task 19-2 — `IActivityFileParser` + `ParsedActivity` + TCX/GPX parsers + zone-histogram math

## Surface
Backend only, and almost entirely pure. One Application abstraction (`IActivityFileParser`), its result
shape (`ParsedActivity` + `ActivitySample`), one pure static `ZoneHistogramCalculator`, the persisted
histogram entry shape (`ZoneHistogramEntry`), two `System.Xml.Linq` parsers in `Bryk.Infrastructure`, a
shared sanity-bounds constant holder, three committed XML fixtures, and unit tests on both test
projects. **No package, no migration, no service, no controller, no `Program.cs` line, no UI.** Nothing
here is reachable over HTTP or resolvable from DI until 19-4 registers the parsers — that is expected
and is not dead code to "wire up while we're here".

## Why
Everything downstream in Phase 19 hangs off one contract: *bytes in, `ParsedActivity` out*. Fixing that
contract first, in `Bryk.Application`, is what keeps the FIT SDK (Task 19-3) from leaking past
`Bryk.Infrastructure` and what lets 19-4 be written against one interface instead of three formats. The
zone-histogram math ships here rather than in the service for the same reason every other numeric rule
in this codebase does (`LoadCalculator`/`LoadService`, `WeeklyTargetCalculator`/`PeriodizationService`,
`TimeInZoneCalculator`/`AnalyticsService`): a pure function with pinned vectors is testable in
`Bryk.Application.Tests` with no host, no EF and no stubs, and the service is left with pure
orchestration. `.tcx` and `.gpx` are plain XML, so they need no dependency at all — which is why they
lead and FIT follows.

## Depends on
- **Task 19-1** — `ActivityFileFormat` (the enum `IActivityFileParser.Format` returns). Contract only;
  no repository, entity or DbContext use in this task.
- **ADR-0010 §1** (parsers sit behind one Application abstraction; `.tcx`/`.gpx` use `System.Xml.Linq`,
  no package), **§5** (the persisted histogram's shape), **§6** (samples are in-memory only).
- **ADR-0007 §4** — the 5-bucket collapse and the coarse %HRmax scheme the histogram must reproduce
  exactly, so 19-6 can union sample-derived and estimate-derived seconds in one chart.
- **Task 19-3** implements this task's interface. **Task 19-4** consumes both the parsers and the
  calculator. Neither may edit this task's files.

## Required reading
- `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs` — **the shape to mirror and the rules to
  reproduce**. Specifically `ZoneCount = 5` (L14), the band-lookup predicate at L122
  (`v >= z.LowerBound && (z.UpperBound is null || v < z.UpperBound)`), the `Math.Min(z, ZoneCount)`
  collapse (L102/L123), and `HrZone` at L127–138 (`<0.60` → 1, `<0.70` → 2, `<0.80` → 3, `<0.90` → 4,
  else 5). **Read only — this file belongs to Task 19-6 and must not be edited here.**
- `api/Bryk.Application/Zones/SportZonesResponse.cs` + `ZoneDto.cs` — the band shape the calculator takes
  (`Metric`, `Zones[].ZoneNumber/LowerBound/UpperBound?`). Note `UpperBound == null` means the open-ended
  top for power and the open-ended slow end for pace.
- `api/Bryk.Application/Analytics/AnalyticsService.cs:147–156` — the session-pace convention this task
  reuses verbatim: run = seconds per **1000 m**, swim = seconds per **100 m**, derived as
  `duration / (distance / unit)`.
- `api/Bryk.Application/Training/Load/LoadCalculator.cs:91–126` — read `ActualCardioTss` to see exactly
  which four values the synthetic step result will need (`AvgPower`, `AvgPace`, `AvgHr`,
  `ActualDurationSeconds`/`ActualDistanceMeters`). **Read only — frozen for Phase 19.**
- `api/Bryk.Application/Exceptions/ValidationException.cs` — `ValidationException(IEnumerable<string>)`
  with an `Errors` list; the type the middleware maps to 400.
- `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs:33–47` — confirm for yourself that
  `Bryk.Application.Exceptions.ValidationException` → 400 with an `errors[]` array, which is why a
  parse failure throws that type and nothing new is needed.
- `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs` — the pure-calculator
  convention: `public static class`, private `const` thresholds, one public entry point, XML `<summary>`
  naming the ADR section, no `DateTime.UtcNow`, no I/O.
- `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs` — the unit-test layout to mirror
  (private factory helpers at the top, one `[Fact]` per pinned case, FluentAssertions, exact values).
- `api/Bryk.API.Tests/Bryk.API.Tests.csproj` — the only test project with a `Bryk.Infrastructure` path
  (via `ProjectReference` to `Bryk.API`). `api/Bryk.Application.Tests/Bryk.Application.Tests.csproj`
  references `Bryk.Application` **only**.

## Acceptance criteria

### `api/Bryk.Application/ActivityFiles/IActivityFileParser.cs` (new)

New folder `Bryk.Application/ActivityFiles/`. Namespace `Bryk.Application.ActivityFiles`.

```csharp
public interface IActivityFileParser
{
    /// <summary>The file format this parser handles. The service selects a parser by matching it.</summary>
    ActivityFileFormat Format { get; }

    /// <summary>
    /// Parses one activity file into its session aggregates plus an in-memory sample series
    /// (ADR-0010 §6 — samples are never persisted). Throws
    /// <see cref="Exceptions.ValidationException"/> with a single "File: ..." message when the content
    /// is malformed or carries no track data; the caller does not catch it (the global middleware maps
    /// it to 400) and must not have staged anything before calling.
    /// </summary>
    Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default);
}
```
- `Stream`, not `byte[]`: `XDocument.LoadAsync` and the FIT decoder both take streams; the caller wraps
  the stored bytes in a `MemoryStream`. The parser must **not** dispose the stream it is given.
- Throwing `Bryk.Application.Exceptions.ValidationException` from `Bryk.Infrastructure` is deliberate and
  legal (Infrastructure references Application). It buys a clean 400 through the existing middleware with
  **no new exception type and no middleware change** (which would be a cross-cutting Sr. Dev gate).

### `api/Bryk.Application/ActivityFiles/ParsedActivity.cs` (new)

```csharp
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

public sealed record ActivitySample(int ElapsedSeconds, int? Hr, int? Power, int? PaceSecPerUnit);
```
Both positional `sealed record`s in one file, mirroring `WeeklyTargetInput`'s placement. XML `<summary>`
on each stating:
- `StartTimeUtc` is the file's first timestamp normalised to UTC; the workout's `CompletedDate` is its
  UTC calendar date. **No timezone handling in v1** — note it as a Phase 21 candidate, do not implement one.
- `AvgPace` is seconds per **km** (Run) or per **100 m** (Swim), null for Bike/Strength/Triathlon —
  the `AnalyticsService.cs:147–156` convention.
- `ElapsedSeconds` is seconds since `StartTimeUtc`, monotonically non-decreasing.
- Every numeric is nullable except `ElapsedSeconds`; a sample the file gives no value for carries nulls.
- **`ParsedActivity` deliberately carries no zone buckets.** Bucketing needs the athlete's zones, which
  are an Application/service concern — `ZoneHistogramCalculator` (below) owns it.

**Cross-format resolution rules — identical in all three parsers, stated once here and cited by 19-3:**

1. **Sport** — (a) the format's own sport metadata when present and recognised; (b) otherwise `Sport.Bike`
   when **any** sample carries a power value; (c) otherwise `Sport.Run`. Deterministic, no throw.
2. **Session averages/max** — always derived from the **retained samples**: `AvgHr` and `AvgPower` are the
   arithmetic mean of the non-null in-range sample values rounded to the nearest `int`; `MaxHr` is the
   max. The file's own summary elements are ignored, so one rule covers all three formats. Note in the
   XML doc that this can differ by ±1 from the device's reported average, which is immaterial for TSS.
3. **Duration / distance** — prefer the file's declared totals when present (TCX lap totals, FIT session
   totals); otherwise derive (`last.ElapsedSeconds`; summed great-circle distance).
4. **`AvgPace`** — `DurationSeconds / (DistanceMeters / unit)` rounded to the nearest `int`, only when
   sport is Run or Swim **and** both duration and distance are `> 0`; null otherwise.
5. **Empty file** — zero retained samples → throw
   `new ValidationException(new[] { "File: The file contains no track data." })`.
6. **Future start time** — `StartTimeUtc` later than "now" is left to the caller; parsers do not read the
   clock (19-4 owns that rejection).

### `api/Bryk.Application/ActivityFiles/ZoneHistogramEntry.cs` (new)

```csharp
public sealed record ZoneHistogramEntry(int ZoneNumber, int Seconds);
```
XML `<summary>`: one bucket of the derived per-zone seconds histogram (ADR-0010 §5). `ZoneNumber` is
1..5, matching `ZoneTimeDto`'s buckets so 19-6 can add the two together. **This is the persisted JSON's
element shape** — serialized by 19-4 and deserialized by 19-6, so changing it after Phase 19 ships is a
data-format change. Say so in the comment.

### `api/Bryk.Application/ActivityFiles/ZoneHistogramCalculator.cs` (new)

```csharp
public static class ZoneHistogramCalculator
{
    private const int ZoneCount = 5;
    private const int MaxSampleGapSeconds = 60;

    public static IReadOnlyList<ZoneHistogramEntry> Compute(
        ParsedActivity activity,
        SportZonesResponse? sportZones,
        int? maxHr);
}
```
XML `<summary>` naming ADR-0010 §5 and ADR-0007 §4, and stating "pure: no I/O, no `DateTime.UtcNow`".
Algorithm — implement exactly this:

1. Always return **five** entries, `ZoneNumber` 1..5, ordered ascending, even when every bucket is 0.
2. Walk `activity.Samples` in order. Sample *i*'s duration is
   `Math.Clamp(samples[i + 1].ElapsedSeconds - samples[i].ElapsedSeconds, 0, MaxSampleGapSeconds)`; the
   **last sample contributes 0 seconds** (there is no following sample to bound it). The clamp is what
   keeps a paused/gapped file from dumping an hour into one bucket — state that in a comment.
3. Resolve sample *i*'s bucket, first match wins:
   - `sportZones is { Metric: ZoneMetric.Power }` and `sample.Power is { } p` → the band satisfying
     `p >= z.LowerBound && (z.UpperBound is null || p < z.UpperBound)`, then `Math.Min(z.ZoneNumber, ZoneCount)`.
   - `sportZones is { Metric: ZoneMetric.Pace }` and `sample.PaceSecPerUnit is { } pace` → the **same
     predicate**, unchanged. Do **not** invert the comparison for pace: `TimeInZoneCalculator.cs:122`
     uses one predicate for both metrics and the zone rows are already stored in that orientation.
     Reproducing it verbatim is what keeps 19-6's two sources commensurable.
   - `sample.Hr is { } hr && hr > 0 && maxHr is { } max && max > 0` → the coarse %HRmax switch, a local
     copy of `TimeInZoneCalculator.HrZone` (L127–138): `< 0.60m` → 1, `< 0.70m` → 2, `< 0.80m` → 3,
     `< 0.90m` → 4, else 5.
   - otherwise → **no bucket**; those seconds are dropped. The histogram's sum may therefore be less
     than the session duration, and that is correct: 19-6 counts only what is measured.
4. `Math.Min(z.ZoneNumber, 5)` on every band lookup — bike Z6/Z7 collapse into 5 (ADR-0007 §4).
5. **Do not** refactor `TimeInZoneCalculator`'s `HrZone` or band predicate into a shared helper. That
   file belongs to Task 19-6; duplicate the ~10 lines locally and record the duplication in the phase
   handoff as tech debt.

### `api/Bryk.Infrastructure/ActivityFiles/ActivitySampleBounds.cs` (new)

New folder `Bryk.Infrastructure/ActivityFiles/`. Namespace `Bryk.Infrastructure.ActivityFiles`.

```csharp
internal static class ActivitySampleBounds
{
    public const int MinHr = 30;
    public const int MaxHr = 230;
    public const int MaxPowerWatts = 2000;

    public static int? Hr(int? value) => value is { } v && v >= MinHr && v <= MaxHr ? v : null;
    public static int? Power(int? value) => value is { } v && v >= 0 && v <= MaxPowerWatts ? v : null;
}
```
- **This task owns sample sanity** (the ROADMAP lists "sample sanity (HR 30–230 etc.)" under validation;
  it belongs at the parse boundary so a corrupt spike never reaches the service). Task **19-4 does not
  own it** and its doc says so; Task 19-3's FIT parser **reuses this type read-only** rather than
  redeclaring the constants.
- Out-of-range values become `null` on that sample — the **sample is retained** (its elapsed time still
  counts toward duration) but contributes nothing to the average, the max, or the histogram bucket.

### `api/Bryk.Infrastructure/ActivityFiles/TcxActivityParser.cs` (new)

`public class TcxActivityParser : IActivityFileParser`, `Format => ActivityFileFormat.Tcx`.

- `System.Xml.Linq` only. Namespaces as private `static readonly XNamespace`:
  `Tcx = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2"`,
  `Tpx = "http://www.garmin.com/xmlschemas/ActivityExtension/v2"`.
- Root element must be `{Tcx}TrainingCenterDatabase`; anything else →
  `"File: The file is not a valid .tcx activity."`.
- Sport from `Activity/@Sport`: `"Running"` → `Sport.Run`, `"Biking"` → `Sport.Bike`,
  `"Swimming"` → `Sport.Swim`, anything else (including `"Other"`/absent) → the §Sport fallback chain.
- Samples from every `Lap/Track/Trackpoint`: `Time` (parse as UTC — `DateTimeStyles.AdjustToUniversal |
  AssumeUniversal`), `HeartRateBpm/Value`, `Extensions/{Tpx}TPX/{Tpx}Watts`, `DistanceMeters`
  (cumulative). A trackpoint with no `Time` is skipped entirely.
- Declared totals: `DurationSeconds` = rounded Σ `Lap/TotalTimeSeconds` when present, else
  `last.ElapsedSeconds`; `DistanceMeters` = rounded Σ `Lap/DistanceMeters` when present, else the last
  trackpoint's cumulative `DistanceMeters`.
- Per-sample pace (Run/Swim only) from the cumulative-distance delta between consecutive trackpoints:
  `paceSecPerUnit = Δseconds / (Δmeters / unit)` when `Δmeters > 0`, else null.
- Any `XmlException` (or `FormatException` from a timestamp/number) →
  `"File: The .tcx file could not be parsed."`. Do **not** let the raw exception escape; do **not**
  swallow it silently.

### `api/Bryk.Infrastructure/ActivityFiles/GpxActivityParser.cs` (new)

`public class GpxActivityParser : IActivityFileParser`, `Format => ActivityFileFormat.Gpx`.

- Namespaces: `Gpx = "http://www.topografix.com/GPX/1/1"`,
  `Tpx1 = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1"`.
- Root must be `{Gpx}gpx`; else `"File: The file is not a valid .gpx activity."`.
- Sport from the first `trk/type` element, case-insensitive `Contains`: `"run"` → Run,
  `"bik"`/`"cycl"`/`"ride"` → Bike, `"swim"` → Swim; else the fallback chain.
- Samples from every `trk/trkseg/trkpt`: `@lat`, `@lon`, `time`, and
  `extensions/{Tpx1}TrackPointExtension/{Tpx1}hr`. Points without `time` are skipped.
- **GPX 1.1 carries no power** — `AvgPower` is always null and no sample carries `Power`. State it in a
  comment; do not chase vendor power extensions in v1.
- `DistanceMeters` = Σ great-circle distance between consecutive points, haversine with
  `EarthRadiusMeters = 6371000d`, rounded to the nearest `int` at the end (not per segment).
- Per-sample pace (Run/Swim only) from the same segment distance, as in the TCX parser.
- Same `XmlException`/`FormatException` → `"File: The .gpx file could not be parsed."` handling.

### Fixtures — `api/Bryk.API.Tests/Fixtures/ActivityFiles/` (new folder)

Three hand-authored files with **exactly** these contents (the tests pin the derived numbers, so the
fixture values are part of the contract):

- `sample-run.tcx` — `Activity Sport="Running"`, one lap with `TotalTimeSeconds 600` and
  `DistanceMeters 2000`, five trackpoints at `t + 0/150/300/450/600 s`, HR `120/140/150/160/150`,
  cumulative distance `0/500/1000/1500/2000`, no `Watts`. Start `2026-06-01T06:00:00Z`.
- `sample-ride.tcx` — `Activity Sport="Biking"`, one lap with `TotalTimeSeconds 3600` and
  `DistanceMeters 30000`, four trackpoints at `t + 0/1200/2400/3600 s`, HR `130/145/150/140`,
  `Watts 200/220/240/180`. Start `2026-06-02T06:00:00Z`.
- `sample-activity.gpx` — GPX 1.1, `<trk><type>running</type>`, three `trkpt` at
  `(40.000000, -105.000000)`, `(40.008993, -105.000000)`, `(40.017986, -105.000000)` with times
  `t + 0/300/600 s` and `gpxtpx:hr` `130/140/150`. Start `2026-06-03T06:00:00Z`.

`api/Bryk.API.Tests/Bryk.API.Tests.csproj` gains **one** item group, written as a glob so Task 19-3 can
drop its `.fit` fixture into the same folder **without touching this file**:
```xml
<ItemGroup>
  <None Update="Fixtures\ActivityFiles\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```
Tests resolve a fixture as `Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name)`.
Add a tiny `private static Stream Fixture(string name)` helper in each test class rather than a shared
base class.

## Non-goals
- **No new NuGet or npm package.** `.tcx`/`.gpx` are `System.Xml.Linq` (in the framework).
  `Garmin.FIT.Sdk` is Task 19-3's and belongs in `Bryk.Infrastructure.csproj` — **do not pre-add it**,
  and **do not edit `Bryk.Infrastructure.csproj` at all** in this task.
- **No migration**, no entity change, no `ApplicationDbContext` edit. If this task appears to need one —
  **STOP and ask** (Sr. Dev gate).
- **Do not edit `api/Bryk.Application/Training/Load/LoadCalculator.cs`** — frozen for Phase 19. The
  parser's job ends at `ParsedActivity`; routing power/pace into the load math is 19-4's synthetic
  `WorkoutStepResult` (ADR-0010 §3). If you find yourself teaching the calculator about session power —
  **STOP and ask**.
- **Do not edit `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs`, `TimeInZoneResponse.cs` or
  `AnalyticsService.cs`** — Task 19-6 owns all three. Duplicate the ~10 lines of HR/band logic locally.
- **Do not add `Workout.SourceFileId`** or a `WorkoutZoneDuration` table; neither is approved
  (ADR-0010 §4). **STOP and ask** if a design seems to need them.
- **Do not persist samples anywhere** (ADR-0010 §6) — `ParsedActivity.Samples` is in-memory only.
- **Do not** create a service, DTO, validator, controller, `Program.cs` registration or DI line. The
  parsers being unresolvable from the container until 19-4 is expected.
- Do not write files owned by siblings: `ActivityFile.cs` / `IActivityFileRepository.cs` /
  `ActivityFileRepository.cs` / `ApplicationDbContext.cs` / `Program.cs` (19-1),
  `FitActivityParser.cs` / `Bryk.Infrastructure.csproj` (19-3), `Bryk.Application/ActivityFiles/`
  service, DTO and validator files + `ActivityFilesController.cs` (19-4), anything under `ui/` (19-5).
- **No auth code** — Phase 12 stays deferred and approval-gated.
- **No ProblemDetails / error-contract rework**, and **no new case in `ExceptionHandlingMiddleware`** —
  parse failures reuse `ValidationException` → 400. Adding a middleware case is a cross-cutting change:
  **STOP and ask**.
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.
- No power curves, decoupling, lap deep-dives, per-lap step results, bulk upload, or vendor OAuth.

## Test expectations

**Pure — `api/Bryk.Application.Tests/ActivityFiles/ZoneHistogramCalculatorTests.cs` (new folder).**
No stubs, no host; build `ParsedActivity` inline with a private `Activity(params ActivitySample[])`
helper and `SportZonesResponse` inline.

- `Compute_AlwaysReturnsFiveBucketsOrderedOneToFive` — even with no samples: five entries,
  `ZoneNumber` `1,2,3,4,5`, all `Seconds == 0`.
- `Compute_PowerSamples_BucketByBand` — power bands `Z1 [0,150) Z2 [150,200) Z3 [200,250) Z4 [250,300)
  Z5 [300,null)`, samples at `t = 0/60/120/180` with power `100/175/225/275` → zones 1–4 each get
  **60** seconds and zone 5 gets **0** (the last sample contributes nothing).
- `Compute_BikeZoneSixAndSeven_CollapseIntoBucketFive` — bands including `Z6`/`Z7`; a sample landing in
  Z7 adds to bucket **5**.
- `Compute_PaceMetricUsesTheSamePredicateAsTimeInZone` — pace bands, one sample per band, asserting the
  bucket for a value exactly on a `LowerBound` (inclusive) and one just below an `UpperBound` (exclusive).
- `Compute_NoZones_FallsBackToPercentOfMaxHr` — `sportZones = null`, `maxHr = 200`, samples with HR
  `119` (59.5 % → Z1), `120` (60 % → Z2), `140` (70 % → Z3), `160` (80 % → Z4), `180` (90 % → Z5) and a
  trailing sample; each of the first five contributes its gap to the pinned bucket. Boundaries at
  exactly 60/70/80/90 % must land in the **higher** bucket.
- `Compute_GapLongerThanSixtySeconds_IsClampedToSixty` — samples at `t = 0` and `t = 600` → the first
  contributes exactly **60**, not 600.
- `Compute_LastSampleContributesZeroSeconds` — two samples, total bucketed seconds equals the single gap.
- `Compute_SamplesWithNoUsableSignal_AreDroppedFromEveryBucket` — samples with all-null Hr/Power/Pace and
  `maxHr = null` → every bucket 0, and the assertion notes the histogram sum may be < the duration.

**Parsers — `api/Bryk.API.Tests/ActivityFiles/TcxActivityParserTests.cs` (new folder).**
(These live in `Bryk.API.Tests` because it is the only test project that can see `Bryk.Infrastructure`;
`Bryk.Application.Tests` references `Bryk.Application` alone. **Do not** add a project reference —
that is a Sr. Dev slow-down gate.)

- `Format_IsTcx`.
- `ParseAsync_RunFixture_PinsSessionAggregates` — `sample-run.tcx` → `Sport == Sport.Run`,
  `DurationSeconds == 600`, `DistanceMeters == 2000`, `AvgHr == 144`, `MaxHr == 160`,
  `AvgPower == null`, `AvgPace == 300`, `Samples.Should().HaveCount(5)`,
  `StartTimeUtc == new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc)`.
- `ParseAsync_RideFixture_DetectsBikeAndAveragesPower` — `sample-ride.tcx` → `Sport == Sport.Bike`,
  `DurationSeconds == 3600`, `DistanceMeters == 30000`, `AvgHr == 141`, `AvgPower == 210`,
  `AvgPace == null`, `Samples.Should().HaveCount(4)`.
- `ParseAsync_OutOfRangeHeartRate_IsDiscardedWithoutDroppingTheSample` — an inline TCX string (not a
  fixture) whose trackpoints carry HR `10 / 150 / 300` → `AvgHr == 150`, `MaxHr == 150`, and
  `Samples.Should().HaveCount(3)` with the first and last carrying `Hr == null`.
- `ParseAsync_PowerAboveTwoThousandWatts_IsDiscarded` — inline TCX with `Watts 5000` alongside a valid
  `200` → `AvgPower == 200`.
- `ParseAsync_MalformedXml_ThrowsValidationExceptionWithFilePrefix` — `"<not xml"` →
  `Bryk.Application.Exceptions.ValidationException` whose single `Errors` entry starts with `"File:"`.
- `ParseAsync_WrongRootElement_ThrowsValidationException` — a well-formed GPX passed to the TCX parser.
- `ParseAsync_NoTrackpoints_ThrowsValidationException` — a valid TCX skeleton with an empty `Track` →
  `Errors` single entry contains `"no track data"`.
- `ParseAsync_UnknownSportAttributeWithPowerSamples_FallsBackToBike` and
  `ParseAsync_UnknownSportAttributeWithoutPower_FallsBackToRun` — the §Sport fallback chain, both branches.

**Parsers — `api/Bryk.API.Tests/ActivityFiles/GpxActivityParserTests.cs`.**

- `Format_IsGpx`.
- `ParseAsync_Fixture_DerivesDistanceFromHaversine` — `DistanceMeters.Should().BeInRange(1995, 2005)`.
  A range, not an exact value: the expected distance is great-circle arithmetic (two 0.008993° meridian
  steps ≈ 1000 m each at `R = 6 371 000 m`). Say so in a comment — it is the only tolerance in the suite.
- `ParseAsync_Fixture_ReadsTheHeartRateExtension` — `AvgHr == 140`, `MaxHr == 150`.
- `ParseAsync_Fixture_ResolvesRunFromTrackType` — `Sport == Sport.Run`, `DurationSeconds == 600`,
  `AvgPace.Should().BeInRange(298, 302)`, `AvgPower.Should().BeNull()`.
- `ParseAsync_MissingTrackType_FallsBackToRun` — inline GPX without `<type>` and with no power samples.
- `ParseAsync_MalformedXml_ThrowsValidationExceptionWithFilePrefix`.
- `ParseAsync_NoTrackPoints_ThrowsValidationException`.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must rise from the **262** baseline (173 `Bryk.Application.Tests` + 89 `Bryk.API.Tests`) plus
whatever 19-1 added, with zero failures. Vitest stays at exactly **252 / 56 files** — this task touches
no UI. The build's **16** warnings must not grow (the fixture glob must not introduce a new one).

## Review checklist
- [ ] `IActivityFileParser` lives in `Bryk.Application`; **no** `Bryk.Infrastructure` type appears in
      its signature, and `Bryk.Application` gained no package reference.
- [ ] `ParsedActivity` carries **samples**, not zone buckets; `ZoneHistogramCalculator` is `static`,
      pure, and has zero `DateTime.UtcNow` / repository / `async` usage.
- [ ] The histogram always returns exactly five entries, ordered 1..5, and the last sample contributes 0.
- [ ] The pace band predicate is character-identical to `TimeInZoneCalculator.cs:122` (not inverted).
- [ ] Both parsers throw `Bryk.Application.Exceptions.ValidationException` with a single `"File: …"`
      message on malformed input and on an empty track; no raw `XmlException` escapes.
- [ ] Out-of-range HR/power null the value but keep the sample (its elapsed time still counts).
- [ ] The three fixtures are committed, the csproj glob covers `Fixtures\ActivityFiles\**`, and the
      pinned aggregate values in the tests match the fixture contents exactly.
- [ ] `git diff --stat` shows no `Bryk.Infrastructure.csproj`, no `Program.cs`, no `TimeInZone*`, no
      `LoadCalculator.cs`, no migration, and nothing under `ui/`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
