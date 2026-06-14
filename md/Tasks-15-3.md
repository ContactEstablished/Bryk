# Task 15-3 — Port `PMCChart` (CTL/ATL lines + daily-load bars) + range toggle

## Surface
Frontend only. A hand-rolled SVG `PMCChart.vue` ported from the design export's `charts.jsx` `PMCChart`;
a **pure** geometry transform (Vitest-covered); a reusable `ChartRangeToggle.vue`; and analytics
store/service additions to load the PMC over a `6w/3m/6m` range. **No charting library.**

## Why
The Progress page's Performance-Manager chart. It reuses the Phase-14 `/analytics/pmc` endpoint — whose
`series` already carries per-day `load` **and** `ctl`/`atl` — so one call feeds the bars *and* the lines
(no redundant `/daily-load` call; the daily load *is* `series[i].load`).

## Depends on
- **ADR-0007** §5 (range convention `?pmc=6w|3m|6m`).
- **Task 14-2/14-3** — `/analytics/pmc` + `types/analytics.ts` (`PmcResponse`, `PmcPoint`) + `services/analytics.ts`.

## Required reading
- The design reference: `charts.jsx` `PMCChart` (extracted at
  `%TEMP%\bryk-design\charts.jsx`, from `~/Downloads/Bryk UI.zip`) and `view-progress.jsx` (the
  "Performance Manager" card + legend). Port the geometry; swap oklch literals for CSS vars.
- `ui/src/components/common/Sparkline.vue` — **the established hand-rolled-SVG port pattern**: `computed`
  point math, `viewBox`, CSS-var stroke/gradient, `pathLength`/`vector-effect`. Mirror its structure.
- `ui/src/services/analytics.ts` — `getPmc`, `isoDate`, `defaultPmcRange`; add the range mapper here.
- `ui/src/stores/analytics.ts` — the slice to extend (keep the dashboard's `loadPmc`/`current` intact).
- `ui/src/style.css` — the palette: `--bryk-accent`/`-hi`/`-lo` (CTL/accent), `--bryk-warn` (ATL/fatigue),
  `--bryk-fg-2`/`-3` + border tokens (axis/gridlines/daily-load bars).

## Acceptance criteria

### Pure transform (`ui/src/lib/charts/pmc.ts`)
- `buildPmcGeometry(series: PmcPoint[], dims?): PmcGeometry` — no DOM, no Vue. Given the contiguous
  zero-filled series, return everything the SVG needs:
  - `ctlPath`, `ctlFillPath`, `atlPath` (path `d` strings), `loadBars` (`{x, y0, y1}` per day),
    `yTicks` (`{value, y}` at 0 / 0.5·max / max), `endMarkers` (`{ctl:{x,y}, atl:{x,y}}`), and the
    `x(i)`/`y(v)` scales' outputs baked in. `maxV = max(ctl, atl) × 1.15` (per the design); guard an
    empty/all-zero series (no NaN — return empty paths / `maxV` fallback 1).
- **Vitest** (`ui/src/lib/charts/__tests__/pmc.spec.ts`): a known small series → exact first/last point
  coordinates, monotone-x, correct `maxV`, and a graceful empty-series result (no `NaN` in any path).

### `PMCChart.vue` (`ui/src/components/charts/PMCChart.vue`)
- Presentational. `defineProps<{ series: PmcPoint[] }>()`. Renders the SVG (viewBox `0 0 720 220`,
  `preserveAspectRatio="xMidYMid meet"`, `width="100%"`) from `buildPmcGeometry`:
  daily-load tick bars (muted), CTL area-fill + line (accent), ATL dashed line (warn), baseline axis +
  y-tick labels, "Nw ago"/"today" x-labels, CTL/ATL end-marker dots. Colours via CSS vars (Sparkline
  precedent) — **no oklch literals, no hard-coded hex** beyond what maps to a token.
- `aria-hidden` on the SVG; the numeric legend lives in the section (below) for accessibility.
- Renders nothing meaningful (or a muted "—"/empty hint) when `series.length < 2`.

### `ChartRangeToggle.vue` (`ui/src/components/charts/ChartRangeToggle.vue`)
- Small reusable segmented control. `defineProps<{ modelValue: string; options: {value:string; label:string}[] }>()`,
  `defineEmits<{ 'update:modelValue': [string] }>()`. Styled like the `ZonesView` sport toggle (rounded
  segments, active = accent). Used by both PMC (here) and Load (15-4).

### Service + store
- `services/analytics.ts`: `pmcRangeToDates(key: '6w'|'3m'|'6m', now?: Date): {from,to}` — `to = today`,
  `from = today − {42|90|180} days` (use `isoDate`). Export a `PMC_RANGES` option list for the toggle.
- `stores/analytics.ts`: add **progress-scoped** state without disturbing the dashboard's `pmc`/`current`:
  `progressPmc = ref<PmcResponse | null>(null)`, `progressPmcRange = ref<'6w'|'3m'|'6m'>('3m')`, a
  `loadProgressPmc(range)` action (maps range → dates → `getPmc`, stores both), plus `loading`/`error`.
  A computed `pmcSeries = progressPmc.value?.series ?? []` for the chart.

### Section wiring (consumed by 15-5; build it self-contained here)
- A `PmcChartSection.vue` (`ui/src/components/charts/`) that composes `ChartRangeToggle` (options
  `PMC_RANGES`) + `PMCChart` + the numeric legend (Fitness CTL / Fatigue ATL / Daily load, values from
  `progressPmc.current`), calls `loadProgressPmc` on mount and on toggle. The `?pmc=` URL sync is wired in
  15-5 (where the `/progress` route lives) — here, drive it from the store ref; expose the range via
  `v-model` so 15-5 can bind it to the query.

### Tests
- The `pmc.spec.ts` transform tests above.
- `PMCChart.spec.ts`: mounts with a sample series → renders the CTL + ATL `<path>`s and the right number of
  daily-load bars; renders the empty state for `< 2` points.
- (Section test optional; the transform + component tests are the required Vitest coverage.)
- `pnpm run build` (vue-tsc) green; `pnpm test` green (`--no-file-parallelism` if the transient worker
  crash appears with all tests passing).

## What NOT to modify
- **No charting library** (success criterion: none in `package.json`). Hand-rolled SVG only.
- Don't disturb the dashboard `FormCard`/`WeeklyLoadCard` or the store's `loadPmc`/`current`/`tsbDeltaVs7d`.
- Don't add a separate `/daily-load` call for this chart — `pmc.series[i].load` is the daily load.
- Don't read athlete identity; the API scopes to the current athlete.

## Suggested commit
```
feat(ui): port PMCChart (CTL/ATL lines + daily-load bars) + range toggle

Hand-rolled SVG PMCChart (Sparkline port pattern, no chart lib) driven by a
pure buildPmcGeometry transform (Vitest), fed by one /analytics/pmc call
whose series carries load + ctl + atl. Reusable ChartRangeToggle with
6w/3m/6m presets; analytics store gains progress-scoped pmc state. Colours
via CSS vars.
```
