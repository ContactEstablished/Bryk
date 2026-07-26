# Task 20-4 — dashboard wiring: real Sleep tile, Resting HR trend, weight + HRV tiles

## Surface
Frontend (Vue) only. One new pure helper module (`ui/src/lib/wellness.ts`), three new dashboard cards
(`SleepCard.vue`, `WeightCard.vue`, `HrvCard.vue`), an upgrade to the existing
`ui/src/components/dashboard/RestingHrCard.vue`, and the dashboard composition itself —
**this task is the sole owner of `ui/src/views/HomeView.vue`**. Plus Vitest coverage for the helper and
each card. **No backend change, no new npm package, no new route or sidebar entry, and no edit to
`MetricTile.vue`, `Sparkline.vue` or `DeltaChip.vue`.**

## Why
This is the task the ROADMAP's Phase 20 success criteria are actually about: the Sleep tile stops being
a placeholder that reads *"Post-v1 — needs a device or health-app integration."* and starts showing a
real 7-day average with a sparkline of nightly hours, and Resting HR stops being the constant an athlete
typed once during onboarding and becomes a trend. Everything needed already exists — Task 20-2 returns
averages, deltas and a 14-day daily series in one call, and `MetricTile` already composes both
`Sparkline` and `DeltaChip` internally — so this task is composition plus two pieces of judgement that
have to be got right: **which metrics may use a `DeltaChip` at all** (ADR-0011 §5: only those where up
is good), and **what each tile shows for an athlete with no wellness history** (ADR-0011 §1: Resting HR
falls back to the profile value; nothing else fabricates a number).

## Depends on
- **Task 20-3** — `ui/src/types/wellness.ts`, `ui/src/services/wellness.ts`, `useWellnessStore`
  (`summary`, `today`, `loadingSummary`, `loadSummary()`), and `WellnessQuickEntryCard.vue`.
- **Task 20-2** — `GET /wellness/summary`'s shape, in particular `days` (sparse, ascending, 14 days),
  `hasAnyEntries`, and each metric's `{ average, priorAverage, delta, daysWithData }`.
- **ADR-0011 §1** — the read-only `Athlete.RestingHr` fallback, and *no* equivalent for weight.
- **ADR-0011 §5** — `delta` prop only where up is good; inverted metrics use `MetricTile`'s `#footer`.

## Required reading
- `ui/src/views/HomeView.vue` — all 111 lines. The `PlaceholderCard` import at **L8**; the onboarding
  gate (`onboarded`, L27–29) the whole dashboard hides behind; the top stat row at **L88–96**
  (`WeeklyLoadCard`, `RestingHrCard`, `PlaceholderCard title="Sleep Avg"` at **L91–94**, `FormCard`);
  the middle row at **L99–104**; the bottom row at **L107–109**.
- `ui/src/components/dashboard/PlaceholderCard.vue` — a 19-line wrapper that renders
  `<MetricTile :label="title" placeholder>`. `HomeView.vue` is its **only** importer today, so this task
  leaves it with none — see the non-goals: remove the orphaned import, keep the file.
- `ui/src/components/common/MetricTile.vue` — all 85 lines. Props `label`, `value?: string | number |
  null`, `unit?`, `signed?`, `delta?: { text: string; dir: 'up' | 'down' | 'flat' } | null`,
  `spark?: number[] | null`, `sparkAccent?`, `loading?`, `placeholder?`. A null `value` renders `—`
  (L63–66). The `#footer` slot is at **L78**. The sparkline renders only when
  `spark && spark.length >= 2` (**L80**). **No new tile component is needed.**
- `ui/src/components/common/Sparkline.vue:45` — `v-if="data.length >= 2"`. Fewer than two points renders
  nothing; the 0-entry and 1-entry athlete must be handled explicitly.
- `ui/src/components/common/DeltaChip.vue:8–12` — `up → text-good`, `down → text-bad`,
  `flat → text-muted-foreground`; and `ui/src/lib/weeklyTarget.ts:21–23` — the standing written
  instruction: *"DeltaChip reports the DIRECTION OF THE DELTA, and colours `up` green / `down` red. That
  is deliberate … Do not 'fix' the chip's colours."* Its four existing consumers are `MetricTile.vue:73`,
  `ThisWeekCard.vue:92`, `PeaksSection.vue:92` and `FormCard.vue:29`.
