# ADR-0007 — Progress analytics (optimal band, peaks, weekly load, time-in-zone)

**Date:** 2026-06-14
**Status:** Accepted (2026-06-14) — optimal band = `[0.8, 1.3] × trailing-4-week mean actual load` (single horizontal band); peaks compute-on-read, session-level only; range-picker via `/progress?pmc=&weeks=` query params.

## Context

Phase 15 ("Progress page") turns the Phase 14 analytics spine into the athlete-facing analytics
home. It **consumes** the [ADR-0006](0006-pmc-computation.md) endpoints (`/api/v1/analytics/pmc`,
`/daily-load`) for the PMC chart and **adds** three additive compute-on-read surfaces — weekly load,
session-level peaks, and time-in-zone — that extend the exact `Bryk.Application/Analytics/` pattern:
pure calculators (like `PmcCalculator`/`AcwrCalculator`/`LoadCalculator`) + a thin `AnalyticsService`
doing the I/O + additive `AnalyticsController` actions. **No migration, no new packages** (in
particular **no charting library** — `PMCChart`/`LoadChart` are hand-rolled SVG following the
`Sparkline.vue` port of the design export's `charts.jsx`).

The math basis is unchanged from the ROADMAP *Math conventions* and ADR-0005/0006:
**EffectiveLoad** = `LoadOverride ?? ComputedLoad` for a completed `Workout`, and
`PlannedLoad ?? ComputedLoad` (`LoadCalculator.ComputePlannedLoad`) for a `PlannedWorkout`.

This ADR resolves the three genuinely-open questions the ROADMAP Phase 15 entry flags under
*Decisions needed* and pins the two remaining shapes (weekly-load, time-in-zone) so Tasks 15-1 … 15-5
build from this document alone — the same role ADR-0006 played for Phase 14.

1. **Optimal-band definition** — the cross-phase one: Phase 18's ramp model anchors its ceiling on it.
2. **Peaks strategy** — compute-on-read vs persist; session-level vs sample-derived.
3. **Range-picker URL/query convention** for the Progress page toggles.

### Conventions this ADR follows

Grounded in `AnalyticsService`, `ThisWeekService`, `LoadCalculator`, `WorkoutRepository`,
`ITrainingPlanRepository`, `IZoneService`:

- Pure calculators (no I/O, deterministic, unit-tested directly) mirror `PmcCalculator`; the service
  does the I/O (athlete via `ICurrentUserService`, the repo reads, zone resolution) and delegates.
- **"Today"** is `DateOnly.FromDateTime(DateTime.UtcNow)`, the same source `ThisWeekService` and the
  analytics range validator already use. No `IClock` abstraction.
- **ISO weeks are Monday-based**, exactly as `ThisWeekService.CurrentWeek` computes them
  (`((int)DayOfWeek + 6) % 7`). The "current week" is the Monday-anchored week containing today.
- Decimals round to **2 places** (`Math.Round(x, 2)`), as the other calculators do.
- **Honesty rule (normative).** Every rendered number traces to the athlete's actual workouts.
  Empty / insufficient states render **"—"**, never a fabricated value. Time-in-zone is **"estimated"**
  (badged) until Phase 19 file import supplies real samples. Peaks are **session-level only** —
  the highest single-workout figure — never a sample-derived duration-curve peak (Phase 19+).

## Decision

### 1. Optimal-band definition (locked — Phase 18 depends on this)

The optimal band drawn on the weekly Load chart is a **single horizontal band**:

```
band = { lower = 0.8 × A, upper = 1.3 × A }
A    = mean ACTUAL weekly load over the trailing 4 ISO weeks
       (= the 4-week rolling-average value at the most recent week of the series)
```

- **Basis is *actual* load** (Σ completed `EffectiveLoad` per week), not planned — it answers "what is
  a safe load for *this/next* week given what I've actually been doing."
- `[0.8, 1.3]` are the **ACWR sweet-spot** multipliers from the ROADMAP math conventions, so the band
  is the weekly-load expression of the same injury-risk window the dashboard ACWR chip uses.
- It is **one horizontal band**, derived from the trailing-4-week mean, *not* a per-week moving band:
  a single ceiling (`1.3 × A`) is exactly what **Phase 18's ramp model** anchors its weekly-increase
  cap on (ramp ≤ ~5–8 %/week keeps projected ACWR ≤ 1.3). A wavy per-week band would be visually
  noisy and give Phase 18 no single number to reference. **Locked once here; Phase 18 reuses `A` as
  its baseline and `1.3 × A` as its ceiling.**
- **`A` is computed over the last up-to-4 weeks present in the series** (`< 4` weeks early on uses what
  exists). When `A = 0` (a fresh athlete with no completed load), the band is **null** — the chart
  draws no band rather than a `[0, 0]` line that reads as a real range.

The 4-week rolling average itself (the dashed trend line) is, per week `i`, the mean actual load over
`[max(0, i-3), i]` — the standard trailing window, matching the design export's `LoadChart` trend.

### 2. Peaks — compute-on-read, session-level only

Peaks are **computed on every read** from the executed-`Workout` rows, exactly as the PMC is. There
is **no** persisted peaks/records table in v1. Rationale is ADR-0006's verbatim: a snapshot would need
invalidation on every Phase-13 write + a backfill, for no measured benefit at v1 volumes; and the
honest *sample-derived* peaks (mean-max power/pace duration curves) need per-sample series that only
**Phase 19** (file import) supplies. A persisted records table is a **future, approval-gated
migration** — discovering a need mid-phase is a STOP-and-ask.

**The records (all session-level, all-time, emitted only when their data exists — else absent / "—"):**

| Kind | Definition (per the sport filter) | Unit |
|---|---|---|
| **Load** | max `EffectiveLoad` (`LoadOverride ?? ComputedLoad`) over the athlete's workouts | TSS |
| **Duration** | max `ActualDurationSeconds` | seconds |
| **Distance** | max `ActualDistanceMeters` | metres |
| **Pace** (run/swim) | **fastest** session avg pace = min(`ActualDurationSeconds ÷ distance-unit`) among workouts with both fields (run = /km, swim = /100 m) | sec per unit |
| **Power** (bike) | max session avg power = **duration-weighted mean of `WorkoutStepResult.AvgPower`** over the session, among bike workouts that captured step power | watts |

- **Why derive Pace and Power.** ADR-0005 §4 keeps avg power/pace at the *step* level, not the
  session. Pace is honestly derivable from session distance ÷ duration; a session avg power is the
  duration-weighted mean of the captured per-step powers (the only honest session-level power). Both
  are emitted only when the inputs exist; a bike with no step power simply has no Power record.
- Each record carries `{ kind, sport, value, achievedDate, achievedWorkoutId, isRecent, previousValue? }`.
  `isRecent` = `achievedDate` within the last 90 days (relative to today). `previousValue` is the
  **second-best** value of that kind when ≥ 2 samples exist, so the UI can render a real improvement
  delta (`value − previousValue`, or `previousValue − value` for pace where lower is better) on the
  **`DeltaChip` for in-range (recent) records** — never a fabricated number, and absent when there is
  no prior sample.
- **Sport filter.** `?sport=` restricts to that sport (and emits only that sport's applicable kinds —
  Pace for run/swim, Power for bike). Omitted = the cross-sport set: Load + Duration over all sports;
  Distance/Pace over the distance sports; Power over bike. Strength workouts contribute only to Load
  and Duration (no distance/pace/power).

### 3. Weekly-load shape

`GET /api/v1/analytics/weekly-load?weeks=N` returns the last **N ISO weeks** ending with the current
(Monday-anchored) week:

```
WeeklyLoadResponse {
  weeks: [ { weekStart, plannedLoad, actualLoad, rollingAverage } ],   // oldest → newest
  optimalBand: { lower, upper } | null                                  // decision 1
}
```

- `weekStart` = the Monday of each ISO week.
- `plannedLoad` = Σ `EffectiveLoad` of the athlete's `PlannedWorkout`s scheduled in that week,
  computed via `LoadCalculator.ComputePlannedLoad` over the loaded structure + the athlete's sport
  profiles + effective zones — **exactly the `ThisWeekService` computation**, generalised to N weeks.
- `actualLoad` = Σ `EffectiveLoad` of the athlete's completed `Workout`s in that week (the persisted
  `LoadOverride ?? ComputedLoad`, a single-table read).
