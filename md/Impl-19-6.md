# Impl 19-6 — Build order: sample-derived time-in-zone (`SampleSeconds`, `samples` provenance)

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-19-6.md`.
**Decision lock:** ADR-0010 §5 (sample-derived seconds report method `samples`, take precedence over
structure/sessionAvg for covered workouts, `SampleSeconds` additive, histogram stays JSON-not-table) +
ADR-0007 §4 (the five buckets, the `Math.Min(z, 5)` collapse, and the coarse %HRmax scheme this task
leaves byte-identical for uncovered workouts).
**Scope:** Backend then frontend — the API must be green end-to-end before the UI consumes it. One
additive DTO field, a fourth calculator branch, one widened service ctor, one honest-badge rewrite. No
migration, no new package, no new endpoint, no `Program.cs` change.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. Commit once at the end with the message in `Tasks-19-6.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`. `dotnet build api/Bryk.sln` green, `cd ui; pnpm run build` green.
- Record this task's starting numbers as the working baseline: `dotnet test api/Bryk.sln` and
  `cd ui; pnpm exec vitest run --no-file-parallelism`. Tasks 19-1 … 19-5 will already have raised both
  suites above the phase-start figures quoted in `Tasks-19-6.md` (**262** xUnit, **252 / 56 files**
  Vitest) — note the *actual* current numbers here; Step 12 must show both counts strictly higher, zero
  failures, and warnings still ≤ **16**.
- **Confirm the hard dependency (Task 19-4) and its transitive dependencies (19-1, 19-2) actually
  landed** — this task will not compile against a stub or a differently-shaped contract. If any of the
  following is missing or shaped differently than described, **STOP**: that task is not done and must
  land first.
  - `api/Bryk.Domain/Entities/ActivityFile.cs` — `public Guid? ParsedWorkoutId { get; set; }` and
    `public string? ZoneHistogramJson { get; set; }` (19-1).
  - `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs` —
    `Task<IReadOnlyList<ActivityFile>> GetByParsedWorkoutIdsAsync(Guid athleteId, IEnumerable<Guid> workoutIds, CancellationToken ct = default)`,
    documented as never loading `Content` and returning an empty list with no query for an empty id set
    (19-1). Already registered in `Program.cs` — confirm no DI edit is needed by this task.
  - `api/Bryk.Application/ActivityFiles/ZoneHistogramEntry.cs` —
    `public sealed record ZoneHistogramEntry(int ZoneNumber, int Seconds);` (19-2).
  - `api/Bryk.Application/ActivityFiles/ActivityFileService.cs` `CommitAsync` — writes
    `file.ZoneHistogramJson` at commit via `JsonSerializer.Serialize(histogram, JsonOptions)` where
    `JsonOptions = new(JsonSerializerDefaults.Web)` (camelCase output — `[{"zoneNumber":1,"seconds":600}, …]`)
    (19-4). Deserializing with the same defaults in Step 3 below is what makes the round-trip honest.
  - `api/Bryk.API/Controllers/ActivityFilesController.cs` — `POST /api/v1/activityfiles` and
    `POST /api/v1/activityfiles/{id}/commit` resolve and return 201 (19-4) — Step 6's integration test
    calls both.
  - `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.tcx` — committed, `Activity Sport="Biking"`,
    avg power 210 W over 3600 s, start `2026-06-02T06:00:00Z` (19-2). Confirm the file exists and the csproj
    glob (`Fixtures\ActivityFiles\**`) still copies it to output.
- Re-read `md/Tasks-19-6.md` in full.
- Open in editor (backend): `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs`,
  `TimeInZoneResponse.cs`, `AnalyticsService.cs:120–143`,
  `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs`,
  `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs`,
  `api/Bryk.API.Tests/Profile/ProfileControllerTests.cs` (the onboarding-seeding pattern Step 6 reuses).
- Open (frontend): `ui/src/types/analytics.ts`,
  `ui/src/components/analytics/TimeInZoneSection.vue`,
  `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts`, `ui/src/lib/format.ts` (`formatHm`,
  read-only).
- Confirm current shapes (already verified during spec-writing): `TimeInZoneCalculator.Compute` has
  exactly **one** production call site (`AnalyticsService.cs:142`) and **6** call sites in
  `TimeInZoneCalculatorTests.cs`. Adding a required 5th parameter means editing those 7 call sites and
  nothing else — **every existing assertion must remain unchanged**, that is this task's regression guard.
  `AnalyticsService`'s primary ctor is
  `(ICurrentUserService currentUser, IValidator<AnalyticsRangeRequest> validator,
  IValidator<WeeklyLoadRequest> weeklyValidator, IWorkoutRepository workoutRepo,
  ITrainingPlanRepository planRepo, IAthleteRepository athleteRepo, IZoneService zoneService)`; it gains
  exactly one parameter (`IActivityFileRepository fileRepo`), inserted **after `athleteRepo`, before
  `zoneService`** — not appended last.

## Step 1 — `TimeInZoneResponse.cs`: additive `SampleSeconds`

**File:** `api/Bryk.Application/Analytics/TimeInZoneResponse.cs` — replace the file with:

```csharp
namespace Bryk.Application.Analytics;

// Seconds spent in one coarse intensity bucket (ADR-0007 §4). ZoneNumber is 1..5 (the lowest common
// denominator across the sports' zone schemes; bike Z6/Z7 collapse to 5).
public class ZoneTimeDto
{
    public int ZoneNumber { get; set; }
    public int Seconds { get; set; }
}