- `ui/src/components/dashboard/RestingHrCard.vue` — all 28 lines: reads
  `useProfileStore().recommended?.restingHr ?? null`, `:loading="!store.recommended"`, and the
  fetched-but-null `#footer` with a `router-link to="/profile"` reading `Set in profile`.
- `ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts` — all three existing tests and the
  `mountCard()` helper (`createTestingPinia` with `initialState: { profile: { recommended } }`,
  `stubs: { RouterLink: RouterLinkStub }`). **This task owns the file**; the three existing cases must
  keep passing, extended not rewritten.
- `ui/src/components/dashboard/WeeklyLoadCard.vue` — the data-loading card exemplar: `onMounted` guards
  each load with `if (!store.x) void store.loadX()` (L10–14), a computed `spark` array (L22–29),
  `:loading` passed to `MetricTile`, and a two-part `#footer` row (L40–48).
- `ui/src/components/dashboard/FormCard.vue:32–37` — the honest empty state: when there is nothing to
  show, the footer says what to do (`Log a workout to see your form`) instead of rendering a fake zero.
- `ui/src/stores/analytics.ts:117–130` — `tsbDeltaVs7d`, the existing `{ text, dir }` delta-builder the
  new helper mirrors (sign-prefixed text, `dir` from the sign, `null` when there is not enough history).
- `ui/src/lib/weeklyTarget.ts` + `ui/src/lib/__tests__/weeklyTarget.spec.ts` — the pure-helper-module
  pattern (no Vue imports, no `Date`, its own spec) the new `lib/wellness.ts` follows.
- `md/decisions/0011-wellness-metrics.md` §1 and §5.

## Acceptance criteria

### 1. `ui/src/lib/wellness.ts` (new — pure: no Vue, no `Date`, no store import)

```ts
import type { WellnessDailyPoint, WellnessMetricKey } from '@/types/wellness'

// The non-null values of `key` in day order — Sparkline's input. Callers pass the result straight to
// MetricTile, whose own `spark.length >= 2` guard (MetricTile.vue:80) handles the 0- and 1-entry
// athlete: fewer than two points renders no sparkline rather than a misleading flat line.
export function metricSeries(days: WellnessDailyPoint[], key: WellnessMetricKey): number[]

// ADR-0011 §5 — ONLY for metrics where up is good (sleep hours, HRV). Mirrors stores/analytics.ts's
// tsbDeltaVs7d: null when `delta` is null, otherwise a sign-prefixed label and the direction.
export function upIsGoodDelta(
  delta: number | null | undefined,
  digits?: number,           // default 1
): { text: string; dir: 'up' | 'down' | 'flat' } | null

// ADR-0011 §5 — for the inverted metrics (resting HR, weight, soreness), which must NEVER pass
// MetricTile's `delta` prop: a drop is good news and DeltaChip would render it red. Returns footer
// text plus its own colour class.
export function invertedChange(
  delta: number | null | undefined,
  unit: string,
  digits?: number,           // default 0
): { text: string; className: string } | null
```
- `metricSeries` returns `[]` (never `null`) when no day carries the metric.
- `upIsGoodDelta`: `dir = delta > 0 ? 'up' : delta < 0 ? 'down' : 'flat'`; `text` =
  `${delta > 0 ? '+' : ''}${delta.toFixed(digits)}`. Returns `null` for `null`/`undefined`.
- `invertedChange`: `className` is `'text-good'` when `delta < 0`, `'text-bad'` when `delta > 0`,
  `'text-muted-foreground'` when `0` — the inversion, in one place, with a comment explaining why it is
  not `DeltaChip`'s job. `text` = `${delta > 0 ? '+' : ''}${delta.toFixed(digits)} ${unit} vs prior 7d`.
  Returns `null` for `null`/`undefined`. Use a plain ASCII `-` (whatever `toFixed` emits) — do not
  substitute a typographic minus, because the specs assert on the string.
- A header comment naming ADR-0011 §5 and listing which metrics take which path, so the next person does
  not have to re-derive it.

### 2. `ui/src/components/dashboard/SleepCard.vue` (new) — replaces the placeholder

