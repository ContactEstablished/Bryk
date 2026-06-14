# Task 15-4 — Port `LoadChart` (planned hatch vs actual + optimal band + trend)

## Surface
Frontend only. A hand-rolled SVG `LoadChart.vue` ported from `charts.jsx` `LoadChart`; a **pure**
geometry transform (Vitest); the `WeeklyLoad*` TS mirror types; `getWeeklyLoad` service + store wiring;
a weeks-span toggle. **No charting library.** Reuses `ChartRangeToggle.vue` from 15-3.

## Why
The Progress page's headline "Weekly Training Load" chart: planned (hatched) vs actual (filled) bars, the
optimal band, and the 4-week trend — all from `/analytics/weekly-load` (15-1), which already returns the
per-week `rollingAverage` and the band, so the component **plots** them rather than recomputing.

## Depends on
- **Task 15-1** — `GET /api/v1/analytics/weekly-load?weeks=N` → `{ weeks:[{weekStart,plannedLoad,actualLoad,rollingAverage}], optimalBand:{lower,upper}|null }`.
- **Task 15-3** — `ChartRangeToggle.vue`.
- **ADR-0007** §1 (band), §3 (weekly shape), §5 (range convention `?weeks=`).

## Required reading
- `charts.jsx` `LoadChart` + `view-progress.jsx` "Weekly Training Load" card/legend (at
  `%TEMP%\bryk-design\`). Port the geometry; swap oklch literals for CSS vars; **use the data's
  `rollingAverage`** for the trend line instead of recomputing it in JS, and the data's `optimalBand`
  instead of the hard-coded 380–500.
- `ui/src/components/common/Sparkline.vue` — the port pattern. `ui/src/components/charts/PMCChart.vue` (15-3)
  — the sibling chart to match (viewBox, CSS-var colours, empty guards).
- `ui/src/services/analytics.ts` / `stores/analytics.ts` — extend (don't disturb 14-3 / 15-3 state).
- `ui/src/style.css` — `--bryk-accent*` (actual bars), `--bryk-warn` (trend), `--bryk-good` (optimal band,
  low-opacity), border/`--bryk-fg-3` (planned hatch, gridlines, labels).

## Acceptance criteria

### Types (`ui/src/types/analytics.ts`)
- `WeeklyLoadWeek { weekStart: string; plannedLoad: number; actualLoad: number; rollingAverage: number }`.
- `OptimalBand { lower: number; upper: number }`.
- `WeeklyLoadResponse { weeks: WeeklyLoadWeek[]; optimalBand: OptimalBand | null }`.

### Service + store
- `services/analytics.ts`: `getWeeklyLoad(weeks: number): Promise<WeeklyLoadResponse>` →
  `apiFetch('/analytics/weekly-load?weeks=N')` (throw on null body, like `getPmc`). Export a
  `WEEKLY_LOAD_RANGES` preset list for the toggle (e.g. `8` / `12` / `26` weeks).
- `stores/analytics.ts`: `weeklyLoad = ref<WeeklyLoadResponse | null>(null)`,
  `weeklyLoadWeeks = ref<number>(8)`, `loadWeeklyLoad(weeks)` action, `loading`/`error`.

### Pure transform (`ui/src/lib/charts/load.ts`)
- `buildLoadGeometry(weeks: WeeklyLoadWeek[], band: OptimalBand | null, dims?): LoadGeometry` — no DOM/Vue.
  Returns: per-week `{ plannedRect, actualRect, valueLabel{x,y,text}, weekLabel{x,y,text,isCurrent} }`
  (last week flagged current/"· NOW"), the `trendPath` (over `rollingAverage`) + `trendDots`, `yTicks`
  (0/0.25/0.5/0.75/1 × max), and `bandRect` (`{x,y,width,height}`) or null. `maxV = max(actual, planned,
  band.upper) × 1.1`; guard empty/all-zero (no `NaN`).
- **Vitest** (`ui/src/lib/charts/__tests__/load.spec.ts`): known weeks → planned & actual rect heights in
  the right ratio; the current-week flag on the last bar; band rect present/absent for band/null;
  `trendPath` follows `rollingAverage`; empty input → no `NaN`.

### `LoadChart.vue` (`ui/src/components/charts/LoadChart.vue`)
- `defineProps<{ weeks: WeeklyLoadWeek[]; optimalBand: OptimalBand | null }>()`. SVG (viewBox `0 0 720 280`,
  `width="100%"`) from `buildLoadGeometry`: gridlines + y-tick labels, the optimal-band rect (green, dashed,
  low-opacity, labelled "OPTIMAL BAND") when present, **planned hatched bars behind** + **actual filled
  bars** (current week emphasised), value + week labels, the dashed 4-week trend line + dots. CSS-var
  colours only. `aria-hidden`; the legend lives in the section.
- **Planned hatch vs actual fill must be visually distinguishable** (a `<pattern>` hatch for planned, a
  gradient/solid fill for actual — per the design). Empty/all-zero → muted empty state.

### Section wiring (consumed by 15-5)
- `LoadChartSection.vue` (`ui/src/components/charts/`): `ChartRangeToggle` (options `WEEKLY_LOAD_RANGES`) +
  `LoadChart` + the legend (Actual / Planned / 4-wk avg). Calls `loadWeeklyLoad` on mount + toggle; expose
  the weeks value via `v-model` for 15-5 to bind to `?weeks=`.

### Tests
- The `load.spec.ts` transform tests.
- `LoadChart.spec.ts`: renders N planned + N actual `<rect>`s, the band rect when `optimalBand` is set and
  none when null, and the trend `<path>`; empty state for `weeks = []`.
- `pnpm run build` + `pnpm test` green.

## What NOT to modify
- **No charting library.** Hand-rolled SVG only.
- Don't recompute the rolling average or the band in the component — plot the server's `rollingAverage` /
  `optimalBand` (single source of truth; the band is the Phase-18 contract).
- Don't disturb 14-3 / 15-3 store state or the dashboard cards.
- Don't fabricate a band when the server returns `optimalBand: null` (fresh athlete) — draw no band.

## Suggested commit
```
feat(ui): port LoadChart (planned hatch vs actual, optimal band, trend)

Hand-rolled SVG LoadChart (no chart lib) from a pure buildLoadGeometry
transform (Vitest), fed by /analytics/weekly-load. Plots the server's
rollingAverage trend and [0.8,1.3]×trailing optimal band (no recompute);
planned hatched bars behind actual filled bars, current week emphasised.
Reuses ChartRangeToggle (8/12/26-week presets). Colours via CSS vars.
```