// How the histogram's seconds were derived. Four provenances, summing to TotalSeconds: sample-derived
// seconds come from an imported file's stored per-zone histogram (ADR-0010 §5) and are measured, not
// estimated — they take precedence over the other three for a covered workout. The remaining three stay
// the ADR-0007 §4 estimate chain: planned structure for linked workouts, coarse session AvgHr otherwise,
// else unclassified.
public class ZoneTimeMethodBreakdownDto
{
    public int SampleSeconds { get; set; }
    public int StructureSeconds { get; set; }
    public int SessionAvgSeconds { get; set; }
    public int UnclassifiedSeconds { get; set; }
}

// The time-in-zone read shape: a 5-bucket intensity histogram in seconds + the method breakdown + total.
public class TimeInZoneResponse
{
    public IReadOnlyList<ZoneTimeDto> Zones { get; set; } = new List<ZoneTimeDto>();
    public ZoneTimeMethodBreakdownDto MethodBreakdown { get; set; } = new();
    public int TotalSeconds { get; set; }
}
```

`SampleSeconds` is first, matching the precedence order. `ZoneTimeDto` and `TimeInZoneResponse` are
byte-identical to before — this is purely additive, so `GET /analytics/time-in-zone` stays
backward-compatible.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 2 — `TimeInZoneCalculator.cs`: the samples-first branch

**File:** `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs` — replace the file with:

```csharp
using Bryk.Application.ActivityFiles;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;

namespace Bryk.Application.Analytics;

/// <summary>
/// Time-in-zone across four provenances (ADR-0007 §4, ADR-0010 §5): samples — an imported file's
/// measured per-zone histogram, which wins outright over the estimate chain for a covered workout —
/// then the ADR-0007 §4 estimate chain unchanged: structure (planned steps), sessionAvg (coarse session
/// AvgHr), and unclassified. No I/O — the caller passes the completed workouts, the linked planned
/// structures, the athlete's zones, MaxHr, and any covering sample histograms. Builds a 5-bucket
/// intensity histogram (seconds) whose four-way method breakdown sums to the total.
/// </summary>
public static class TimeInZoneCalculator
{
    private const int ZoneCount = 5;

    public static TimeInZoneResponse Compute(
        IReadOnlyList<Workout> workouts,
        IReadOnlyDictionary<Guid, PlannedWorkout> structures,
        ZonesResponse zones,
        int? maxHr,
        IReadOnlyDictionary<Guid, IReadOnlyList<ZoneHistogramEntry>> sampleHistograms)
    {
        var zoneSeconds = new int[ZoneCount + 1]; // index 1..5
        var sampleSeconds = 0;
        var structureSeconds = 0;
        var sessionAvgSeconds = 0;
        var unclassifiedSeconds = 0;

        foreach (var workout in workouts)
        {
            // 0. samples — an imported file's measured per-zone histogram (ADR-0010 §5). It wins outright
            // over structure and sessionAvg: those are estimates of the same time. A histogram that sums
            // to zero is treated as absent (the file carried no usable signal) and the workout falls
            // through to the estimate chain below.
            if (sampleHistograms.TryGetValue(workout.Id, out var histogram) && histogram.Count > 0)
            {
                var measured = 0;
                foreach (var bucket in histogram)
                {
                    if (bucket.ZoneNumber >= 1 && bucket.ZoneNumber <= ZoneCount && bucket.Seconds > 0)
                    {
                        zoneSeconds[bucket.ZoneNumber] += bucket.Seconds;
                        measured += bucket.Seconds;
                    }
                }

                if (measured > 0)
                {
                    sampleSeconds += measured;
                    // Samples with no usable signal are not measured time; attribute the remainder of the
                    // session honestly rather than shrinking the athlete's total training time.
                    unclassifiedSeconds += Math.Max(0, (workout.ActualDurationSeconds ?? 0) - measured);
                    continue;
                }
            }

            var planned = workout.PlannedWorkoutId is { } pid
                          && structures.TryGetValue(pid, out var p)
                          && p.Blocks.Count > 0
                ? p
                : null;

            if (planned is not null)
            {
                // 1. structure — attribute each planned step's duration to its zone.
                var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == workout.Sport);
                foreach (var block in planned.Blocks)
                {
                    var repeats = Math.Max(block.Repeats, 1);
                    foreach (var step in block.Steps)
                    {
                        var seconds = (step.DurationSeconds ?? 0) * repeats;
                        if (seconds <= 0)
                        {
                            continue; // distance-only / zero-duration steps contribute no known time
                        }

                        if (ClassifyStep(step, sportZones) is { } zone)
                        {
                            zoneSeconds[zone] += seconds;
                            structureSeconds += seconds;
                        }
                        else
                        {
                            unclassifiedSeconds += seconds;
                        }
                    }
                }
            }
            else if (workout.AvgHr is { } hr && hr > 0 && maxHr is { } max && max > 0)
            {
                // 2. sessionAvg — the whole session goes to one bucket via coarse %HRmax.
                var seconds = workout.ActualDurationSeconds ?? 0;
                if (seconds > 0)
                {
                    zoneSeconds[HrZone(hr, max)] += seconds;
                    sessionAvgSeconds += seconds;
                }
            }
            else
            {
                // 3. unclassified — no structure, no usable AvgHr (incl. strength).
                unclassifiedSeconds += workout.ActualDurationSeconds ?? 0;
            }
        }

        var zoneList = Enumerable.Range(1, ZoneCount)
            .Select(z => new ZoneTimeDto { ZoneNumber = z, Seconds = zoneSeconds[z] })
            .ToList();

