# Task 15-5 — `ProgressView` + nav live; time-in-zone + peaks; assembly

## Surface
Frontend only. The `/progress` route + `ProgressView.vue` composing the four sections; the Progress nav
item lit live (`AppSidebar` desktop + mobile tab); the time-in-zone stacked bars (+ "estimated" badge,
method breakdown) and the peaks `MetricTile` grid (the two sections not built in 15-3/15-4); their TS
types + service + store wiring; Vitest. **No charting library.**

## Why
The phase payoff: the Progress nav goes live and renders all four sections from seed data — PMC chart,
weekly load, time-in-zone (honestly "estimated"), and session-level peaks.

## Depends on
- **Task 15-3** (`PmcChartSection`), **15-4** (`LoadChartSection`), **15-2** (`/analytics/time-in-zone`),
  **15-1** (`/analytics/peaks`).
- **ADR-0007** §2 (peaks), §4 (time-in-zone "estimated"), §5 (range convention).

## Required reading
- `view-progress.jsx` (`%TEMP%\bryk-design\`) — the page layout: headline metric tiles
  (Fitness/Fatigue/Form/ACWR), the Weekly Load card, the PMC card, the records grid. **Port the records
  grid's look; ignore the design's Goal-Proximity/phase-timeline card (Phase 17/18) — out of scope here.**
- `ui/src/views/ZonesView.vue` + `ui/src/components/zones/ZoneSportCard.vue` — the sport toggle pattern and
  **the zone-colour convention `var(--chart-${min(zoneNumber,5)})`** (reuse it for the stacked bars).
- `ui/src/views/WorkoutsView.vue` / `WorkoutDetailView.vue` — `formatDuration`/`formatPace` helpers + the
  `AppShell` + `sportToPillKind` + `TypePill` patterns (extract the two formatters to a small shared
  `ui/src/lib/format.ts` if reused across sections — keep it surgical).
- `ui/src/components/common/MetricTile.vue` (props `label`/`value`/`unit`/`signed`/`delta`/`spark`/`#footer`),
  `DeltaChip.vue` (`dir` + slot), `TypePill.vue` + `pills.ts` (`sportToPillKind`).
- `ui/src/components/layout/AppSidebar.vue` (+ `__tests__/AppSidebar.spec.ts`) — flip Progress live.
- `ui/src/router/index.ts` — add the lazy `/progress` route.
- `ui/src/components/dashboard/FormCard.vue` — the live-tile + empty-state pattern.

## Acceptance criteria

### Types + service + store (`analytics.ts`)
- Types: `ZoneTime { zoneNumber: number; seconds: number }`,
  `ZoneTimeMethodBreakdown { structureSeconds: number; sessionAvgSeconds: number; unclassifiedSeconds: number }`,
  `TimeInZoneResponse { zones: ZoneTime[]; methodBreakdown: ZoneTimeMethodBreakdown; totalSeconds: number }`;
  `PeakKind` union (`'Load'|'Duration'|'Distance'|'Pace'|'Power'`),
  `PeakRecord { kind: PeakKind; sport: PlannedSport; value: number; achievedDate: string; achievedWorkoutId: string; isRecent: boolean; previousValue: number | null }`,
  `PeaksResponse { records: PeakRecord[] }`.
- Service: `getTimeInZone(from, to, sport?)`, `getPeaks(sport?)` (build the query string; `getPeaks` and
  `getTimeInZone` omit `sport` when undefined).
- Store: `timeInZone`/`peaks` refs + `loadTimeInZone(from,to,sport)` / `loadPeaks(sport)` actions +
  `loading`/`error`.

### `ProgressView.vue` (`ui/src/views/ProgressView.vue`)
- `AppShell title="Progress" subtitle="…"`. Reads `route.query` for `pmc` (`6w|3m|6m`, default `3m`),
  `weeks` (1–26, default `8`), `sport` (optional) — clamps/validates, falls back to defaults, and writes
  changes back via `router.replace` (ADR-0007 §5). Binds those to the section toggles' `v-model`.
- Composes, top-to-bottom:
  1. **Headline tiles** (reuse `progressPmc.current`): Fitness · CTL, Fatigue · ATL, Form · TSB (`signed`,
     Fresh/Neutral/Fatigued footer per ADR-0006 §8), ACWR (in/out of 0.8–1.3). All render "—" when
     `current == null` (fresh athlete). (Cheap, honest; reuses the 14-3 store.)
  2. **`LoadChartSection`** (15-4) — bound to `?weeks=`.
  3. **`PmcChartSection`** (15-3) — bound to `?pmc=`.
  4. **Time-in-zone** (below).
  5. **Peaks** (below).

### Time-in-zone section
- A sport toggle (reuse the ZonesView pattern) → `?sport=`; default range = the same window the `pmc`
  toggle implies (or a fixed sensible window, e.g. last 90 days — state it). Calls `loadTimeInZone`.
- **Stacked horizontal bar**: segments for zones 1..5 sized by `seconds / totalSeconds`, coloured
  `var(--chart-${min(zoneNumber,5)})`; a per-zone legend with formatted time (`h m`). The **unclassified**
  remainder (`totalSeconds − Σ zones`) is its own muted segment.
- An **"estimated" badge** always present (none of it is sample-derived until Phase 19), with a
  one-line provenance from `methodBreakdown` (e.g. "from planned structure · session HR · unclassified",
  optionally with the seconds split). **Honesty:** when `totalSeconds === 0` render "—" / an empty hint,
  not an empty coloured bar.

### Peaks section
- A `MetricTile` grid (reuse the design's records look). Per `PeakRecord`:
  - `label` = the kind ("Best Load", "Longest", "Longest Distance", "Fastest Pace", "Best Power"),
    `value`/`unit` formatted by kind: **Load** = round TSS; **Duration** = `h:mm:ss`; **Distance** =
    km (`/1000`, 2 dp) or m; **Pace** = `m:ss` + `/km`|`/100m`; **Power** = `W`.
  - `TypePill` for `record.sport`; a sub-line with `achievedDate`.
  - `DeltaChip` (dir `up`) **only when `isRecent && previousValue != null`**, showing the improvement
    (`value − previousValue`, or `previousValue − value` for Pace — lower is faster), formatted by kind.
  - Empty `records` → "No records yet — log workouts to set personal bests" (no fabricated tiles).

### Nav live
- `AppSidebar.vue`: give the Progress item `to: '/progress', routeName: 'progress'` (remove the inert
  "soon" branch for it); the mobile tab bar picks it up automatically (it filters on `item.to`).
- `router/index.ts`: add `{ path: '/progress', name: 'progress', component: () => import('@/views/ProgressView.vue') }`.

### Tests
- `ProgressView.spec.ts`: mounts (router + pinia testing, services mocked) → renders the four section
  headings; headline tiles show "—" when `current == null`; reads/writes the `pmc`/`weeks`/`sport` query.
- Time-in-zone: a stacked bar with the right segment widths for a sample response; "—" when `total === 0`;
  the "estimated" badge present.
- Peaks: a `DeltaChip` shows for a recent record with `previousValue`, hidden otherwise; pace formats as
  `m:ss/km`; empty-state copy when `records: []`.
- `AppSidebar.spec.ts` (update): Progress is now a link (not "soon"); Goals stays "soon".
- `pnpm run build` + `pnpm test` green (`--no-file-parallelism` if the transient crash appears).

## What NOT to modify
- **No charting library.** Reuse `Sparkline`, the 15-3/15-4 charts, `MetricTile`, `TypePill`, `DeltaChip`,
  eyebrow/card-surface utilities, `useCountUp`. Additive props on shared components only if truly needed
  (mirror Phase 14's `MetricTile.signed`), kept minimal.
- Don't build the Goal-Proximity / phase-timeline card (Phase 17/18) or per-sport PMC tabs / export.
- Time-in-zone stays coarse + "estimated" — no sample-derived zone time.
- Every rendered number traces to real workouts; empty/insufficient → "—", never a fabricated value.
- No auth code; the API scopes to the current athlete.

## Suggested commit
```
feat(ui): Progress page — time-in-zone + peaks + assembly, nav live

ProgressView at /progress composes the PMC and Load chart sections (15-3/4)
with a time-in-zone stacked bar (ZonesView colours, always "estimated" with
a method-breakdown provenance, "—" when empty) and a session-level peaks
MetricTile grid (TypePill + a DeltaChip for recent records with a real
previous-best improvement). Headline CTL/ATL/TSB/ACWR tiles reuse the pmc
current summary. Progress nav lit live (sidebar + mobile tab); ?pmc/?weeks/
?sport query convention. Vitest covers the sections + nav.
```
