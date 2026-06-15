# HANDOFF — Phase 15 complete (Progress page)

**Date:** 2026-06-14
**Phase:** 15 — Progress page (PMC chart, weekly load, time-in-zone, peaks) (✅ COMPLETE)
**Decision:** `md/decisions/0007-progress-analytics.md` (Accepted 2026-06-14).
**Specs:** `md/Tasks-15-1.md` … `md/Tasks-15-5.md` (committed `179d214`).
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is read from a request/query.

Phase 15 turns the Phase-14 analytics spine into the athlete-facing Progress page: it **consumes**
`/analytics/pmc` and **adds** weekly-load, peaks, and time-in-zone — three additive compute-on-read
surfaces — then ports the design-export `PMCChart` / `LoadChart` as hand-rolled SVG (no chart lib) and
assembles `/progress`. **No migration, no new packages, no snapshot table** (compute-on-read per ADR-0007).

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| ADR-0007 | Docs | Optimal band = `[0.8, 1.3] × trailing-4-week mean actual` (single horizontal band; Phase-18 contract); peaks compute-on-read session-level; weekly-load shape; time-in-zone 5-level coarse "estimated" model; range-picker `?pmc=&weeks=&sport=` convention | `345e1fa` |
| 15-1 | Backend | Pure `WeeklyLoadCalculator` (4-week rolling avg + band) + `PeaksCalculator` (session Load/Duration/Distance/Pace/Power, best + second-best, 90-day recency) + DTOs; `GET /analytics/weekly-load`, `/peaks`; additive `GetByAthleteWithStepResultsAsync`; xUnit | `5e3dd04` |
| 15-2 | Backend | Pure `TimeInZoneCalculator` (5-bucket intensity histogram; structure / sessionAvg(%HRmax) / unclassified; per-method sums to total) + `GET /analytics/time-in-zone`; additive `GetPlannedWorkoutsByIdsWithStructureAsync`; xUnit | `97042f1` |
| 15-3 | Frontend | `PMCChart.vue` (SVG port) + pure `buildPmcGeometry` (Vitest) + `ChartRangeToggle` + `PmcChartSection`; progress-scoped pmc store state | `c043d1c` |
| 15-4 | Frontend | `LoadChart.vue` (planned hatch vs actual fill, optimal band, 4-week trend) + pure `buildLoadGeometry` (Vitest) + `LoadChartSection`; `WeeklyLoad*` types + service + store | `b1de9a0` |
| 15-5 | Frontend | `ProgressView` at `/progress` (headline CTL/ATL/TSB/ACWR tiles + the four sections), `TimeInZoneSection` (stacked bar + "estimated" + method breakdown) + `PeaksSection` (MetricTile grid + DeltaChip), nav live (sidebar + mobile tab), `?pmc/?weeks/?sport` query convention, shared `lib/format.ts` | `263a89f` |
| fix | Both | Per-sport pace peaks — run sec/km and swim sec/100 m never compared (live-verification finding) | `1b4179a` |

## Verification state

- **Backend:** `dotnet build` clean (only the known design-time `System.Security.Cryptography.Xml`
  advisory). `dotnet test api/Bryk.sln` green — **148 tests** (101 application + 47 integration; was 119).
- **Frontend:** `pnpm run build` (vue-tsc) green; `pnpm test` green — **120 tests / 40 files** (was 97/33).
  *Run `vitest run --no-file-parallelism` for a clean exit* (the transient worker crash noted in memory).
  **No chart library in `package.json`** (success criterion — grep-verified).