        return new TimeInZoneResponse
        {
            Zones = zoneList,
            MethodBreakdown = new ZoneTimeMethodBreakdownDto
            {
                SampleSeconds = sampleSeconds,
                StructureSeconds = structureSeconds,
                SessionAvgSeconds = sessionAvgSeconds,
                UnclassifiedSeconds = unclassifiedSeconds
            },
            TotalSeconds = sampleSeconds + structureSeconds + sessionAvgSeconds + unclassifiedSeconds
        };
    }

    // A planned step → intensity bucket 1..5: TargetZone (collapsed via min(z,5)), else the raw target's
    // midpoint resolved against the sport's zone bands, else null (unclassified — incl. HR-only steps).
    private static int? ClassifyStep(WorkoutStep step, SportZonesResponse? sportZones)
    {
        if (step.TargetZone is { } tz && tz > 0)
        {
            return Math.Min(tz, ZoneCount);
        }

        if (sportZones is null)
        {
            return null;
        }

        var value = sportZones.Metric switch
        {
            ZoneMetric.Power => Midpoint(step.TargetPowerLow, step.TargetPowerHigh),
            ZoneMetric.Pace => Midpoint(step.TargetPaceLow, step.TargetPaceHigh),
            _ => null
        };

        if (value is not { } v)
        {
            return null;
        }

        var band = sportZones.Zones.FirstOrDefault(z => v >= z.LowerBound && (z.UpperBound is null || v < z.UpperBound));
        return band is null ? null : Math.Min(band.ZoneNumber, ZoneCount);
    }

    // Coarse %HRmax 5-zone scheme (ADR-0007 §4): <60% Z1, <70% Z2, <80% Z3, <90% Z4, ≥90% Z5.
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

    private static decimal? Midpoint(int? low, int? high)
    {
        if (low is { } l && high is { } h) return (l + h) / 2m;
        if (low is { } lo) return lo;
        if (high is { } hi) return hi;
        return null;
    }
}
```

`ClassifyStep`, `HrZone`, `Midpoint`, the structure branch, the sessionAvg branch, and the `zoneList`
projection are byte-identical to before — the whole point is that an uncovered workout's numbers are
unchanged. **Do not touch them.**

**Verify:** `dotnet build api/Bryk.sln`. This is **expected to fail**: `AnalyticsService.cs` still calls
`Compute` with the old 4-argument signature (its production call site), and
`TimeInZoneCalculatorTests.cs` still has 6 call sites at the old arity. Do not try to fix the test project
in isolation — proceed straight to Step 3, which repairs the one production call site and turns the
solution-wide build green again before the tests are touched.

## Step 3 — `AnalyticsService.cs`: widen the ctor, load the histograms, pass them through

**File:** `api/Bryk.Application/Analytics/AnalyticsService.cs` — three edits, in this order.

**3a. Using block** — add two usings (alphabetical among the existing block):

```csharp
using System.Text.Json;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;
```

**3b. Primary ctor + a new private field** — widen the ctor (one new parameter, inserted after
`athleteRepo` and before `zoneService` — not appended last) and add the deserialization options field
alongside the two existing `const`s:

```csharp
public class AnalyticsService(
    ICurrentUserService currentUser,
    IValidator<AnalyticsRangeRequest> validator,
    IValidator<WeeklyLoadRequest> weeklyValidator,
    IWorkoutRepository workoutRepo,
    ITrainingPlanRepository planRepo,
    IAthleteRepository athleteRepo,
    IActivityFileRepository fileRepo,
    IZoneService zoneService) : IAnalyticsService
{
    // Bounded warm-up before `from` so the EWMA is primed; 180 days ≫ the 42-day CTL constant (ADR-0006 §2).
    private const int LookbackDays = 180;

    private const int DefaultWeeks = 8;

    // The same defaults Task 19-4's commit path serializes ActivityFile.ZoneHistogramJson with, so the
    // camelCase stored JSON round-trips without a custom naming policy (ADR-0010 §5).
    private static readonly JsonSerializerOptions HistogramJsonOptions = new(JsonSerializerDefaults.Web);
```

No other constructor-consuming line changes: `IActivityFileRepository` is already registered in
`Program.cs` by Task 19-1, so DI resolves the widened ctor automatically. **No `Program.cs` edit in this
task.**

**3c. `GetTimeInZoneAsync`** — insert the histogram-loading block between the structures lookup and the
zones/athlete reads, and pass `sampleHistograms` as `Compute`'s fifth argument:

```csharp
public async Task<TimeInZoneResponse> GetTimeInZoneAsync(DateOnly? from, DateOnly? to, Sport? sport, CancellationToken ct = default)
{
    // Reuse the PMC range contract (both required, from ≤ to, ≤ 400 days, no future to).
    await validator.ValidateOrThrowAsync(new AnalyticsRangeRequest { From = from, To = to }, ct);
    var fromDate = from!.Value;
    var toDate = to!.Value;
    var athleteId = currentUser.GetCurrentAthleteId();

    var completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, fromDate, toDate, ct);
    var workouts = sport is { } s ? completed.Where(w => w.Sport == s).ToList() : completed;

    var linkedIds = workouts
        .Where(w => w.PlannedWorkoutId is not null)
        .Select(w => w.PlannedWorkoutId!.Value)
        .Distinct()
        .ToList();
    var structures = (await planRepo.GetPlannedWorkoutsByIdsWithStructureAsync(linkedIds, ct))
        .ToDictionary(p => p.Id);

    // Sample-derived histograms for any workout in this range that came from an imported file
    // (ADR-0010 §5). The reverse lookup never loads the file bytes. The ids passed are the
    // already sport-filtered `workouts` list, so a ?sport= query narrows the file lookup too.
    var files = await fileRepo.GetByParsedWorkoutIdsAsync(athleteId, workouts.Select(w => w.Id), ct);
    var sampleHistograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>();
    foreach (var file in files)
    {
        if (file.ParsedWorkoutId is not { } workoutId || string.IsNullOrWhiteSpace(file.ZoneHistogramJson))
        {
            continue;
        }

        List<ZoneHistogramEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<ZoneHistogramEntry>>(file.ZoneHistogramJson, HistogramJsonOptions);
        }
        catch (JsonException)
        {
            continue; // a malformed stored histogram degrades to the estimate chain, never to a 500
        }

        if (entries is { Count: > 0 })
        {
            sampleHistograms[workoutId] = entries;
        }
    }

    var zones = await zoneService.GetZonesAsync(ct);
    var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);

    return TimeInZoneCalculator.Compute(workouts, structures, zones, athlete?.MaxHr, sampleHistograms);
}
```

**Do not touch** `GetDailyLoadAsync`, `GetPmcAsync`, `GetWeeklyLoadAsync`, `GetPeaksAsync`, `ToSummary`,
`WeekStart`, `BuildSeriesAsync`, `ComputeFrom`, or either `Slice` overload — `GetTimeInZoneAsync` is the
only method this task changes.

**Verify:** `dotnet build api/Bryk.sln`. The four main projects (`Bryk.Domain`, `Bryk.Application`,
`Bryk.Infrastructure`, `Bryk.API`) now build green — the one production call site is fixed.
`Bryk.Application.Tests` still fails to build (its 6 call sites in `TimeInZoneCalculatorTests.cs` are
still at the old arity) — that is expected; proceed to Step 4.

## Step 4 — Widen `TimeInZoneCalculatorTests.cs` (its own step; one regression fact)

**File:** `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs`.

Add the using:

```csharp
using Bryk.Application.ActivityFiles;
```

Add a shared empty-histograms constant alongside `NoZones`:

```csharp
private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<ZoneHistogramEntry>> NoSamples =
    new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>();
