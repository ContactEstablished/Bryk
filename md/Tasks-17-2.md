# Task 17-2 — Port `ProgressRing` + refactor `PrimaryGoalCard` to share it

## Surface
Frontend only. A new presentational `ProgressRing.vue` ported from the Claude Design export (ticks +
gradient + draw-in), following the established `Sparkline.vue` hand-rolled-SVG porting pattern; a pure
ring-geometry helper (Vitest-covered); and a refactor of the dashboard `PrimaryGoalCard.vue` to render
its countdown **through** the shared ring (one implementation, two surfaces). **No charting library, no
new package.**

## Why
Both the dashboard Primary-Goal card and the Phase-17 Goals page render an event countdown; the ROADMAP
requires one ring implementation shared across both surfaces so they render identically. Porting the
ring now (17-2), before `GoalsView` (17-3) consumes it, keeps the visual contract in one place and lets
the dashboard card prove the shared component before the new page depends on it. The fill fraction
(elapsed portion of the creation→target window, or plan-start→target when a plan is linked) is the
honest date-based progress the deferred quantitative tracking would otherwise have shown.

## Depends on
- **Task 17-1** — `GET /events` linked-plan ids (the ring's fill anchor uses plan start when linked;
  17-3 supplies plan-start via the linked plan, so this task's ring takes a *fill fraction* prop and
  does not itself fetch — see below).
- **Phase 8 dashboard** — `PrimaryGoalCard.vue`, `useCountUp`, `useProfileStore.primaryEvent`.
- **Task 15-3 precedent** — `Sparkline.vue` as the canonical hand-rolled-SVG port pattern (computed
  point/arc math, `viewBox`, CSS-var stroke/gradient, `pathLength` draw-in, `vector-effect`).