- `rollingAverage` = trailing 4-week mean of `actualLoad` (decision 1).
- The reads: `GetPlannedWorkoutsInRangeWithStructureAsync` + `GetByAthleteInRangeAsync` over the full
  N-week span (both already exist), grouped by ISO week. **No new repo read.**

`weeks` ∈ **[1, 26]**, validated via the locked `ValidateOrThrowAsync` extension (→ 400). Default `8`
applied by the controller when the param is absent.

### 4. Time-in-zone — coarse 5-level intensity, honestly estimated

`GET /api/v1/analytics/time-in-zone?from=&to=&sport=` returns a zone histogram in **seconds** with a
per-method breakdown:

```
TimeInZoneResponse {
  zones: [ { zoneNumber, seconds } ],         // zoneNumber 1..5 (coarse intensity)
  methodBreakdown: { structureSeconds, sessionAvgSeconds, unclassifiedSeconds },
  totalSeconds
}
```

Because the zone model (ADR-0004) exposes **one** metric per sport (Power Z1–Z7 for bike, Pace Z1–Z5
for run/swim) and **no HR bands**, and because the session-AvgHr fallback is inherently a 5-level HR
scheme, the histogram is a **coarse 5-bucket intensity scale** — the lowest common denominator that
keeps all three methods coherent and matches the existing UI palette (`ZoneSportCard` already colours
zones with `var(--chart-${min(zoneNumber, 5)})`). Per the math conventions, each completed `Workout`
in `[from, to]` (filtered by `?sport=` when given) is classified by, in order:

1. **structure** — the workout is linked to a `PlannedWorkout` whose blocks/steps carry zones: each
   planned step's `DurationSeconds` (× `block.Repeats`) is attributed to its zone — `TargetZone` when
   set, else the step's resolved raw target (power vs the bike power zones / pace vs the run·swim pace
   zones) classified into a zone band, else **unclassified**. Bike Z6/Z7 collapse to bucket 5 via
   `min(z, 5)`; run/swim Z1–Z5 map directly.
2. **sessionAvg** — an **unlinked** workout (or one whose structure yields nothing) with a session
   `AvgHr` and the athlete's `MaxHr` set: the whole `ActualDurationSeconds` is attributed to one
   bucket by a coarse **%HRmax** band (`AvgHr / MaxHr`: `< 0.60` → Z1, `< 0.70` → Z2, `< 0.80` → Z3,
   `< 0.90` → Z4, `≥ 0.90` → Z5). Documented, unit-tested; `%HRR` is a future refinement.
3. **unclassified** — any duration none of the above can place (a structure step with no resolvable
   zone, or a session with no `AvgHr` / `MaxHr`, or a strength workout) lands here.

**Invariant (pinned by tests):** `structureSeconds + sessionAvgSeconds + unclassifiedSeconds =
totalSeconds`, and `Σ zones[z].seconds = totalSeconds − unclassifiedSeconds`. The UI shows the stacked
histogram in zone colours, always badged **"estimated"** (none of it is sample-derived until Phase 19),
with the method breakdown the honest provenance. Strength time-in-zone is therefore all-unclassified —
honest, since strength has no zone model.

Reads: `GetByAthleteInRangeAsync` for the completed workouts + one additive
`GetPlannedWorkoutsByIdsWithStructureAsync(ids)` batch-loading the linked planned structures + the
athlete's zones (`IZoneService.GetZonesAsync`). Range rules identical to ADR-0006 §7
(both bounds required, `from ≤ to`, ≤ 400 days, no future `to`).

### 5. Range-picker URL/query convention (locked)

The Progress page holds its toggles in **`/progress` query params** (written via `router.replace`, so
they survive reload and are shareable) — this **sets the convention** for later analytics pages:

| Param | Values | Default | Drives |
|---|---|---|---|
| `pmc` | `6w` (42 d) · `3m` (90 d) · `6m` (180 d) | `3m` | the PMC chart's `from`/`to` (`to = today`) |
| `weeks` | integer `1`–`26` | `8` | the weekly-load span |

The view reads `route.query`, falls back to the defaults on absent/invalid values, and updates them on
toggle. The analytics service calls derive `from`/`to` from `pmc` and pass `weeks` straight through.

### 6. Endpoints + controller actions (additive)

Three additive `AnalyticsController` actions, athlete always via `ICurrentUserService`:

| Endpoint | Returns |
|---|---|
| `GET /api/v1/analytics/weekly-load?weeks=8` | `WeeklyLoadResponse` (decision 3 + 1). |
| `GET /api/v1/analytics/time-in-zone?from=&to=&sport=` | `TimeInZoneResponse` (decision 4). |
| `GET /api/v1/analytics/peaks?sport=` | `PeaksResponse { records: PeakRecordDto[] }` (decision 2). |

## Consequences

**Closed by this decision:** the ROADMAP Phase 15 *Decisions needed* — the optimal-band definition
(and its Phase-18 contract), peaks persistence (compute-on-read, session-level), and the range-picker
URL/query convention; plus the weekly-load and time-in-zone shapes.

**Created by this decision (no migration, no new package):**