```

Pass `NoSamples` as the fifth argument at every one of the six existing `Compute(...)` call sites — **do
not change any existing assertion**:

1. `StructuredWorkout_AttributesStepDurationsToTargetZones`
2. `StructuredBike_Z6Z7CollapseToBucket5_AndRepeatsMultiply`
3. `RawTarget_ResolvesAgainstZoneBands_WhenNoTargetZone`
4. `UnlinkedWorkout_ClassifiesWholeSessionByHrMax`
5. `NoStructureNoHr_AndStrength_AreUnclassified`
6. `MethodSeconds_SumToTotal_AndZonesEqualTotalMinusUnclassified`

e.g. `TimeInZoneCalculator.Compute(workouts, structures, NoZones, maxHr: 190, NoSamples)`.

Then add one explicit new fact proving the regression guard — a structured workout and a sessionAvg
workout with **no** sample histograms produce the same numbers as before, with `SampleSeconds == 0`:

```csharp
[Fact]
public void Compute_WithoutSampleHistograms_IsUnchanged()
{
    var pid = Guid.NewGuid();
    var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = Planned(pid, 1, Step(600, 2)) };
    var workouts = new[]
    {
        Workout(Sport.Bike, plannedId: pid),             // structure 600
        Workout(Sport.Run, avgHr: 150, duration: 1800),  // sessionAvg 1800
    };

    var result = TimeInZoneCalculator.Compute(workouts, structures, NoZones, maxHr: 190, NoSamples);

    result.Zones.Single(z => z.ZoneNumber == 2).Seconds.Should().Be(600);
    result.MethodBreakdown.SampleSeconds.Should().Be(0);
    result.MethodBreakdown.StructureSeconds.Should().Be(600);
    result.MethodBreakdown.SessionAvgSeconds.Should().Be(1800);
    result.TotalSeconds.Should().Be(2400);
}
```

**Verify:** `dotnet build api/Bryk.sln` — green, whole solution. `dotnet test api/Bryk.sln` — the 6
pre-existing facts pass **with their original assertions unchanged**, and the new
`Compute_WithoutSampleHistograms_IsUnchanged` fact passes.

## Step 5 — `TimeInZoneCalculatorTests.cs`: the 8 samples-branch facts

**File:** `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs` — append below the fact
added in Step 4:

```csharp
[Fact]
public void Compute_SampleHistogram_PopulatesZonesAndSampleSeconds()
{
    var workout = Workout(Sport.Bike, duration: 3600);
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [workout.Id] = new List<ZoneHistogramEntry> { new(1, 600), new(2, 1200), new(3, 1800), new(4, 0), new(5, 0) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { workout }, new Dictionary<Guid, PlannedWorkout>(), NoZones, maxHr: 190, histograms);

    result.Zones.Single(z => z.ZoneNumber == 1).Seconds.Should().Be(600);
    result.Zones.Single(z => z.ZoneNumber == 2).Seconds.Should().Be(1200);
    result.Zones.Single(z => z.ZoneNumber == 3).Seconds.Should().Be(1800);
    result.MethodBreakdown.SampleSeconds.Should().Be(3600);
    result.MethodBreakdown.UnclassifiedSeconds.Should().Be(0);
    result.TotalSeconds.Should().Be(3600);
}

[Fact]
public void Compute_SampleHistogram_TakesPrecedenceOverPlannedStructure()
{
    var pid = Guid.NewGuid();
    var workout = Workout(Sport.Bike, plannedId: pid, duration: 3600);
    var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = Planned(pid, 1, Step(600, 2), Step(480, 4)) }; // 1080s
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [workout.Id] = new List<ZoneHistogramEntry> { new(1, 3600) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { workout }, structures, NoZones, maxHr: 190, histograms);

    result.MethodBreakdown.StructureSeconds.Should().Be(0);
    result.MethodBreakdown.SampleSeconds.Should().Be(3600);
}