- **Live end-to-end (dev API on SQL Server `IRONMAN` + `db/dev-seed.sql`, athlete `…112`, api-today 2026-06-14):**
  - `/progress` rendered all four sections from seed with **zero console errors/warnings**. Headline:
    CTL 13 · ATL 50 · TSB −40 Fatigued · ACWR — (Need 28 days). 20 `<svg>`, the planned-hatch `<pattern>`
    + actual-fill gradient distinguishable, the optimal-band rect labelled "OPTIMAL BAND".
  - **Weekly-load:** the band = `[0.8, 1.3] × 155.89` rolling avg = `[124.71, 202.66]`. **Band moves on
    workout change:** logging a 200-TSS workout this week shifted the rolling avg 155.89→205.89 and the
    band to `[164.71, 267.66]`; delete restored it exactly. Planned (162.56 / 330.62 / 380.81) vs actual
    (174.2 / 449.35 / 0) render as hatch-behind-fill bars.
  - **Peaks:** Best Load 118 TSS (+18, Bike), Longest 2:30:00 (+1:00:00, Bike), Longest Distance 65.0 km
    (+49.0 km, Bike), **Fastest Pace 5:00/km (−0:37, Run) AND 2:16/100m (Swim)** — two per-sport records
    after the fix, Best Power 176 W (+12, Bike). DeltaChips show only for recent records with a prior best.
  - **Time-in-zone:** the "estimated" badge renders with the honest provenance "Estimated from planned
    structure (1h 45m) · session HR (7h 0m) · unclassified (50m)" — `6300 + 25200 + 3000 = 34500` total;
    the stacked bar segments sum to total − unclassified (verified end-to-end).
  - **Fresh athlete (DevAuth swapped to a fresh GUID, restored after):** all four endpoints return honest
    empties (weekly-load 8 zero weeks + band null, peaks `[]`, pmc `current` null, time-in-zone total 0).
    `/progress` rendered headline tiles "—", ACWR "Need 28 days", time-in-zone "No classifiable training",
    peaks "No records yet", **no optimal band** — zero console errors. The Load/PMC charts render flat-zero
    (the backend returns zero-filled series/weeks, not empty arrays — zero load is honest, not fabricated).
  - **Seed left intact** — the verification workout was deleted; the DevAuth secret restored to `…112`.

