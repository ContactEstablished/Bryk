# Task 19-6 — sample-derived time-in-zone (`SampleSeconds`, `samples` provenance)

## Surface
Backend + Frontend. One additive field on `ZoneTimeMethodBreakdownDto`, a fourth provenance branch in
`TimeInZoneCalculator` that takes precedence over the existing three, the `AnalyticsService` read that
loads the covering `ActivityFile` rows, the matching TypeScript type, and the honest-badge rewrite in
`TimeInZoneSection.vue`. **No migration, no new package, no new endpoint, no `Program.cs` change.**

## Why
Phase 15 shipped time-in-zone with a permanently-lit `estimated` badge and a sentence that literally
reads *"Real sample data lands with file import."* (`TimeInZoneSection.vue:119`). This is that task: the
promise made in Phase 15's own UI copy, paid off. `TimeInZoneCalculator` currently classifies a workout
one of three ways — planned structure, coarse session-`AvgHr` %HRmax, or unclassified — and every one of
them is an estimate. An imported file has a real per-sample histogram computed against the athlete's own
zones, so for those workouts the chart can stop guessing. The read has to union the two sources rather
than replace them, because a 90-day range will mix imported and hand-logged sessions for a long time,
and the badge has to tell the truth about which it is showing.

## Depends on
- **Task 19-2** — `ZoneHistogramEntry` (the persisted JSON's element shape) and
  `ZoneHistogramCalculator`'s bucket semantics: five entries, `ZoneNumber` 1..5, bike Z6/Z7 already
  collapsed, seconds only for samples with a usable signal. Read-only.
- **Task 19-4** — writes `ActivityFile.ZoneHistogramJson` at commit, serialized with
  `new JsonSerializerOptions(JsonSerializerDefaults.Web)` (so the stored JSON is camelCase:
  `[{"zoneNumber":1,"seconds":600}, …]`). Deserialize with the same defaults.
- **Task 19-1** — `IActivityFileRepository.GetByParsedWorkoutIdsAsync(athleteId, workoutIds, ct)` and its
  `AddScoped` registration (already in `Program.cs`; the new constructor parameter resolves with **no**
  DI change).
- **ADR-0010 §5** — sample-derived seconds report method `samples` and take precedence over structure and
  sessionAvg for covered workouts; `SampleSeconds` is additive; normalizing the JSON into a table is a
  Phase 21 candidate.
- **ADR-0007 §4** — the five buckets, the `Math.Min(z, 5)` collapse and the coarse %HRmax scheme this
  task leaves exactly as they are for uncovered workouts.

## Required reading
- `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs` — all 147 lines. The three-way branch at
  L27–77 (structure L35–61, sessionAvg L62–71, unclassified L72–76), `ZoneCount = 5` (L14), and the
  accumulator/return block at L79–93. This is the file you are extending, not rewriting.
- `api/Bryk.Application/Analytics/TimeInZoneResponse.cs` — 27 lines; `ZoneTimeMethodBreakdownDto` at
  L14–19 and its comment ("no sample-derived zone time until Phase 19 file import"), which this task
  makes obsolete and must update.
- `api/Bryk.Application/Analytics/AnalyticsService.cs:120–143` — `GetTimeInZoneAsync` end to end: the
  reused `AnalyticsRangeRequest` validation, the `GetByAthleteInRangeAsync` read, the optional sport
  filter, the linked-plan structures lookup, `zoneService.GetZonesAsync`, `athleteRepo
  .GetWithSportProfilesAsync`, and the delegate at L142. The primary ctor is at L11–18.
- `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs` (from 19-1) — note the contract: it never loads
  `Content`, and an empty id list returns an empty list with no query.
- `api/Bryk.Application/ActivityFiles/ZoneHistogramEntry.cs` (from 19-2) — the record being deserialized.
- `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs` — the existing suite and its
  private `Workout(...)` / `Planned(...)` / `Step(...)` helpers and `NoZones` constant. Every existing
  `Compute(...)` call site changes in this task (one new argument); the assertions must not.
- `ui/src/components/analytics/TimeInZoneSection.vue` — the badge at L68–72, the segment builder at
  L42–57 (**leave it alone**), the stacked bar at L92–100 (**leave it alone** — a spec asserts on
  `.h-5 > div`), and the provenance paragraph at L111–120 which this task replaces.
- `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts` — the two `methodBreakdown` fixtures
  (L24, L47) that must gain `sampleSeconds`, and the `always renders the "estimated" badge` spec (L29)
  that must be rewritten.
- `ui/src/types/analytics.ts:58–68` — `ZoneTimeMethodBreakdown` / `TimeInZoneResponse`.

## Acceptance criteria

### `api/Bryk.Application/Analytics/TimeInZoneResponse.cs` (edit — additive)

```csharp
public class ZoneTimeMethodBreakdownDto
{
    public int SampleSeconds { get; set; }
    public int StructureSeconds { get; set; }
    public int SessionAvgSeconds { get; set; }
    public int UnclassifiedSeconds { get; set; }
}
```
- `SampleSeconds` first, matching the precedence order.
- Update the type's comment: the four now sum to `TotalSeconds`; sample-derived seconds come from an
  imported file's stored histogram (ADR-0010 §5) and are **measured**, not estimated. Delete the
  "no sample-derived zone time until Phase 19 file import" clause — it is no longer true.
- **Additive only.** `ZoneTimeDto` and `TimeInZoneResponse` are otherwise unchanged; no field is removed
  or renamed, so `GET /analytics/time-in-zone` stays backward-compatible.

### `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs` (edit)

New required parameter, **appended last**:
```csharp
public static TimeInZoneResponse Compute(
    IReadOnlyList<Workout> workouts,
    IReadOnlyDictionary<Guid, PlannedWorkout> structures,
    ZonesResponse zones,
    int? maxHr,
    IReadOnlyDictionary<Guid, IReadOnlyList<ZoneHistogramEntry>> sampleHistograms)
```
Required, not optional-with-a-default: there is exactly one production call site and a handful of tests,
and an explicit argument makes "this range has no imported files" a visible statement rather than an
omission.

New **first** branch inside the `foreach (var workout in workouts)` loop, before the existing
`planned is not null` check:
```csharp
// 0. samples — an imported file's measured per-zone histogram (ADR-0010 §5). It wins outright over
// structure and sessionAvg: those are estimates of the same time. A histogram that sums to zero is
// treated as absent (the file carried no usable signal) and the workout falls through to the
// estimate chain below.
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
```
- Declare `var sampleSeconds = 0;` alongside the existing three accumulators (L23–25) and add it to the
  emitted `ZoneTimeMethodBreakdownDto` and to `TotalSeconds`
  (`sampleSeconds + structureSeconds + sessionAvgSeconds + unclassifiedSeconds`).
- Out-of-range `ZoneNumber`s in a stored histogram are **ignored**, not clamped: the histogram was
  already collapsed to 1..5 by `ZoneHistogramCalculator`, so anything else means corrupt data and must
  not be silently folded into bucket 5.
- Update the class `<summary>`: it is no longer honestly-"estimated" across the board — name the four
  provenances and cite ADR-0010 §5 alongside ADR-0007 §4. Delete "Stays coarse until Phase 19 supplies
  real samples."
- **Do not touch** `ClassifyStep`, `HrZone`, `Midpoint`, the structure branch, the sessionAvg branch, or
  the `zoneList` projection. The whole point is that an uncovered workout's numbers are byte-identical
  to today's.

### `api/Bryk.Application/Analytics/AnalyticsService.cs` (edit)

- Primary ctor gains **one** parameter, appended after `athleteRepo`:
  `IActivityFileRepository fileRepo`. Final signature:
  `(ICurrentUserService currentUser, IValidator<AnalyticsRangeRequest> validator,
  IValidator<WeeklyLoadRequest> weeklyValidator, IWorkoutRepository workoutRepo,
  ITrainingPlanRepository planRepo, IAthleteRepository athleteRepo, IActivityFileRepository fileRepo,
  IZoneService zoneService)`. **No `Program.cs` change** — the repository is already registered by 19-1
  and DI resolves the new parameter automatically.
- `private static readonly JsonSerializerOptions HistogramJsonOptions = new(JsonSerializerDefaults.Web);`
  — the same defaults 19-4 serializes with, so camelCase names round-trip.
- In `GetTimeInZoneAsync`, between the structures lookup (L136–137) and the delegate (L142):
  ```csharp
  // Sample-derived histograms for any workout in this range that came from an imported file
  // (ADR-0010 §5). The reverse lookup never loads the file bytes.
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
  ```
  then pass `sampleHistograms` as `Compute`'s fifth argument.
- Note that the ids passed are the **already sport-filtered** `workouts` list, so a `?sport=` query
  narrows the file lookup too.
- **Do not touch** `GetDailyLoadAsync`, `GetPmcAsync`, `GetWeeklyLoadAsync`, `GetPeaksAsync`,
  `ToSummary`, `WeekStart`, `BuildSeriesAsync`, `ComputeFrom`, or either `Slice`.

### `ui/src/types/analytics.ts` (edit — additive)

```ts
export interface ZoneTimeMethodBreakdown {
  sampleSeconds: number
  structureSeconds: number
  sessionAvgSeconds: number
  unclassifiedSeconds: number
}
```
Update the neighbouring comment (L52) so it no longer calls the whole thing "estimated".

### `ui/src/components/analytics/TimeInZoneSection.vue` (edit)

In `<script setup>`, add:
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
Template changes, and only these two:
- The badge (currently L68–72) becomes `{{ provenance }}` with a conditional colour:
  `:class="provenance === 'samples' ? 'text-primary-hi' : 'text-warn'"`, keeping the existing
  `rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em]` classes.
  (`text-primary-hi` is already in use at `WorkoutsView.vue:174`.)
- The provenance paragraph (currently L111–120) becomes:
  ```html
  <p class="font-mono text-[11px] text-faint">
    {{ sampleSeconds > 0 ? 'Measured from' : 'Estimated from' }} {{ provenanceParts.join(' · ') }}.<span
      v-if="sampleSeconds === 0"
    >
      Import a device file for real sample data.</span
    >
  </p>
  ```
  The sentence *"Real sample data lands with file import."* is **deleted** — the feature exists now.
- **Do not touch** the `segments` computed (L42–57), the stacked bar markup (L92–100), the legend
  (L103–108), the sport toggle, the loading state, or the empty state. A spec asserts on `.h-5 > div`
  and on the `20.83` width; both must keep passing unchanged.

## Non-goals
- **No migration.** The histogram lives in `ActivityFile.ZoneHistogramJson` (ADR-0010 §5). If a
  `WorkoutZoneDuration` table or any other schema change seems necessary — **STOP and ask** (Sr. Dev
  gate). Normalizing the JSON is an explicit **Phase 21** candidate; record it in the phase handoff as
  tech debt, do not implement it here.
- **Do not add `Workout.SourceFileId`.** The join is `ActivityFile.ParsedWorkoutId → Workout.Id` through
  `GetByParsedWorkoutIdsAsync`. If you reach for a column on `Workout` — **STOP and ask**.
- **Do not edit `api/Bryk.Application/Training/Load/LoadCalculator.cs`** — frozen for Phase 19.
- **Do not change `IActivityFileRepository`** or any other repository contract — a persistence-boundary
  change is a Sr. Dev gate. The reverse lookup 19-1 shipped is sufficient; if it is not, **STOP and ask**.
- **Do not change `TimeInZoneCalculator`'s existing three branches or their numbers.** A range with no
  imported files must produce a byte-identical response to today's, and a regression test proves it.
- **Do not** add a `method` string field, a per-workout provenance array, a new query parameter, or a new
  endpoint. `SampleSeconds` on the existing breakdown is the whole API surface change.
- **Do not modify** `PmcCalculator`, `AcwrCalculator`, `WeeklyLoadCalculator`, `PeaksCalculator`, or any
  other `GetXxxAsync` on `AnalyticsService`.
- Do not write files owned by siblings: `ActivityFile.cs` / `IActivityFileRepository.cs` /
  `ActivityFileRepository.cs` / `ApplicationDbContext.cs` / `Program.cs` (19-1, 19-4),
  `Bryk.Application/ActivityFiles/*` (19-2, 19-4), `Bryk.Infrastructure/ActivityFiles/*` +
  `Bryk.Infrastructure.csproj` (19-2, 19-3), `ActivityFilesController.cs` (19-4),
  `ui/src/services/api.ts` / `activityFiles.ts` / `stores/activityFiles.ts` /
  `views/WorkoutsView.vue` / `views/WorkoutDetailView.vue` / `ui/src/components/import/*` (19-5).
- **No new NuGet or npm package.** `System.Text.Json` is in the framework.
- **No auth code** — Phase 12 stays deferred and approval-gated.
- **No ProblemDetails / error-contract rework** — Phase 21 owns it.
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

**Unit — `api/Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests.cs` (extend).**
Add a shared `private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>
NoSamples = new Dictionary<Guid, IReadOnlyList<ZoneHistogramEntry>>();` and pass it at every existing
call site. **Every existing assertion must remain unchanged** — that is the regression guard.

- `Compute_WithoutSampleHistograms_IsUnchanged` — an explicit new fact asserting that a structured
  workout and a sessionAvg workout produce the same `Zones`, `MethodBreakdown` and `TotalSeconds` as
  before, with `SampleSeconds == 0`.
- `Compute_SampleHistogram_PopulatesZonesAndSampleSeconds` — one workout with
  `ActualDurationSeconds = 3600` and a histogram `[(1,600),(2,1200),(3,1800),(4,0),(5,0)]` →
  `Zones[1] == 600`, `Zones[2] == 1200`, `Zones[3] == 1800`,
  `MethodBreakdown.SampleSeconds == 3600`, `UnclassifiedSeconds == 0`, `TotalSeconds == 3600`.
- `Compute_SampleHistogram_TakesPrecedenceOverPlannedStructure` — the same workout **also** has a linked
  planned structure worth 1080 s; assert `StructureSeconds == 0` and `SampleSeconds == 3600` (the
  ADR-0010 §5 precedence pin).
- `Compute_SampleHistogram_TakesPrecedenceOverSessionAvg` — the workout has `AvgHr = 150` and
  `maxHr = 190` but a histogram; `SessionAvgSeconds.Should().Be(0)`.
- `Compute_SampleHistogramShorterThanTheSession_AttributesTheRemainderToUnclassified` — duration 3600,
  histogram summing to 3000 → `SampleSeconds == 3000`, `UnclassifiedSeconds == 600`,
  `TotalSeconds == 3600`.
- `Compute_SampleHistogramSummingToZero_FallsBackToTheEstimateChain` — histogram `[(1,0)…(5,0)]` on a
  workout with a planned structure → `SampleSeconds == 0` and the structure branch runs as it would
  without any histogram.
- `Compute_SampleHistogramWithAnOutOfRangeZoneNumber_IgnoresThatBucket` — an entry `(7, 600)` alongside
  `(3, 600)` → `Zones[5]` unaffected, `SampleSeconds == 600`.
- `Compute_MixedRange_SplitsSampleAndEstimateSeconds` — two workouts, one covered and one structured →
  both `SampleSeconds` and `StructureSeconds` positive, and the four breakdown fields sum exactly to
  `TotalSeconds`.
- `Compute_HistogramForAWorkoutOutsideTheList_IsIgnored` — a dictionary entry keyed on an unrelated
  `Guid` changes nothing.

**Integration — `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs` (extend).**
- `TimeInZone_AfterCommittingAnImportedFile_ReportsSampleSeconds` — upload `sample-ride.tcx` through
  `POST /api/v1/activityfiles`, commit it, then `GET /api/v1/analytics/time-in-zone?from=&to=` covering
  the fixture's date with the athlete seeded so the histogram is non-empty (an `AthleteSportProfile`
  with a bike `ThresholdValue`, or an `Athlete.MaxHr`, so a bucket resolves) →
  `methodBreakdown.sampleSeconds > 0` and the four breakdown fields sum to `totalSeconds`.