[Fact]
public void Compute_SampleHistogram_TakesPrecedenceOverSessionAvg()
{
    var workout = Workout(Sport.Run, avgHr: 150, duration: 3600);
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [workout.Id] = new List<ZoneHistogramEntry> { new(3, 3600) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { workout }, new Dictionary<Guid, PlannedWorkout>(), NoZones, maxHr: 190, histograms);

    result.MethodBreakdown.SessionAvgSeconds.Should().Be(0);
    result.MethodBreakdown.SampleSeconds.Should().Be(3600);
}

[Fact]
public void Compute_SampleHistogramShorterThanTheSession_AttributesTheRemainderToUnclassified()
{
    var workout = Workout(Sport.Bike, duration: 3600);
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [workout.Id] = new List<ZoneHistogramEntry> { new(2, 3000) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { workout }, new Dictionary<Guid, PlannedWorkout>(), NoZones, maxHr: 190, histograms);

    result.MethodBreakdown.SampleSeconds.Should().Be(3000);
    result.MethodBreakdown.UnclassifiedSeconds.Should().Be(600);
    result.TotalSeconds.Should().Be(3600);
}

[Fact]
public void Compute_SampleHistogramSummingToZero_FallsBackToTheEstimateChain()
{
    var pid = Guid.NewGuid();
    var workout = Workout(Sport.Bike, plannedId: pid);
    var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = Planned(pid, 1, Step(600, 2)) };
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [workout.Id] = new List<ZoneHistogramEntry> { new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { workout }, structures, NoZones, maxHr: 190, histograms);

    result.MethodBreakdown.SampleSeconds.Should().Be(0);
    result.Zones.Single(z => z.ZoneNumber == 2).Seconds.Should().Be(600);
    result.MethodBreakdown.StructureSeconds.Should().Be(600);
}

[Fact]
public void Compute_SampleHistogramWithAnOutOfRangeZoneNumber_IgnoresThatBucket()
{
    var workout = Workout(Sport.Bike, duration: 1200);
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [workout.Id] = new List<ZoneHistogramEntry> { new(3, 600), new(7, 600) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { workout }, new Dictionary<Guid, PlannedWorkout>(), NoZones, maxHr: 190, histograms);

    result.Zones.Single(z => z.ZoneNumber == 5).Seconds.Should().Be(0);
    result.Zones.Single(z => z.ZoneNumber == 3).Seconds.Should().Be(600);
    result.MethodBreakdown.SampleSeconds.Should().Be(600);
}

[Fact]
public void Compute_MixedRange_SplitsSampleAndEstimateSeconds()
{
    var pid = Guid.NewGuid();
    var covered = Workout(Sport.Bike, duration: 1800);
    var structured = Workout(Sport.Bike, plannedId: pid);
    var structures = new Dictionary<Guid, PlannedWorkout> { [pid] = Planned(pid, 1, Step(600, 2)) };
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [covered.Id] = new List<ZoneHistogramEntry> { new(1, 1800) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { covered, structured }, structures, NoZones, maxHr: 190, histograms);

    var b = result.MethodBreakdown;
    b.SampleSeconds.Should().BeGreaterThan(0);
    b.StructureSeconds.Should().BeGreaterThan(0);
    (b.SampleSeconds + b.StructureSeconds + b.SessionAvgSeconds + b.UnclassifiedSeconds).Should().Be(result.TotalSeconds);
}

[Fact]
public void Compute_HistogramForAWorkoutOutsideTheList_IsIgnored()
{
    var workout = Workout(Sport.Run, avgHr: 150, duration: 1800);
    var histograms = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
    {
        [Guid.NewGuid()] = new List<ZoneHistogramEntry> { new(1, 1800) }
    };

    var result = TimeInZoneCalculator.Compute(new[] { workout }, new Dictionary<Guid, PlannedWorkout>(), NoZones, maxHr: 190, histograms);

    result.MethodBreakdown.SampleSeconds.Should().Be(0);
    result.MethodBreakdown.SessionAvgSeconds.Should().Be(1800);
}
```

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings. `dotnet test api/Bryk.sln` — all 9 new
facts in this file (the Step 4 regression fact + these 8) pass, and the 6 pre-existing facts still pass
with their original assertions.

## Step 6 — Integration tests: extend `AnalyticsControllerTests.cs`

**File:** `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs`. Add two usings:

```csharp
using Bryk.Application.ActivityFiles;
using Bryk.Application.Onboarding;
```

Append two facts (reuses `TrainingPlansControllerTests`'s multipart-upload shape from `Tasks-19-4.md` and
`ProfileControllerTests`'s onboarding-seeding pattern for the athlete/thresholds):

```csharp
[Fact]
public async Task TimeInZone_AfterCommittingAnImportedFile_ReportsSampleSeconds()
{
    await using var factory = new BrykWebApplicationFactory();
    var client = factory.CreateClient();

    // A Bike ThresholdValue gives the histogram real power bands to bucket into (ZoneService.GetZonesAsync);
    // with no threshold and no MaxHr the histogram would resolve zero buckets and prove nothing.
    await SubmitAthleteWithBikeThresholdAsync(client);

    using var content = new MultipartFormDataContent();
    var bytes = FixtureBytes("sample-ride.tcx");
    content.Add(new ByteArrayContent(bytes), "file", "sample-ride.tcx");
    var uploadResponse = await client.PostAsync("/api/v1/activityfiles", content);
    uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);

    var commitResponse = await client.PostAsJsonAsync(
        $"/api/v1/activityfiles/{uploaded!.Id}/commit", new CommitActivityFileRequest());
    commitResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    // sample-ride.tcx starts 2026-06-02T06:00:00Z (Task 19-2's committed fixture).
    var day = new DateOnly(2026, 6, 2);
    var result = await client.GetFromJsonAsync<TimeInZoneResponse>(
        $"/api/v1/analytics/time-in-zone?from={Iso(day.AddDays(-1))}&to={Iso(day.AddDays(1))}", JsonOptions);

    result.Should().NotBeNull();
    result!.MethodBreakdown.SampleSeconds.Should().BeGreaterThan(0);
    (result.MethodBreakdown.SampleSeconds + result.MethodBreakdown.StructureSeconds
        + result.MethodBreakdown.SessionAvgSeconds + result.MethodBreakdown.UnclassifiedSeconds)
        .Should().Be(result.TotalSeconds);
}