- `Bryk.Application/Analytics/`: pure `WeeklyLoadCalculator` (rolling average + band),
  `PeaksCalculator` (records from per-workout session summaries), `TimeInZoneCalculator`
  (the 3-method classifier); the `WeeklyLoadResponse`/`WeeklyLoadWeekDto`/`OptimalBandDto`,
  `PeaksResponse`/`PeakRecordDto` (+ a `PeakKind` enum), `TimeInZoneResponse`/`ZoneTimeDto`/
  `ZoneTimeMethodBreakdownDto` shapes; a `WeeklyLoadRequest` (+ validator); `TimeInZoneRequest`
  reusing the ADR-0006 range rules + `sport`.
- `AnalyticsService` gains `GetWeeklyLoadAsync`, `GetPeaksAsync`, `GetTimeInZoneAsync` (it takes
  `ITrainingPlanRepository`, `IAthleteRepository`, `IZoneService` as new ctor deps, mirroring
  `ThisWeekService`). One additive repo read: `ITrainingPlanRepository.GetPlannedWorkoutsByIdsWithStructureAsync`.
- `AnalyticsController` gains three additive actions. No DI changes beyond the new ctor deps (all
  already registered).
- UI: `types/analytics.ts` + `services/analytics.ts` mirrors (the latter already has `getDailyLoad`
  from Phase 14), an analytics-store extension, the hand-rolled `PMCChart.vue` / `LoadChart.vue`
  (ported from `charts.jsx`), a `TimeInZoneBar`/peaks grid, the data-transform composables (Vitest),
  and `ProgressView.vue` at `/progress` with the nav lit live (`AppSidebar` + mobile tab bar).

**Phase 18 depends on this** — the optimal band's `A` (trailing-4-week mean actual) and `1.3 × A`
ceiling are the periodization baseline and ramp cap; `LoadChart` is reused to render Phase-18 targets.

### For Tasks 15-1 … 15-5

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **15-1** `WeeklyLoadCalculator` + `PeaksCalculator` + DTOs + `weekly-load`/`peaks` endpoints + xUnit | Backend | ADR-0007 | Decisions 1–3, 6 (band, peaks records, weekly shape, validation). |
| **15-2** `TimeInZoneCalculator` + `time-in-zone` endpoint + xUnit (classification + sums-to-total) | Backend | ADR-0007 | Decision 4 (3 methods, %HRmax, the invariant). |
| **15-3** `PMCChart` port (CTL/ATL lines + daily-load bars) + 6w/3m/6m toggle + transform composable (Vitest) | Frontend | 15-1, ADR-0006 endpoints | Decision 5 (range convention). |
| **15-4** `LoadChart` port (8-week bars, planned hatch, optimal band, 4-week trend) + transform (Vitest) | Frontend | 15-1 | Decisions 1, 3 (band + weekly shape). |
| **15-5** `ProgressView` + nav live; time-in-zone stacked bars (+"estimated" badge) + peaks `MetricTile` grid; assembly | Frontend | 15-2, 15-3, 15-4 | Decisions 2, 4, 5. |

## Alternatives considered

- **Per-week moving optimal band.** Rejected (decision 1) — visually noisy and gives Phase 18 no single
  ramp ceiling to anchor on; the trailing-4-week single band is the cross-phase contract.
- **Planned-load basis for the band.** Rejected (decision 1) — the band answers "is my *actual* load
  safe," and the ACWR sweet spot is defined on actual load; planned is the prescription, not the dose.
- **Persist a peaks / records table.** Rejected for v1 (decision 2) — invalidation on every Phase-13
  write + backfill for no v1 benefit; sample-derived curves belong with Phase 19. Re-openable as an
  approval-gated migration.
- **Native per-sport zone counts (Power Z1–Z7) for time-in-zone.** Rejected (decision 4) — the
  session-AvgHr fallback is inherently a 5-level HR scheme and there are no HR bands, so a 7-bucket
  histogram can't coherently mix the methods; the coarse 5-level intensity scale (with `min(z,5)`
  collapse) keeps them coherent and matches the UI palette. The fine 7-zone split can return with
  sample data in Phase 19+.
- **Scaling a linked workout's planned structure to its actual duration for time-in-zone.** Rejected —
  the math convention says "planned structure (per-step duration × zone target)"; using planned
  durations as-is is the honest estimate and keeps the sums-to-total invariant trivial. It is labeled
  estimated regardless.
- **Improvement-vs-fabricated-baseline for peaks deltas.** Rejected — the `DeltaChip` improvement is
  `value − second-best` (a real prior sample) or absent; no synthesized baseline.
- **Component-local toggle state (no URL).** Rejected (decision 5) — lost on reload, not shareable,
  sets no convention for later pages.