- `TimeInZone_WithNoImportedFiles_ReportsZeroSampleSeconds` — the existing hand-logged path →
  `sampleSeconds == 0` and the rest of the response unchanged.

**Vitest — `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts` (extend/adjust).**
Both existing `methodBreakdown` fixtures (L24, L47) gain `sampleSeconds: 0`.
- Rewrite `always renders the "estimated" badge` → `renders the "estimated" badge when no seconds are
  sample-derived`.
- `renders the "samples" badge when every second is sample-derived` — `sampleSeconds` equal to
  `totalSeconds`; assert the badge text and that it carries `text-primary-hi`.
- `renders the "mixed" badge when only some seconds are sample-derived`.
- `lists device samples first in the provenance line` — text contains `Measured from device samples`.
- `drops the "Import a device file" hint once samples are present` — the hint appears when
  `sampleSeconds === 0` and is absent otherwise.
- The two existing specs (`renders one stacked segment per non-zero zone…` asserting `.h-5 > div` and
  `20.83`, and the empty-state spec) must pass **unmodified apart from the fixture field**.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must rise from the **262** baseline (173 `Bryk.Application.Tests` + 89 `Bryk.API.Tests`) plus what
19-1 … 19-4 added, with zero failures — and the existing `TimeInZoneCalculatorTests` assertions must
still pass after the signature change. Vitest must rise from **252 / 56 files** plus 19-5's additions.
`pnpm run build` must be green (the `sampleSeconds` type change is caught by `vue-tsc`). Warnings must
not exceed **16**.