[Fact]
public async Task TimeInZone_WithNoImportedFiles_ReportsZeroSampleSeconds()
{
    await using var factory = new BrykWebApplicationFactory();
    var client = factory.CreateClient();

    var day = Today.AddDays(-3);
    await client.PostAsJsonAsync("/api/v1/workouts", LogWithOverride(day, 50m)); // duration 3600, hand-logged

    var result = await client.GetFromJsonAsync<TimeInZoneResponse>(
        $"/api/v1/analytics/time-in-zone?from={Iso(day.AddDays(-1))}&to={Iso(Today)}", JsonOptions);

    result.Should().NotBeNull();
    result!.MethodBreakdown.SampleSeconds.Should().Be(0);
    result.MethodBreakdown.UnclassifiedSeconds.Should().Be(3600);
    (result.MethodBreakdown.SampleSeconds + result.MethodBreakdown.StructureSeconds
        + result.MethodBreakdown.SessionAvgSeconds + result.MethodBreakdown.UnclassifiedSeconds)
        .Should().Be(result.TotalSeconds);
}

private static async Task SubmitAthleteWithBikeThresholdAsync(HttpClient client)
{
    var required = new OnboardingRequiredRequest
    {
        Name = "Test Athlete",
        Gender = Gender.Female,
        DateOfBirth = new DateOnly(1992, 6, 15),
        HeightCm = 170m,
        WeightKg = 65m,
        YearsTraining = 4,
        TypicalWeeklyHours = 9m,
        Methodology = MethodologyChoice.Polarized
    };
    (await client.PostAsJsonAsync("/api/v1/onboarding/required", required))
        .StatusCode.Should().Be(HttpStatusCode.NoContent);

    var recommended = new OnboardingRecommendedRequest
    {
        RestingHr = 48,
        MaxHr = 190,
        SportThresholds = new List<SportThresholdsDto>
        {
            new() { Sport = Sport.Bike, IsActive = true, ThresholdValue = 200m, Lt1 = 150m, Lt2 = 190m }
        }
    };
    (await client.PostAsJsonAsync("/api/v1/onboarding/recommended", recommended))
        .StatusCode.Should().Be(HttpStatusCode.NoContent);
}

private static byte[] FixtureBytes(string name) =>
    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));
```

Note `TimeInZone_UnlinkedNoHr_IsUnclassified_AndMethodsSumToTotal` (the pre-existing fact) needs **no**
edit — `SampleSeconds` defaults to `0` on the additive DTO field, so its existing assertions stay true
without touching that test.

**Verify:** `dotnet test api/Bryk.sln` — both new facts pass, and every pre-existing
`AnalyticsControllerTests` fact (including `TimeInZone_UnlinkedNoHr_IsUnclassified_AndMethodsSumToTotal`)
still passes unmodified.

## Step 7 — Backend full verification (API green before touching the UI)

- `dotnet build api/Bryk.sln` — 0 errors, warning count unchanged from Step 0's recorded baseline (≤ 16).
- `dotnet test api/Bryk.sln` — all green: the 9 new `TimeInZoneCalculatorTests` facts, the 2 new
  `AnalyticsControllerTests` facts, and every pre-existing test pass. Total count is at least 11 higher
  than Step 0's recorded xUnit baseline.
- `git diff --stat` sanity check so far — only
  `api/Bryk.Application/Analytics/TimeInZoneResponse.cs`, `TimeInZoneCalculator.cs`, `AnalyticsService.cs`,
  `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs`,
  `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs` should show as changed. No `Program.cs`, no
  controller, no migration, no `LoadCalculator.cs`, no `Workout.cs`, no `.csproj`. If anything else
  appears, **STOP** — that is scope creep.

Do not proceed to the frontend until this step is fully green.

## Step 8 — `ui/src/types/analytics.ts`: additive `sampleSeconds`

**File:** `ui/src/types/analytics.ts`. Replace the comment above `ZoneTime` and the
`ZoneTimeMethodBreakdown` interface with:

```ts
// Time-in-zone (ADR-0007 §4, ADR-0010 §5). zoneNumber 1..5. sampleSeconds is measured (from an imported
// file's stored histogram); the rest stays the coarse estimate chain.
export interface ZoneTime {
  zoneNumber: number
  seconds: number
}

export interface ZoneTimeMethodBreakdown {
  sampleSeconds: number
  structureSeconds: number
  sessionAvgSeconds: number
  unclassifiedSeconds: number
}
```

`TimeInZoneResponse` and every other interface in the file are untouched. Do not touch
`WeeklyLoadWeek`/`OptimalBand`/`WeeklyLoadResponse` above this block or `PeakKind`/`PeakRecord`/
`PeaksResponse` below it.

**Verify:** `pnpm run build` green (`vue-tsc -b` — a stray type error here fails the whole build, which is
the point: `TimeInZoneSection.vue`'s Step 9 edit reads `sampleSeconds` off this type).

## Step 9 — `TimeInZoneSection.vue`: the honest badge + provenance line

**File:** `ui/src/components/analytics/TimeInZoneSection.vue`.

Script addition — after the existing `segments` computed, before `const pct = ...`:

```ts
const sampleSeconds = computed(() => timeInZone.value?.methodBreakdown.sampleSeconds ?? 0)