Thin store-reading wrapper around `MetricTile`, in the shape of `WeeklyLoadCard.vue`/`FormCard.vue`.
(These three new files are dashboard **cards**, not tile primitives — the rendering primitive stays
`MetricTile`, which already composes `Sparkline` and `DeltaChip`. Do not build a generic
"tile-with-sparkline" component.)

- `onMounted`: `if (!store.summary) void store.loadSummary()`.
- `label="Sleep Avg"`, `unit="h"`.
- `value` = `store.summary?.sleepHours.average ?? null`, rounded to **one** decimal for display
  (`Math.round(v * 10) / 10`) — the server rounds to 2, tiles show 1.
- `delta` = `upIsGoodDelta(store.summary?.sleepHours.delta, 1)` — sleep hours is one of the two metrics
  ADR-0011 §5 allows a `DeltaChip` for.
- `spark` = `metricSeries(store.summary?.days ?? [], 'sleepHours')`.
- `loading` = `store.loadingSummary && !store.summary`.
- `#footer`: when `daysWithData > 0`, `{n} night{s} logged`; when the summary is loaded and
  `daysWithData === 0`, the honest empty state `Log sleep to see your 7-day average` (the `FormCard`
  pattern). Never a fabricated zero.

### 3. `ui/src/components/dashboard/RestingHrCard.vue` (edit — this task owns the file)

Keeps its label, unit and profile fallback; gains history, a sparkline and a footer change.

- Reads **both** stores. `onMounted`: `if (!profile.recommended) void profile.loadRecommended()` (the
  existing line, unchanged) **and** `if (!wellness.summary) void wellness.loadSummary()`.
- `wellnessAvg` = `wellness.summary?.restingHr.average ?? null`, rounded to whole bpm for display.
- `value` = `wellnessAvg ?? profile.recommended?.restingHr ?? null` — **ADR-0011 §1's read-only
  fallback**: the tile prefers logged history and falls back to the onboarding value so it never
  regresses to `—` for an athlete who has not started logging. The card must not write anything back.
- `spark` = `metricSeries(wellness.summary?.days ?? [], 'restingHr')`.
- **No `delta` prop** — resting HR is inverted (ADR-0011 §5). The footer carries the change instead, in
  exactly these four states:

  | State | Footer |
  |---|---|
  | wellness average present, `delta` non-null | `invertedChange(delta, 'bpm', 0)`'s `text` in its `className` (e.g. `-2 bpm vs prior 7d` in `text-good`) |
  | wellness average present, `delta` null (no prior-week data) | `7-day average` in `text-muted-foreground` |
  | no wellness average, profile value present | `From profile · log RHR to see a trend` |
  | neither (fetched, both null) | the existing `router-link to="/profile"` reading `Set in profile` — **unchanged markup**, so the existing spec keeps passing |

