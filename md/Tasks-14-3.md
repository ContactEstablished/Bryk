# Task 14-3 — Form (TSB) tile + ACWR chip (dashboard wiring)

## Surface
Frontend only. Light up the dashboard "Form (TSB)" placeholder with a live tile, add an ACWR chip to the
Weekly Load card, and add the analytics plumbing: a `services/analytics.ts` module, a Pinia analytics
slice (its own store, one domain concept), and `types/analytics.ts` mirrors of the 14-2 DTOs. One API
call (`GET /analytics/pmc`) feeds both surfaces.

## Why
Phase 14's user-visible payoff: a real, signed TSB that moves after logging/deleting a workout, and an
honest ACWR read on training-load risk. Charts wait for Phase 15 — this is the engine + two tiles only.

## Depends on
- **Task 14-2** — `GET /api/v1/analytics/pmc?from=&to=` returning `{ series, current }` with
  `current: { date, ctl, atl, tsb, acwr } | null`.
- **ADR-0006** §6 (`current` nullability), §8 (TSB bands).

## Required reading
- `ui/src/services/training.ts` — the service-module pattern (`apiFetch`, query-string building); copy it
  for `analytics.ts`.
- `ui/src/stores/training.ts` — the Pinia slice pattern (`ref` state, `loadX` action, `ApiError` capture).
- `ui/src/components/dashboard/WeeklyLoadCard.vue` — the card to extend with the ACWR chip; uses
  `MetricTile` + a `#footer` slot.