// samples = every second in the window came from an imported file; mixed = some did; estimated = none.
const provenance = computed<'samples' | 'mixed' | 'estimated'>(() => {
  if (sampleSeconds.value === 0) return 'estimated'
  return sampleSeconds.value === total.value ? 'samples' : 'mixed'
})

const provenanceParts = computed(() => {
  const b = timeInZone.value?.methodBreakdown
  if (!b) return []
  const parts: string[] = []
  if (b.sampleSeconds > 0) parts.push(`device samples (${formatHm(b.sampleSeconds)})`)
  if (b.structureSeconds > 0) parts.push(`planned structure (${formatHm(b.structureSeconds)})`)
  if (b.sessionAvgSeconds > 0) parts.push(`session HR (${formatHm(b.sessionAvgSeconds)})`)
  if (b.unclassifiedSeconds > 0) parts.push(`unclassified (${formatHm(b.unclassifiedSeconds)})`)
  return parts
})
```

No new import needed — `computed` and `formatHm` are already imported.

Template edit 1 — the badge (currently the static `estimated` `<span>` inside the header):

```html
<span
  class="rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em]"
  :class="provenance === 'samples' ? 'text-primary-hi' : 'text-warn'"
>
  {{ provenance }}
</span>
```

(`text-primary-hi` is already in use at `WorkoutsView.vue:174`; the static class list keeps every class
except `text-warn`, which becomes the conditional's `false` branch.)

Template edit 2 — the provenance paragraph (currently the "Estimated from … Real sample data lands with
file import." block):

```html
<p class="font-mono text-[11px] text-faint">
  {{ sampleSeconds > 0 ? 'Measured from' : 'Estimated from' }} {{ provenanceParts.join(' · ') }}.<span
    v-if="sampleSeconds === 0"
  >
    Import a device file for real sample data.</span
  >
</p>
```

The sentence *"Real sample data lands with file import."* is deleted — the feature exists now.

**Do not touch** the `segments` computed, the stacked bar markup (the `.h-5` div and its `v-for`), the
per-zone legend, the sport toggle (`ChartRangeToggle`), the loading state, or the empty state. A Vitest
spec asserts on `.h-5 > div` and on the `20.83` width string; both must keep passing unchanged.

**Verify:** `pnpm run build` green.

## Step 10 — Extend `TimeInZoneSection.spec.ts`

**File:** `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts` — replace the file with:

```ts
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import TimeInZoneSection from '@/components/analytics/TimeInZoneSection.vue'
import type { TimeInZoneResponse } from '@/types/analytics'

function mountWith(timeInZone: TimeInZoneResponse | null) {
  return mount(TimeInZoneSection, {
    props: { modelValue: '' },
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {}, initialState: { analytics: { timeInZone } } })],
    },
  })
}

const sample: TimeInZoneResponse = {
  zones: [
    { zoneNumber: 1, seconds: 600 },
    { zoneNumber: 2, seconds: 0 },
    { zoneNumber: 3, seconds: 1800 },
    { zoneNumber: 4, seconds: 480 },
    { zoneNumber: 5, seconds: 0 },
  ],
  methodBreakdown: { sampleSeconds: 0, structureSeconds: 1080, sessionAvgSeconds: 1800, unclassifiedSeconds: 0 },
  totalSeconds: 2880,
}