## Required reading
- The design reference: the `ProgressRing` component in the Claude Design export (extracted alongside
  `charts.jsx` at `%TEMP%\bryk-design\` — same export Task 15-3 ported `PMCChart` from; grep the export
  for `Ring`/`stroke-dasharray`/`circle`). Port the **geometry and the draw-in animation**; swap any
  `oklch(...)` literals for the CSS vars in `ui/src/style.css`. **If the export is not present locally,
  STOP and ask for the design zip** rather than inventing a ring look — but the geometry contract below
  is authoritative regardless.
- `ui/src/components/common/Sparkline.vue` — **the port pattern to mirror**: `computed` path math,
  per-instance `useId()` gradient, `viewBox`, `pathLength="1"` + a CSS `stroke-dashoffset` draw-in,
  `vector-effect="non-scaling-stroke"`, `aria-hidden` on the decorative SVG.
- `ui/src/components/dashboard/PrimaryGoalCard.vue` — the card to refactor: its `daysUntil`,
  `formattedDate`, `weeks`, `useCountUp(weeks)` internals and the race-day/eve headline branch. The
  countdown number moves into the ring's center; the surrounding card copy stays.
- `ui/src/composables/useCountUp.ts` — reused for the center count-up; note it snaps under
  `prefers-reduced-motion` (the test stub reports `reduce`, keeping assertions synchronous).
- `ui/src/components/common/TypePill.vue` + `ui/src/components/common/pills.ts` — the pill precedent for
  any sport/priority chip the card renders (not the ring itself).
- `ui/src/style.css` — palette tokens: `--bryk-accent`/`-hi`/`-lo` (ring gradient), `--bryk-fg-*` and
  border tokens (ticks/track), `--bryk-accent-glow`.

## Acceptance criteria

### Pure geometry helper (`ui/src/lib/progressRing.ts`)
- `buildRingGeometry(fraction: number, opts?: { size?: number; stroke?: number; ticks?: number }): RingGeometry`
  — no DOM, no Vue. Given a fill `fraction` in `[0, 1]`, return everything the SVG needs:
  - `size`, `radius`, `cx`, `cy`, `circumference` (`2πr`),
  - `dashArray` / `dashOffset` for the progress arc (offset = `circumference × (1 - clampedFraction)`),
  - `ticks: { x1, y1, x2, y2 }[]` — evenly spaced tick marks around the track (default `ticks = 60`).
  - **Clamp** `fraction` to `[0, 1]` (an overdue event → `1`; a not-yet-started window → `0`); guard
    `NaN`/`Infinity` → treat as `0`. Defaults: `size = 160`, `stroke = 8`, `ticks = 60`.
- **Vitest** (`ui/src/lib/__tests__/progressRing.spec.ts`): `fraction = 0` → `dashOffset === circumference`;
  `fraction = 1` → `dashOffset === 0`; `fraction = 0.5` → `dashOffset === circumference / 2`;
  `fraction = 1.4` clamps to `1`; `fraction = NaN` → `0`; `ticks` length matches the option; no `NaN` in
  any returned number.

### `ProgressRing.vue` (`ui/src/components/common/ProgressRing.vue`)
- Presentational only. Props:
  `defineProps<{ fraction: number; centerValue?: string | number; centerLabel?: string; size?: number; animate?: boolean }>()`
  (`animate` defaults `true`). **No data fetching** — the parent computes `fraction` and passes the
  center content.
- Renders the SVG from `buildRingGeometry(fraction, { size })`: a muted full-circle **track**, the tick
  marks, and the **progress arc** (accent gradient via a per-instance `useId()` `linearGradient`, mirroring
  Sparkline). Arc uses `pathLength`-style `stroke-dasharray`/`stroke-dashoffset`; when `animate`, a CSS
  transition draws the offset in (respect `prefers-reduced-motion` — snap, no transition). `aria-hidden`
  on the SVG.
- A center slot: default renders `centerValue` (large, gradient text like the current card's week number)
  above `centerLabel` (eyebrow). Expose a `#center` slot so `PrimaryGoalCard` / `GoalsView` can override
  (e.g. the race-day "Today"/"Tomorrow" headline).
- No hard-coded hex/oklch beyond what maps to a CSS-var token.

### `PrimaryGoalCard.vue` refactor (shared internals, identical render)
- Replace the inline week-number block with `<ProgressRing>`:
  - `centerValue = animatedWeeks` (keep `useCountUp(weeks)`), `centerLabel = "weeks to go"`, plus the
    "{days} days" sub-line (via the `#center` slot or the existing markup moved into the slot).
  - Compute `fraction` from the elapsed portion of the countdown window. For the dashboard card the
    window is **event creation → target**; since the dashboard `primaryEvent` (an `EventResponse`) has no
    creation date, use a **created-window fallback**: fraction = elapsed of `[today − <lookback>, eventDate]`
    is *not* meaningful without a start — instead derive fraction from days-to-go against a **rolling
    horizon**: `fraction = clamp(1 - daysUntil / HORIZON_DAYS, 0, 1)` with `HORIZON_DAYS = 168` (24
    weeks). Document this as the dashboard's honest approximation; 17-3's GoalsView, which *does* have the
    linked plan's `startDate`, passes the true `[start, target]` elapsed fraction.
  - Preserve the race-day/eve branch (`days <= 1` → "Today"/"Tomorrow" headline) via the ring's `#center`
    slot; preserve the empty ("No upcoming events" → "Set a goal") and loading states unchanged.
- The card's outer copy (`Primary Goal` eyebrow, event name, sport · date line) stays exactly as-is.
- **Behavioral parity:** the rendered week number, race-day headline, and empty/loading states must match
  the pre-refactor card — the refactor is internal (extract the ring), not a redesign.

### Tests
- `ui/src/lib/__tests__/progressRing.spec.ts` — the geometry cases above.
- `ui/src/components/common/__tests__/ProgressRing.spec.ts` — mounts with `fraction = 0.5` → renders the
  arc `<circle>`/`<path>` with the expected `stroke-dashoffset`; renders `centerValue`/`centerLabel`;
  `#center` slot override renders instead of the default.
- `ui/src/components/dashboard/__tests__/PrimaryGoalCard.spec.ts` — extend/add: with a seeded
  `primaryEvent` the card renders a `ProgressRing` and the week count (reduced-motion snaps `useCountUp`);
  race-day event (`daysUntil <= 0`) renders "Today"; no upcoming event renders the "Set a goal" link.
- `pnpm run build` (vue-tsc) green; `pnpm test` green (`pnpm exec vitest run --no-file-parallelism` if the
  known transient worker crash appears with all tests passing).

## What NOT to modify
- **No charting/animation library, no new package** — hand-rolled SVG + CSS transition + `useCountUp` only
  (success criterion: `package.json` unchanged).
- **Do not** make `ProgressRing` fetch data or know about events/goals — it takes `fraction` + center
  content. All domain logic (which window, which fraction) lives in the consuming card/view.
- **Do not** change `PrimaryGoalCard`'s outer copy, the profile store, `primaryEvent`, or the empty/loading
  behavior — the refactor extracts the countdown into the ring and must render identically.
- **Do not** couple the ring to a specific size/color at the call site beyond the `size` prop + CSS vars.
- **Do not** import the design export's oklch literals verbatim — map them to the existing CSS-var tokens.

## Suggested commit
```
feat(ui): port ProgressRing, share it with PrimaryGoalCard

Hand-rolled SVG ProgressRing (Sparkline port pattern: computed arc/tick
geometry, per-instance gradient, pathLength draw-in, reduced-motion snap)
driven by a pure buildRingGeometry transform (Vitest pins dashOffset at
0/0.5/1, clamps overshoot, guards NaN). Refactor the dashboard
PrimaryGoalCard to render its countdown through the ring — one
implementation, two surfaces — with the week count in the ring center via
useCountUp and the race-day headline through a #center slot. Dashboard
fill uses a rolling-horizon fraction (the EventResponse carries no start);
17-3's GoalsView passes the true linked-plan [start, target] window. No
chart lib, no new package; card render parity preserved.
```