- `ui/src/components/dashboard/RestingHrCard.vue` — a live `MetricTile`-backed card with an empty-state
  affordance (the closest template for the Form tile's "—" / "Set in profile"-style fallback).
- `ui/src/components/common/MetricTile.vue` — props: `label`, `value`, `unit`, `delta {text,dir}`,
  `loading`, `placeholder`; renders "—" when `value == null`. `useCountUp` animates numeric values.
- `ui/src/components/common/DeltaChip.vue` — `dir: 'up'|'down'|'flat'` + slot text.
- `ui/src/views/HomeView.vue` — the `<PlaceholderCard title="Form (TSB)" … />` to replace (top stat row).
- `ui/src/types/training.ts` — the mirror-type style (string dates `YYYY-MM-DD`, nullable fields).

## Acceptance criteria

### Types (`ui/src/types/analytics.ts`)
Mirror the 14-2 DTOs (camelCase, dates as `'YYYY-MM-DD'`):
- `DailyLoadPoint { date: string; load: number }`.
- `PmcPoint { date: string; load: number; ctl: number; atl: number; tsb: number }`.
- `PmcSummary { date: string; ctl: number; atl: number; tsb: number; acwr: number | null }`.
- `PmcResponse { series: PmcPoint[]; current: PmcSummary | null }`.

### Service (`ui/src/services/analytics.ts`)
- `getPmc(from: string, to: string): Promise<PmcResponse>` → `apiFetch('/analytics/pmc?from=&to=')`,
  throwing on a null body like the other services.
- (Optional, for parity / Phase-15 readiness) `getDailyLoad(from, to): Promise<DailyLoadPoint[]>` —
  include only if cheap; not required by this task's UI.
- A small date helper for the dashboard's default range: `to = today`, `from = today − 90 days`
  (formatted `YYYY-MM-DD`, local). Wide enough that "7 days ago" is always in-series for the delta.

### Store (`ui/src/stores/analytics.ts`)
- `usePmcStore` (or `useAnalyticsStore`) Pinia setup store: `pmc = ref<PmcResponse | null>(null)`,
  `loading`, `error: ApiError | Error | null`, `loadPmc()` (computes the default 90-day range, calls the
  service, captures errors like `training.ts`).
- A getter/computed `current` = `pmc.value?.current ?? null`.
- A computed `tsbDeltaVs7d`: find `series` points at `to` and `to − 7d`; if both exist, the signed TSB
  delta → `{ text, dir }` for `MetricTile.delta` (dir `up` when fresher/higher TSB, `down` when lower,
  `flat` when ~0). If either point is missing, `null` (no chip — honest).

### Form (TSB) tile (new `ui/src/components/dashboard/FormCard.vue`)
- On mount, `loadPmc()` if not loaded.
- `MetricTile label="Form (TSB)"`:
  - `value` = `current.tsb` **signed** (show a leading `+` for positive; `MetricTile`/`useCountUp` render
    the number — pass a formatted string or extend the tile minimally if a `+` sign is needed; prefer
    formatting in the card and passing a string only if the count-up animation isn't required, else pass
    the number and add the sign via the `unit`/label — keep the change surgical).
  - `delta` = the store's `tsbDeltaVs7d` (vs 7 days ago).
  - **Empty state:** when `current == null` (fresh athlete) → render "—" (pass `value: null`); a small
    "Log a workout to see your form" hint is acceptable (mirror RestingHrCard's affordance), no fake number.
  - **Interpretation label** (ADR-0006 §8) in the `#footer` slot: `tsb > 10` → "Fresh"; `-10 ≤ tsb ≤ 10` →
    "Neutral"; `tsb < -10` → "Fatigued". Only when `current != null`.
- Replace the `<PlaceholderCard title="Form (TSB)" … />` in `HomeView.vue` with `<FormCard />`.

### ACWR chip on `WeeklyLoadCard.vue`
- Load `current` (reuse the analytics store — call its `loadPmc()` on mount alongside the existing
  `thisWeek`/`recentWorkouts` loads).
- In the `#footer` slot, render an ACWR chip:
  - `current.acwr == null` → show "ACWR —" in muted style (honest: < 28 days of history).
  - else `ACWR {acwr.toFixed(2)}`, styled **in-band** (good) when `0.8 ≤ acwr ≤ 1.3`, **out-of-band**
    (warning) otherwise. Reuse existing semantic colour utilities (`text-good` / `text-bad` /
    muted, as `DeltaChip` does) — no new colour tokens.
- Keep the existing "planned this week" footer text; the chip is additive.

### Tests (Vitest)
- `analytics` store spec: `loadPmc` populates `pmc`; `tsbDeltaVs7d` computes the signed delta when both
  series points exist and is `null` when the 7-days-ago point is absent; `current` getter passthrough.
  (Mock the service; `@pinia/testing` per existing store specs.)
- `FormCard.spec.ts`: renders the TSB value + interpretation label for a non-null `current`; renders "—"
  (no number, no label) when `current == null`. (Reduced-motion stub makes `useCountUp` synchronous —
  see existing `MetricTile`-backed specs.)
- `WeeklyLoadCard.spec.ts` (extend): ACWR chip shows the value + in-band style when `acwr` is in
  `[0.8,1.3]`, out-of-band style otherwise, and "—" when `acwr == null`.
- `pnpm run build` (vue-tsc) green; `pnpm test` green (re-run `--no-file-parallelism` once if the
  transient worker crash appears with all tests passing).

## What NOT to modify
- No charts, no sparkline beyond what `MetricTile` already does — Phase 15 owns PMC/Load charts.
- Don't touch the other placeholder cards (Sleep Avg stays a placeholder).
- Don't add a charting library.
- Don't fabricate a TSB/ACWR when data is insufficient — "—" is the honest render (ADR-0006 §6).
- Don't read athlete identity anywhere — the API scopes to the current athlete.
- Keep the `current`-summary contract: one `GET /analytics/pmc` call feeds both the tile and the chip
  (don't add a second endpoint call for ACWR).

## Suggested commit
```
feat(ui): live Form (TSB) tile + Weekly Load ACWR chip

Replaces the Form (TSB) placeholder with a live MetricTile (signed TSB,
DeltaChip vs 7 days ago, Fresh/Neutral/Fatigued band per ADR-0006) and
adds an ACWR chip to WeeklyLoadCard (in/out of the 0.8-1.3 band, "—" under
28 days of history). New analytics service + Pinia slice + types; one
GET /analytics/pmc call feeds both. Vitest covers the delta, band, and
fresh-athlete empty states.
```