> Preview note (carry-forward): the preview tab freezes `requestAnimationFrame`, wedging Vue's
> `<Transition mode="out-in">` on lazy-route first load. To drive the in-browser SPA pass: full-reload to
> `/` (HomeView is non-lazy → renders without a transition), then shim `rAF→setTimeout`, inject
> `.page-*{transition:none}`, and `router.push('/progress')` from that clean state. Read `#app.textContent`
> (not `innerText`, which needs layout the frozen tab won't compute). See `[[preview-raf-frozen-transition-shim]]`.

## Success criteria (ROADMAP Phase 15) — checked

- **`/progress` renders all four sections from seed, zero console errors, no chart lib** — ✅ (live + grep).
- **Planned hatch vs actual fill distinguishable; band/trend move when workouts change** — ✅ (`<pattern>`
  hatch; band shifted 124.71/202.66 → 164.71/267.66 on log, restored on delete).
- **Time-in-zone badge logic correct and per-method seconds sum to total** — ✅ (always "estimated";
  6300+25200+3000=34500, pinned in xUnit + verified live).
- **Vitest covers chart data-transform composables** — ✅ (`buildPmcGeometry`, `buildLoadGeometry` specs).

## Decisions made (ADR-0007)

- **Optimal band = `[0.8, 1.3] × A`**, `A` = trailing-4-week mean *actual* load (the rolling-average value
  at the latest week); a **single horizontal band**, null when `A = 0`. **Phase 18 reuses `A` as its ramp
  baseline and `1.3 × A` as its weekly-increase ceiling** — locked once here.
- **Peaks: compute-on-read, session-level only.** No table (= migration); sample-derived duration curves
  pair with Phase 19. Pace is **per-sport** (run /km, swim /100 m never compared); records emit only when
  data exists; `previousValue` = second-best for an honest DeltaChip improvement on recent records.
- **Time-in-zone: a coarse 5-level intensity histogram**, always "estimated" — planned structure
  (`TargetZone`, bike Z6/Z7→5) for linked workouts, `%HRmax` session-AvgHr otherwise, else unclassified;
  the per-method seconds sum to the total. Stays coarse until Phase 19 file import.
- **Range-picker convention:** `/progress?pmc=6w|3m|6m` (default 3m), `?weeks=1..26` (default 8), `?sport=`
  — written via `router.replace`, validated/defaulted on read.

## New surface (no migration, no new packages)

- `Bryk.Application/Analytics/`: `WeeklyLoadCalculator`, `PeaksCalculator` (+ `PeakWorkoutSummary`),
  `TimeInZoneCalculator`, the `WeeklyLoadResponse`/`OptimalBandDto`, `PeaksResponse`/`PeakRecordDto`/`PeakKind`,
  `TimeInZoneResponse`/`ZoneTimeDto`/`ZoneTimeMethodBreakdownDto` shapes, `WeeklyLoadRequest` (+ validator).
- `AnalyticsService` gains `GetWeeklyLoadAsync`/`GetPeaksAsync`/`GetTimeInZoneAsync` (new ctor deps:
  `ITrainingPlanRepository`, `IAthleteRepository`, `IZoneService`, `IValidator<WeeklyLoadRequest>`); two
  additive repo reads (`IWorkoutRepository.GetByAthleteWithStepResultsAsync`,
  `ITrainingPlanRepository.GetPlannedWorkoutsByIdsWithStructureAsync`). No `Program.cs` DI change.
- `AnalyticsController`: three additive actions (`weekly-load`, `peaks`, `time-in-zone`).
- UI: `lib/charts/pmc.ts` + `load.ts` (pure transforms), `components/charts/` (`PMCChart`, `LoadChart`,
  `ChartRangeToggle`, `PmcChartSection`, `LoadChartSection`), `components/analytics/`
  (`TimeInZoneSection`, `PeaksSection`), `views/ProgressView.vue`, `lib/format.ts`, `types/analytics.ts`
  + `services/analytics.ts` + `stores/analytics.ts` extensions, the `/progress` route, Progress nav live.

## Known gaps / carry-forward

- **Time-in-zone is coarse + "estimated"** (5-level, structure/AvgHr/unclassified) until **Phase 19**
  file import supplies real per-sample zone time. The badge + method breakdown make this honest in-UI.
- **Peaks are session-level only** — duration-curve / mean-max peaks need samples (Phase 19+).
- **ACWR shows "—" in the stock dev dashboard** (seed first workout ~18 days back, < 28 days) — back-date
  a seed row to demo the numeric ACWR tile.
- **`LoadChart` is reused by Phase 18** to render the periodization target ramp (targets in place of the
  planned hatch); the optimal band's `A` / `1.3 × A` is the Phase-18 baseline / ceiling.
- **CLAUDE.md tech-debt list** (DbUpdateException→409, NotImplemented→501, ProblemDetails,
  per-version SwaggerDoc) untouched by Phase 15.

## Next — Phase 16 (Calendar & scheduling)

Month/week calendar merging planned + completed + events, reschedule (pointer-drag / tap-move, no DnD
lib), compliance coloring. `GET /api/v1/calendar?from=&to=` + a lightweight schedule `PATCH`. Needs a
compliance-thresholds mini-ADR (reused by 18). Authentication (Phase 12) remains the declared
approval-gated phase and is still deferred.

## Session-start checklist

1. Read this handoff + ADR-0007 + `md/Tasks-15-*.md` and the ROADMAP Phase 16 entry.
2. `git status` clean; `git log --oneline -15`.
3. Backend: `dotnet test api/Bryk.sln` (expect 148). Frontend (from `ui/`): `pnpm run build` +
   `pnpm exec vitest run --no-file-parallelism` (expect 120; re-run plain `pnpm test` once if the
   transient worker crash appears with all tests passing).
4. `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` +
   `DevAuth:CurrentAthleteId` (`…112`). Seed: paste the athlete id into `db/dev-seed.sql` and run it.
5. Dev stack: API (`dotnet run` from `api/Bryk.API`, https://localhost:60129 / http://localhost:60130);
   `pnpm dev` from `ui/` (vite proxies `/api` → 60129).