describe('TimeInZoneSection', () => {
  it('renders the "estimated" badge when no seconds are sample-derived', () => {
    const wrapper = mountWith(sample)
    expect(wrapper.text().toLowerCase()).toContain('estimated')
  })

  it('renders the "samples" badge when every second is sample-derived', () => {
    const allSamples: TimeInZoneResponse = {
      ...sample,
      methodBreakdown: { sampleSeconds: 2880, structureSeconds: 0, sessionAvgSeconds: 0, unclassifiedSeconds: 0 },
    }
    const wrapper = mountWith(allSamples)

    expect(wrapper.text().toLowerCase()).toContain('samples')
    const badge = wrapper.findAll('span').find((s) => s.text().toLowerCase() === 'samples')
    expect(badge?.classes()).toContain('text-primary-hi')
  })

  it('renders the "mixed" badge when only some seconds are sample-derived', () => {
    const mixed: TimeInZoneResponse = {
      ...sample,
      methodBreakdown: { sampleSeconds: 1000, structureSeconds: 1080, sessionAvgSeconds: 800, unclassifiedSeconds: 0 },
    }
    const wrapper = mountWith(mixed)

    expect(wrapper.text().toLowerCase()).toContain('mixed')
  })

  it('lists device samples first in the provenance line', () => {
    const allSamples: TimeInZoneResponse = {
      ...sample,
      methodBreakdown: { sampleSeconds: 2880, structureSeconds: 0, sessionAvgSeconds: 0, unclassifiedSeconds: 0 },
    }
    const wrapper = mountWith(allSamples)

    expect(wrapper.text()).toContain('Measured from device samples')
  })

  it('drops the "Import a device file" hint once samples are present', () => {
    const withSamples: TimeInZoneResponse = {
      ...sample,
      methodBreakdown: { sampleSeconds: 2880, structureSeconds: 0, sessionAvgSeconds: 0, unclassifiedSeconds: 0 },
    }

    expect(mountWith(sample).text()).toContain('Import a device file for real sample data.')
    expect(mountWith(withSamples).text()).not.toContain('Import a device file for real sample data.')
  })

  it('renders one stacked segment per non-zero zone, sized by its share', () => {
    const wrapper = mountWith(sample)
    const segments = wrapper.findAll('.h-5 > div')

    // zones 1, 3, 4 are positive (zone 2 & 5 are zero), no unclassified remainder → 3 segments.
    expect(segments).toHaveLength(3)
    // zone 1 = 600 / 2880 ≈ 20.83%.
    expect(segments[0].attributes('style')).toContain('20.83')
  })

  it('renders "—" / empty hint when there is no classifiable training', () => {
    const wrapper = mountWith({
      zones: [{ zoneNumber: 1, seconds: 0 }],
      methodBreakdown: { sampleSeconds: 0, structureSeconds: 0, sessionAvgSeconds: 0, unclassifiedSeconds: 0 },
      totalSeconds: 0,
    })
    expect(wrapper.find('.h-5').exists()).toBe(false)
    expect(wrapper.text()).toContain('—')
  })
})
```

The last two specs (`renders one stacked segment …` and `renders "—" / empty hint …`) are unchanged apart
from the fixtures gaining `sampleSeconds: 0` — their assertions (`.h-5 > div` count, the `20.83` width
string, the empty-state text) are byte-identical to before.

**Verify:**
`pnpm exec vitest run ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts --no-file-parallelism`
— all 7 tests pass.

## Step 11 — Frontend full verification

- `pnpm run build` (from `ui/`) — `vue-tsc -b && vite build` green.
- `pnpm exec vitest run --no-file-parallelism` (from `ui/`) — full suite, not just the new file (confirms
  no regression elsewhere). Re-run once before debugging a worker crash reporting all tests passed (known
  transient fork quirk).
- `git diff --stat` sanity check — only `ui/src/types/analytics.ts`,
  `ui/src/components/analytics/TimeInZoneSection.vue`,
  `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts` should show. Confirm nothing under
  `ui/src/components/import/`, `ui/src/services/`, or `ui/src/stores/` appears — those belong to Task 19-5.

## Step 12 — Final verification, and commit

- `dotnet build api/Bryk.sln` — 0 errors, warnings unchanged from Step 0's recorded baseline (≤ 16).
- `dotnet test api/Bryk.sln` — all green, xUnit count strictly higher than Step 0's recorded baseline (11
  new: 9 `TimeInZoneCalculatorTests` + 2 `AnalyticsControllerTests`), and the existing
  `TimeInZoneCalculatorTests` assertions all still pass after the signature change.
- `cd ui; pnpm run build` — green.
- `cd ui; pnpm exec vitest run --no-file-parallelism` — all green, count strictly higher than Step 0's
  recorded baseline (7 new/rewritten specs in `TimeInZoneSection.spec.ts`; net new count depends on how
  many of the 3 pre-existing specs were rewritten vs added — confirm the file now has 7 `it` blocks).
- `git diff --stat` — the full expected set and nothing else:
  - `api/Bryk.Application/Analytics/TimeInZoneResponse.cs`, `TimeInZoneCalculator.cs`,
    `AnalyticsService.cs`
  - `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs`
  - `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs`
  - `ui/src/types/analytics.ts`
  - `ui/src/components/analytics/TimeInZoneSection.vue`,
    `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts`
  - No `Program.cs`, no controller, no migration, no `.csproj`/`package.json`, no `LoadCalculator.cs`, no
    `Workout.cs`, no `Bryk.Application/ActivityFiles/*` (read-only dependency, not owned by this task), no
    `Bryk.Infrastructure/*`, and nothing under `ui/src/components/import/`. If any of these appear —
    **STOP**, that is scope creep beyond `Tasks-19-6.md`.
- Confirm by eye: the samples branch runs first and `continue`s (a covered workout contributes to neither
  `StructureSeconds` nor `SessionAvgSeconds`); a zero-sum histogram falls through to the estimate chain;
  an out-of-range `ZoneNumber` is ignored, not clamped; the four breakdown fields sum exactly to
  `TotalSeconds` in every test; `AnalyticsService` gained exactly one constructor parameter and
  `GetTimeInZoneAsync` is the only method changed; a malformed stored histogram is skipped, not thrown.
- Record in the phase handoff, as tech debt: normalizing `ActivityFile.ZoneHistogramJson` into a real
  child table is a **Phase 21** candidate (ADR-0010 §5).

Commit with the message from `Tasks-19-6.md` (no AI co-author trailer — project convention):

```
feat: sample-derived time in zone (samples beats estimates)

Pay off the promise Phase 15 wrote into its own UI copy. TimeInZoneCalculator
gains a fourth provenance ahead of the existing three: when a workout came
from an imported file, its stored per-zone histogram - measured against the
athlete's own zones at commit - is used directly and the planned-structure and
session-HR estimates are skipped for that session (ADR-0010 5). Seconds the
file could not classify are attributed to unclassified rather than dropped, so
an import never shrinks the athlete's total training time, and a histogram
that sums to zero falls back to the estimate chain instead of silently
zeroing a session.

ZoneTimeMethodBreakdownDto gains SampleSeconds - additive, so the endpoint
stays backward-compatible - and AnalyticsService loads the covering
ActivityFile rows through the reverse lookup, which never touches the file
bytes. A malformed stored histogram is skipped rather than thrown, so bad JSON
can never turn a chart read into a 500. No migration: the histogram stays a
JSON column, and normalizing it into a table is recorded as a Phase 21
candidate.

The Progress badge stops lying. It now reads "samples" when every second in
the window is measured, "mixed" when only some are, and "estimated" when none
are, and the provenance line leads with device samples and drops the "Real
sample data lands with file import." sentence. The stacked bar, legend and
empty state are untouched; the existing calculator assertions all still hold
for ranges with no imported files, which is the regression guard.
```