## Review checklist
- [ ] `ZoneTimeMethodBreakdownDto` gained `SampleSeconds` and lost nothing; the response is
      backward-compatible.
- [ ] The samples branch runs **first** and `continue`s — a covered workout contributes to neither
      `StructureSeconds` nor `SessionAvgSeconds`.
- [ ] A zero-sum histogram falls through to the estimate chain; an out-of-range `ZoneNumber` is ignored,
      not clamped.
- [ ] The four breakdown fields sum exactly to `TotalSeconds` in every test.
- [ ] `AnalyticsService` gained exactly one constructor parameter, `GetTimeInZoneAsync` is the only
      method changed, and **no `Program.cs` line was needed**.
- [ ] A malformed stored histogram is skipped, not thrown — `GET /analytics/time-in-zone` cannot 500 on
      bad JSON.
- [ ] `TimeInZoneSection.vue`'s bar, legend, segment builder and empty state are untouched; only the
      badge and the provenance paragraph changed; the "Real sample data lands with file import."
      sentence is gone.
- [ ] `git diff --stat` shows no migration, no `LoadCalculator.cs`, no `Workout.cs`, no
      `Bryk.Application/ActivityFiles/*`, no `Bryk.Infrastructure/*`, and nothing under
      `ui/src/components/import/`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