- `loading` = `!profile.recommended && !wellness.summary` — still loading only while there is nothing at
  all to show (the existing spec's third case mounts with both null and must still see `Loading…`).

### 4. `ui/src/components/dashboard/WeightCard.vue` (new)

- `label="Weight"`, `unit="kg"`; `value` = `weightKg.average` rounded to one decimal.
- `spark` = `metricSeries(days, 'weightKg')`; `loading` as above.
- **No `delta` prop** (inverted). `#footer` = `invertedChange(weightKg.delta, 'kg', 1)`, falling back to
  `7-day average`, and to `Log weight to see a trend` when there is no average.
- **No fallback to `Athlete.WeightKg`** (ADR-0011 §1). The profile number is a one-off onboarding
  self-report and this is a trend tile; an athlete who has never logged sees `—` plus the prompt. Put
  that reasoning in a comment — it is the one place the asymmetry with Resting HR will look like an
  oversight.

### 5. `ui/src/components/dashboard/HrvCard.vue` (new)

- `label="HRV"`, `unit="ms"`; `value` = `hrvMs.average` rounded to a whole number.
- `delta` = `upIsGoodDelta(hrvMs.delta, 0)` — HRV is the second metric where up is good.
- `spark` = `metricSeries(days, 'hrvMs')`; `loading` as above.
- `#footer`: `{n} days logged` when `daysWithData > 0`, otherwise `Log HRV to see a trend`.

### 6. `ui/src/views/HomeView.vue` (edit — the only file this task shares with nobody)

- Imports: **remove** `PlaceholderCard` (L8 — an orphan created by this change); **add** `SleepCard`,
  `WeightCard`, `HrvCard` and `WellnessQuickEntryCard`
  (`@/components/wellness/WellnessQuickEntryCard.vue`).
- Top stat row (L88–96): replace the `<PlaceholderCard title="Sleep Avg" … />` block (L91–94) with
  `<SleepCard />`. The row keeps its four columns and its existing order:
  `WeeklyLoadCard`, `RestingHrCard`, `SleepCard`, `FormCard`.
- Insert **one** new row immediately after the top stat row and before the middle row (L99):
  ```html
  <!-- Wellness: today's entry plus the two metrics with no tile of their own -->
  <div class="stagger-in grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
    <div class="lg:col-span-2">
      <WellnessQuickEntryCard />
    </div>
    <WeightCard />
    <HrvCard />
  </div>
  ```
- **Nothing else changes**: the pre-onboarding hero block (L42–83), the `onboarded` gate, `formattedDate`,
  the middle row and the bottom row are untouched. No sport filter, no date range picker, no
  reordering of existing cards.
- Soreness gets **no dashboard tile** this phase. It is captured and displayed in
  `WellnessQuickEntryCard`'s collapsed summary (Task 20-3), which never renders a `DeltaChip`; if it ever
  earns a tile, ADR-0011 §5 puts its change in the footer like the other inverted metrics.

## Non-goals
- **Do not edit `ui/src/components/common/DeltaChip.vue`** — not its colours, not an `invert` prop, not
  a new variant. ADR-0011 §5 and `ui/src/lib/weeklyTarget.ts:21–23` both forbid it, and it has four
  existing consumers. If a tile seems to need red-for-up, use the footer. If that seems wrong —
  **STOP and ask**.
- **Do not edit `ui/src/components/common/MetricTile.vue` or `Sparkline.vue`.** `MetricTile` already
  exposes everything needed (`spark`, `delta`, `#footer`, `loading`, null → `—`). No new tile component,
  no `MetricTileWithSparkline` abstraction. If a tile appears to need a prop `MetricTile` lacks —
  **STOP and ask**.
- **Do not "fix" the < 2-point sparkline.** `Sparkline.vue:45` renders nothing below two points by
  design; a 1-entry athlete gets a value and no line. Do not pad the series, duplicate the point, or
  render a flat baseline.
- **Do not write anything back to the athlete profile.** `useProfileStore`, `services/profile.ts` and
  everything under `api/` are read-only here (ADR-0011 §1). The Resting HR fallback is a **read**.
- **Do not add a weight fallback to `Athlete.WeightKg`.** Deliberate asymmetry, documented in the card.
- **Do not delete `ui/src/components/dashboard/PlaceholderCard.vue`.** It becomes unused by this change;
  removing pre-existing files is out of scope (CLAUDE.md "surgical changes"). Remove only the import
  your change orphaned, and note the now-unused component in the commit body / handoff.
- **Do not edit any Task 20-3 file**: `ui/src/types/wellness.ts`, `services/wellness.ts`,
  `stores/wellness.ts`, `schemas/wellness.ts`, `components/common/ScaleSelector.vue`,
  `components/common/RpeSelector.vue`, `components/wellness/WellnessQuickEntryCard.vue` and their specs.
  If a tile needs something the store does not expose — **STOP and ask** rather than widening it
  unilaterally.
- **No backend change of any kind.** Nothing under `api/` may appear in `git diff`. If the dashboard
  wants a field `GET /wellness/summary` does not return — **STOP and ask**.
- **No new npm package** (**STOP and ask**) and **no migration** (**STOP and ask**).
- **No new route and no sidebar entry** — `ui/src/router/index.ts` and
  `ui/src/components/layout/AppSidebar.vue` must not appear in `git diff`. There is no `/wellness` page
  in Phase 20; a wellness history view is a later phase.
- **No HRV-into-TSB blending** and no readiness score — `FormCard.vue`, `stores/analytics.ts` and the
  PMC surfaces are untouched (ADR-0011 §3).
- **No `ExceptionHandlingMiddleware` change / ProblemDetails rework** — Phase 21 owns the error contract.
- **No auth code** — Phase 12 stays deferred and approval-gated.
- No device/health sync (Whoop/Oura/Apple Health), no hydration/nutrition/menstruation fields, no
  logging reminders or notifications.
- Do not write files owned by siblings: anything under `api/` (20-1, 20-2) or the 20-3 list above.
- **Do not fix** the two pre-existing nullable warnings in
  `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

### `ui/src/lib/__tests__/wellness.spec.ts` (new — pure, no mounting)
- `metricSeries returns only the non-null values, in day order` — three days with
  `sleepHours` `7`, `null`, `8` → `[7, 8]`.
- `metricSeries returns an empty array when no day carries the metric` → `[]`.
- `upIsGoodDelta labels a positive delta with a leading + and dir up` — `0.4` →
  `{ text: '+0.4', dir: 'up' }`.
- `upIsGoodDelta maps a negative delta to dir down` — `-0.4` → `{ text: '-0.4', dir: 'down' }`.
- `upIsGoodDelta maps zero to flat` — `0` → `{ text: '0.0', dir: 'flat' }`.
- `upIsGoodDelta returns null for a null delta`.
- `invertedChange colours a drop as good and a rise as bad` — `(-2, 'bpm', 0)` →
  `{ text: '-2 bpm vs prior 7d', className: 'text-good' }`; `(+2, 'bpm', 0)` → `'+2 bpm vs prior 7d'`
  with `'text-bad'`. **This is the ADR-0011 §5 guard**: if anyone later routes these through `DeltaChip`,
  the colours invert and this test is the tripwire.
- `invertedChange returns null for a null delta`.

### `ui/src/components/dashboard/__tests__/SleepCard.spec.ts` (new)
`createTestingPinia({ createSpy: () => () => {} })` with `initialState: { wellness: { summary } }`.
- `renders the 7-day average with the h unit` — average `7.46` renders `7.5` and `h`.
- `renders a DeltaChip for sleep hours` — `delta: 0.4` → the `DeltaChip` component exists and its text
  contains `+0.4` (sleep is an up-is-good metric).
- `renders a sparkline when at least two nights are logged` — `Sparkline` exists; and
  `renders no sparkline with a single night` — `Sparkline` does not exist (the
  `MetricTile.vue:80` / `Sparkline.vue:45` rule, asserted explicitly).
- `renders an em dash and the prompt when nothing is logged` — `hasAnyEntries: false` → text contains
  `—` and `Log sleep to see your 7-day average`.
- `shows the loading state before the summary arrives`.

### `ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts` (extend — this task owns it)
The three existing tests stay and must pass unchanged (`renders the bpm value when restingHr is set`,
`renders the empty state with a /profile link when restingHr is null`, `shows the loading state before
recommended is fetched`) — extend `mountCard()` to accept an optional wellness summary rather than
rewriting it. New cases:
- `prefers the wellness 7-day average over the profile value` — profile `55`, wellness average `48` →
  text contains `48` and not `55`.
- `falls back to the profile value when the athlete has no wellness entries` — `hasAnyEntries: false` →
  text contains `55` and the footer reads `From profile · log RHR to see a trend`.
- `renders the 7-day change in the footer and never as a DeltaChip` — wellness average `48`,
  `delta: -2` → `wrapper.findComponent(DeltaChip).exists()` is **false** and the text contains
  `-2 bpm vs prior 7d`. This is the inverted-metric guard.
- `renders a sparkline when at least two days carry a resting HR`.

### `ui/src/components/dashboard/__tests__/WeightCard.spec.ts` (new)
- `renders the 7-day average in kg`.
- `renders the change in the footer, not as a DeltaChip` — `findComponent(DeltaChip).exists()` false.
- `renders the prompt and no value when nothing is logged` — includes `Log weight to see a trend`, and
  the text does **not** contain the athlete's profile weight (assert the profile store's value is
  ignored — the deliberate asymmetry with Resting HR).

### `ui/src/components/dashboard/__tests__/HrvCard.spec.ts` (new)
- `renders the 7-day average in ms`.
- `renders a DeltaChip because up is good for HRV` — chip exists, text contains `+5`.
- `renders the prompt when nothing is logged`.

**No `HomeView.spec.ts`.** There is none today, the view is a pure composition behind an onboarding
gate, and the cards above carry the behaviour. The `HomeView` change is verified by the review checklist
(`git grep` for the placeholder) rather than by a new mounting harness — say so in the commit body
rather than adding one speculatively.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
Vitest must **rise** from wherever Task 20-3 left it (the **288 / 61 files** baseline plus 20-3's ~25)
by roughly 20 more across 5 files, with zero failures — including the three pre-existing
`RestingHrCard` cases, which must still pass **unmodified**, and the three pre-existing `RpeSelector`
cases. `pnpm run build` (`vue-tsc -b && vite build`) must stay green. xUnit must stay **exactly** where
Tasks 20-1 and 20-2 left it (the **343** baseline plus their additions) because this task touches no
backend file, and backend warnings must stay at **16** on a clean (`--no-incremental`) compile. If the
transient Vitest worker-fork crash appears with all tests passing, re-run once before debugging
(project memory).

## Review checklist
- [ ] `git grep -n "Sleep Avg" ui/src` no longer matches `PlaceholderCard`, and `git grep -n
      "PlaceholderCard" ui/src` matches only the component file itself (the import is gone, the file is
      not deleted).
- [ ] Sleep and HRV pass `MetricTile`'s `delta` prop; **resting HR, weight and soreness do not** — with
      a spec asserting `DeltaChip` is absent from the two inverted tiles.
- [ ] `DeltaChip.vue`, `MetricTile.vue` and `Sparkline.vue` are absent from `git diff --stat`.
- [ ] The Resting HR tile shows the wellness average when there is one, the profile value when there is
      not, and the existing `Set in profile` link when there is neither — all four footer states
      covered by specs.
- [ ] Weight does **not** fall back to `Athlete.WeightKg`, and the reason is in a comment.
- [ ] The 0- and 1-entry athlete render no sparkline and no fabricated number, asserted explicitly.
- [ ] `ui/src/lib/wellness.ts` is pure — no Vue import, no store import, no `Date` — and has its own
      spec.
- [ ] `git diff --stat` shows **nothing under `api/`**, no `router/index.ts`, no `AppSidebar.vue`, and no
      Task 20-3 file (`types/wellness.ts`, `services/wellness.ts`, `stores/wellness.ts`,
      `schemas/wellness.ts`, `ScaleSelector.vue`, `RpeSelector.vue`, `WellnessQuickEntryCard.vue`).
- [ ] No new npm package in `ui/package.json`.
- [ ] Every new SFC is `<script setup lang="ts">`; every store read goes through Pinia; no component
      calls `fetch` or a service directly.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
```
feat(ui): real Sleep tile, Resting HR trend and weight/HRV tiles

The dashboard's Sleep tile has read "Post-v1 - needs a device or health-app
integration" since Phase 14. Manual entry is the honest answer, so the
placeholder is replaced by a real tile: 7-day average nightly hours, a
sparkline of the logged nights, and a DeltaChip against the prior week. Resting
HR stops being the constant typed once during onboarding and becomes a trend
over logged entries, falling back to the profile value - read-only - when the
athlete has no wellness history, so a shipped tile never regresses to a dash
(ADR-0011 1). Weight and HRV join as MetricTile pairs, and the Today entry card
sits beside them; every one of them reads the same summary call, so saving an
entry refreshes the whole row without any event plumbing.

Which metrics may carry a DeltaChip is a decision, not a detail. The chip
colours up green and down red by documented convention (lib/weeklyTarget.ts),
and for resting HR, weight and soreness a drop is good news - so those tiles
pass no delta prop at all and render their 7-day change in MetricTile's footer
slot with their own colouring, while sleep hours and HRV use the chip
(ADR-0011 5). DeltaChip itself is untouched, and a spec on each inverted tile
asserts the chip is absent so nobody quietly re-routes it later.

Sparkline renders only at two or more points, which is exactly right for an
athlete one day into logging: they get a number and no line, never a padded
series or a flat baseline. The tile maths lives in a pure lib/wellness.ts with
its own spec, PlaceholderCard's import is removed from HomeView (the component
file stays, now unused), and no route or sidebar entry was added - the tiles
live on the dashboard.
```
