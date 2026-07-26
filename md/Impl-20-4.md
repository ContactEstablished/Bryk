# Impl 20-4 — Build order: real Sleep tile, Resting HR trend, weight + HRV tiles

**Executor:** the architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-20-4.md`.
**Decision lock:** ADR-0011 §1 (the **read-only** `Athlete.RestingHr` fallback, and deliberately **no**
equivalent for weight) + ADR-0011 §5 (`MetricTile`'s `delta` prop only where up is good — sleep hours and
HRV; the inverted metrics render their change in the `#footer` slot, and `DeltaChip.vue` is not touched).
Both are restated in the ROADMAP's Phase 20 entry; ADR-0011 itself is Task 20-1's first deliverable and is
referenced here without a code dependency on it (the same pattern Impl-18-4 used for ADR-0009).
**Scope:** Frontend only. One new pure helper module, three new dashboard cards, one card rewrite, one
view edit, five spec files. **No backend change, no migration, no new npm package, no new route, no
sidebar entry.** This is Phase 20's final task, so it closes with a phase-closeout prompt (Step 13).

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. One commit at the end with the message in `Tasks-20-4.md`, then a *separate* docs commit for the
phase closeout.

---

## Step 0 — Pre-flight

### 0a. Tree and baselines

- `git status` clean on `main`. Do not revert, stash, or commit unrelated working-tree changes.
- `dotnet build api/Bryk.sln` green. `dotnet test api/Bryk.sln` — **record the count**. It is the phase's
  **343** baseline plus whatever Tasks 20-1 and 20-2 added. This task touches **no** backend file, so at
  Step 10 that number must be **identical**, not merely green. Clean-compile warnings stay at **16**
  (`--no-incremental`; an incremental build reports 14 because it skips `Bryk.API.Tests`). Fourteen of the
  sixteen are the design-time `System.Security.Cryptography.Xml` NU1903 advisory; the other two are the
  pre-existing nullable warnings at `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121` (CS8604)
  and `:150` (CS8602) — **do not fix them.**
- `cd ui; pnpm run build` green (`vue-tsc -b && vite build`).
- `cd ui; pnpm exec vitest run --no-file-parallelism` — **record files/tests**. The Phase-20 baseline is
  **288 tests / 61 files**; Task 20-3 added ~25 across 4 new files. Note the *actual* current numbers here.
  Step 10 must show ~**24 more tests across 4 more files** (`wellness.spec.ts`, `SleepCard.spec.ts`,
  `WeightCard.spec.ts`, `HrvCard.spec.ts` are new; `RestingHrCard.spec.ts` is extended in place), zero
  failures. If the known transient Vitest worker-fork crash appears **with every test reporting passed**,
  re-run once before debugging (project memory: `vitest-worker-crash-transient`).

### 0b. Confirm Task 20-3 actually landed — this task will not compile against a stub

Read each of these and confirm the exact shapes. If any is missing or differently shaped, **STOP**: 20-3
is not done, and reimplementing any of it here is out of scope (`Tasks-20-4.md` non-goals).

- `ui/src/types/wellness.ts` exports `WellnessMetricKey` (the six-key union), `WellnessDailyPoint`
  (`date` + the six `number | null` metrics), `WellnessMetricSummary`
  (`{ average, priorAverage, delta, daysWithData }`) and `WellnessSummaryResponse`
  (`to`, `from`, `priorFrom`, the six named `WellnessMetricSummary`s, `days: WellnessDailyPoint[]`,
  `hasAnyEntries: boolean`).
- `ui/src/stores/wellness.ts` exports `useWellnessStore` with `summary`, `today`, `loadingSummary`,
  `saving` and `loadSummary()`. **If a tile appears to need something the store does not expose —
  STOP and ask** rather than widening 20-3's file.
- `ui/src/components/wellness/WellnessQuickEntryCard.vue` exists (Step 9 mounts it).
- `GET /api/v1/wellness/summary` is live (20-2) — Step 11's runtime gate calls it through the dev proxy.

### 0c. Open in the editor

**Edit targets (7 files, 4 of them new):**
`ui/src/lib/wellness.ts` (new), `ui/src/lib/__tests__/wellness.spec.ts` (new),
`ui/src/components/dashboard/SleepCard.vue` (new),
`ui/src/components/dashboard/__tests__/SleepCard.spec.ts` (new),
`ui/src/components/dashboard/WeightCard.vue` (new),
`ui/src/components/dashboard/__tests__/WeightCard.spec.ts` (new),
`ui/src/components/dashboard/HrvCard.vue` (new),
`ui/src/components/dashboard/__tests__/HrvCard.spec.ts` (new),
`ui/src/components/dashboard/RestingHrCard.vue` (rewrite),
`ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts` (extend),
`ui/src/views/HomeView.vue` (edit).

**Read-only, for the patterns and the fences:**
`ui/src/components/common/MetricTile.vue` (all 85 lines), `Sparkline.vue:45`, `DeltaChip.vue:8–12`,
`ui/src/composables/useCountUp.ts`, `ui/src/lib/weeklyTarget.ts` + its spec,
`ui/src/components/dashboard/WeeklyLoadCard.vue`, `FormCard.vue` + `FormCard.spec.ts`,
`ui/src/components/dashboard/PlaceholderCard.vue`, `ui/src/stores/analytics.ts:117–130`,
`ui/src/stores/profile.ts:87–97`, `ui/src/types/profile.ts:12–27`.

### 0d. Fences to hold for the whole task (re-checked at Step 10's `git diff --stat`)

- **`ui/src/components/common/DeltaChip.vue` is not edited** — not its colours, not an `invert` prop, not
  a variant. `ui/src/lib/weeklyTarget.ts:21–23` carries the standing written instruction:

  > *"DeltaChip reports the DIRECTION OF THE DELTA, and colours `up` green / `down` red. That is
  > deliberate and separate from `state`: the bar carries the honest band colour, so an over-target week
  > shows a green "up" chip beside a warn-coloured bar. **Do not "fix" the chip's colours.**"*

  It has four consumers today (`MetricTile.vue:73`, `ThisWeekCard.vue:92`, `PeaksSection.vue:92`,
  `FormCard.vue:29`). If a tile seems to need red-for-up — use the footer. If *that* seems wrong,
  **STOP and ask.**
- **`ui/src/components/common/MetricTile.vue` and `Sparkline.vue` are not edited.** No new tile
  component, no `MetricTileWithSparkline`. If a tile appears to need a prop `MetricTile` lacks —
  **STOP and ask.**
- **Nothing under `api/`** appears in the diff. If the dashboard wants a field
  `GET /wellness/summary` does not return — **STOP and ask**; do not add it.
- **No Task 20-3 file** appears in the diff: `types/wellness.ts`, `services/wellness.ts`,
  `stores/wellness.ts`, `schemas/wellness.ts`, `components/common/ScaleSelector.vue`, `RpeSelector.vue`,
  `components/wellness/WellnessQuickEntryCard.vue`, or any of their specs.
- **No `ui/src/router/index.ts`, no `ui/src/components/layout/AppSidebar.vue`.** There is no `/wellness`
  page in Phase 20.
- **No `ui/package.json` change** — a new npm package is a **STOP and ask**. So is a migration (nothing
  here can need one).
- **`PlaceholderCard.vue` is not deleted.** Step 9 orphans it by removing its only import; deleting a
  pre-existing file is out of scope (CLAUDE.md "surgical changes"). Remove only the import your change
  orphaned, and note the now-unused component in the commit body.
- **No write to the athlete profile.** `useProfileStore`, `services/profile.ts` and everything under
  `api/` are **read-only** here (ADR-0011 §1). The Resting HR fallback is a *read*.

### 0e. Two facts verified during spec-writing that change what you type

1. **`MetricTile` renders numeric values through `useCountUp`, which formats with `decimals = 0`.**
   `MetricTile.vue:34` calls `useCountUp(numericValue)` with **no options**, and `useCountUp.ts:20`
   defaults `decimals = 0` — so a numeric `7.5` renders **`"8"`**. `MetricTile.vue:35–39` also shows the
   escape hatch: a **string** `value` is returned verbatim, which is exactly how
   `WorkoutDetailView.vue:198` passes `formatDuration(...)`.
   **Therefore:** the one-decimal tiles (**Sleep**, **Weight**) pass `average.toFixed(1)` — a *string* —
   and the whole-number tiles (**Resting HR**, **HRV**) pass `Math.round(average)` — a *number*. This is
   not a missing `MetricTile` prop and is **not** a STOP: it is the existing string-value path. Getting
   this wrong is a silently failing spec (`7.46` would render `8`, not `7.5`).
2. **`useCountUp` animates with `requestAnimationFrame`** (`useCountUp.ts:47,49`) unless the user prefers
   reduced motion. Under jsdom the test setup's `matchMedia` stub reports `reduce`, so text assertions
   stay synchronous — but in the **in-app Browser pane**, where rAF is frozen, a numeric tile would stall
   mid-animation. Step 11 shims rAF for exactly this reason.

---

## Step 1 — `ui/src/lib/wellness.ts` (new, pure)

**File:** `ui/src/lib/wellness.ts` — create with:

```ts
import type { WellnessDailyPoint, WellnessMetricKey } from '@/types/wellness'

// Pure tile maths for the wellness dashboard tiles. No Vue, no store, no Date — the same shape as
// lib/weeklyTarget.ts, and tested on its own.
//
// ADR-0011 §5 splits the six metrics in two, and this file is where that split is expressed:
//
//   up is good   → sleepHours, hrvMs               → upIsGoodDelta()  → MetricTile's `delta` prop
//   down is good → restingHr, weightKg, soreness   → invertedChange() → MetricTile's `#footer` slot
//
// DeltaChip colours `up` green and `down` red by documented convention (lib/weeklyTarget.ts:21-23:
// "Do not 'fix' the chip's colours."), and it has four existing consumers. Routing an inverted metric
// through it would paint good news red — so those tiles pass NO `delta` prop at all and the inversion
// lives here, once. Soreness has no tile this phase; if it earns one, it takes the inverted path.

// The non-null values of `key` in day order — Sparkline's input. Callers hand the result straight to
// MetricTile, whose own `spark && spark.length >= 2` guard (MetricTile.vue:80) handles the 0- and
// 1-entry athlete: fewer than two points renders no sparkline rather than a misleading flat line.
// Never padded, never zero-filled — a day with no reading is missing, not a zero.
export function metricSeries(days: WellnessDailyPoint[], key: WellnessMetricKey): number[] {
  const out: number[] = []
  for (const day of days) {
    const value = day[key]
    if (value != null) out.push(value)
  }
  return out
}

// ONLY for metrics where up is good (sleep hours, HRV). Mirrors stores/analytics.ts:120-130's
// tsbDeltaVs7d: null when there is no delta, otherwise a sign-prefixed label and the direction.
export function upIsGoodDelta(
  delta: number | null | undefined,
  digits = 1,
): { text: string; dir: 'up' | 'down' | 'flat' } | null {
  if (delta == null) return null
  const dir = delta > 0 ? 'up' : delta < 0 ? 'down' : 'flat'
  return { text: `${delta > 0 ? '+' : ''}${delta.toFixed(digits)}`, dir }
}

// For the inverted metrics (resting HR, weight, soreness), which must NEVER pass MetricTile's `delta`
// prop. Returns footer text plus its own colour class: a DROP is good news, which is the inversion
// DeltaChip deliberately cannot express (see the header comment).
export function invertedChange(
  delta: number | null | undefined,
  unit: string,
  digits = 0,
): { text: string; className: string } | null {
  if (delta == null) return null
  const className = delta < 0 ? 'text-good' : delta > 0 ? 'text-bad' : 'text-muted-foreground'
  // Plain ASCII '-' — whatever toFixed emits. Do not substitute a typographic minus: the specs assert
  // on this exact string.
  return { text: `${delta > 0 ? '+' : ''}${delta.toFixed(digits)} ${unit} vs prior 7d`, className }
}
```

`day[key]` types as `number | null` because `WellnessMetricKey` excludes `date` — that is why 20-3
exported the union rather than letting this file re-derive it.

**Verify:** `cd ui; pnpm run build` green. (`vue-tsc -b` type-checks the new module against
`types/wellness.ts`; a wrong key union fails here, which is the point.)

---

## Step 2 — `ui/src/lib/__tests__/wellness.spec.ts` (new)

**File:** `ui/src/lib/__tests__/wellness.spec.ts` — create with (the pure-helper spec pattern from
`weeklyTarget.spec.ts`: no mounting, no Pinia):

```ts
import { describe, expect, it } from 'vitest'
import { invertedChange, metricSeries, upIsGoodDelta } from '@/lib/wellness'
import type { WellnessDailyPoint } from '@/types/wellness'

function day(date: string, over: Partial<WellnessDailyPoint> = {}): WellnessDailyPoint {
  return {
    date,
    sleepHours: null,
    sleepQuality: null,
    restingHr: null,
    weightKg: null,
    soreness: null,
    hrvMs: null,
    ...over,
  }
}

describe('metricSeries', () => {
  it('returns only the non-null values, in day order', () => {
    const days = [
      day('2026-07-24', { sleepHours: 7 }),
      day('2026-07-25'),
      day('2026-07-26', { sleepHours: 8 }),
    ]

    expect(metricSeries(days, 'sleepHours')).toEqual([7, 8])
  })

  it('returns an empty array when no day carries the metric', () => {
    expect(metricSeries([day('2026-07-26', { restingHr: 48 })], 'sleepHours')).toEqual([])
  })
})

describe('upIsGoodDelta — ADR-0011 §5, sleep hours and HRV only', () => {
  it('labels a positive delta with a leading + and dir up', () => {
    expect(upIsGoodDelta(0.4)).toEqual({ text: '+0.4', dir: 'up' })
  })

  it('maps a negative delta to dir down', () => {
    expect(upIsGoodDelta(-0.4)).toEqual({ text: '-0.4', dir: 'down' })
  })

  it('maps zero to flat', () => {
    expect(upIsGoodDelta(0)).toEqual({ text: '0.0', dir: 'flat' })
  })

  it('returns null for a null delta', () => {
    expect(upIsGoodDelta(null)).toBeNull()
    expect(upIsGoodDelta(undefined)).toBeNull()
  })
})

describe('invertedChange — ADR-0011 §5, resting HR / weight / soreness', () => {
  // THE ADR-0011 §5 GUARD. If anyone later routes these through DeltaChip, the colours invert
  // (down → text-bad) and this test is the tripwire.
  it('colours a drop as good and a rise as bad', () => {
    expect(invertedChange(-2, 'bpm', 0)).toEqual({
      text: '-2 bpm vs prior 7d',
      className: 'text-good',
    })
    expect(invertedChange(2, 'bpm', 0)).toEqual({
      text: '+2 bpm vs prior 7d',
      className: 'text-bad',
    })
  })

  it('returns null for a null delta', () => {
    expect(invertedChange(null, 'kg', 1)).toBeNull()
  })
})
```

Boundary values are pinned exactly as `Tasks-20-4.md` states them: `0.4 → '+0.4'/up`,
`-0.4 → '-0.4'/down`, `0 → '0.0'/flat`, `(-2,'bpm',0) → '-2 bpm vs prior 7d'/text-good`,
`(+2,'bpm',0) → '+2 bpm vs prior 7d'/text-bad`. Do not "round" them to nicer numbers.

**Verify:** `cd ui; pnpm exec vitest run src/lib/__tests__/wellness.spec.ts --no-file-parallelism` —
**8 tests pass**.

---

## Step 3 — `ui/src/components/dashboard/SleepCard.vue` (new) — replaces the placeholder

**File:** `ui/src/components/dashboard/SleepCard.vue` — create with (the `WeeklyLoadCard`/`FormCard`
shape: a thin store-reading wrapper around `MetricTile`; **no new tile primitive**):

```vue
<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useWellnessStore } from '@/stores/wellness'
import { metricSeries, upIsGoodDelta } from '@/lib/wellness'

const store = useWellnessStore()

onMounted(() => {
  if (!store.summary) void store.loadSummary()
})

const sleep = computed(() => store.summary?.sleepHours ?? null)

// One decimal, as a STRING. MetricTile animates NUMERIC values through useCountUp, which formats with
// 0 decimals (MetricTile.vue:34 passes no options), so a numeric 7.5 would render "8". A string value
// is rendered verbatim (MetricTile.vue:35-39) — the same path WorkoutDetailView.vue:198 uses for
// formatDuration(). The server rounds to 2 decimals; tiles show 1. Null stays null so the tile shows "—".
const value = computed(() => {
  const average = sleep.value?.average
  return average == null ? null : average.toFixed(1)
})

// Sleep hours is one of the two metrics ADR-0011 §5 allows a DeltaChip for (more sleep is good).
const delta = computed(() => upIsGoodDelta(sleep.value?.delta, 1))

const spark = computed(() => metricSeries(store.summary?.days ?? [], 'sleepHours'))

const nights = computed(() => sleep.value?.daysWithData ?? 0)
</script>

<template>
  <MetricTile
    label="Sleep Avg"
    :value="value"
    unit="h"
    :delta="delta"
    :spark="spark"
    :loading="store.loadingSummary && !store.summary"
  >
    <template #footer>
      <p v-if="nights > 0" class="text-xs text-muted-foreground">
        {{ nights }} night{{ nights === 1 ? '' : 's' }} logged
      </p>
      <!-- Nothing logged: no fabricated zero — say what to do (the FormCard.vue:32-37 pattern). -->
      <p v-else-if="store.summary" class="text-xs text-muted-foreground">
        Log sleep to see your 7-day average
      </p>
    </template>
  </MetricTile>
</template>
```

Note what is **not** here: no `placeholder` prop (that was `PlaceholderCard`'s dashed border and "soon"
badge, `MetricTile.vue:44,51`), no sparkline padding, no `?? 0` on the average.

**Verify:** `cd ui; pnpm run build` green.

---

## Step 4 — `ui/src/components/dashboard/__tests__/SleepCard.spec.ts` (new)

**File:** `ui/src/components/dashboard/__tests__/SleepCard.spec.ts` — create with:

```ts
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import SleepCard from '@/components/dashboard/SleepCard.vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import Sparkline from '@/components/common/Sparkline.vue'
import type {
  WellnessDailyPoint,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

function metric(over: Partial<WellnessMetricSummary> = {}): WellnessMetricSummary {
  return { average: null, priorAverage: null, delta: null, daysWithData: 0, ...over }
}

function day(date: string, over: Partial<WellnessDailyPoint> = {}): WellnessDailyPoint {
  return {
    date,
    sleepHours: null,
    sleepQuality: null,
    restingHr: null,
    weightKg: null,
    soreness: null,
    hrvMs: null,
    ...over,
  }
}

function makeSummary(over: Partial<WellnessSummaryResponse> = {}): WellnessSummaryResponse {
  return {
    to: '2026-07-26',
    from: '2026-07-20',
    priorFrom: '2026-07-13',
    sleepHours: metric(),
    sleepQuality: metric(),
    restingHr: metric(),
    weightKg: metric(),
    soreness: metric(),
    hrvMs: metric(),
    days: [],
    hasAnyEntries: false,
    ...over,
  }
}

// Pass `undefined` for summary to leave the store unfetched.
function mountCard(summary?: WellnessSummaryResponse, loadingSummary = false) {
  return mount(SleepCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: { wellness: { summary: summary ?? null, loadingSummary } },
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('SleepCard', () => {
  it('renders the 7-day average with the h unit', () => {
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.46, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.text()).toContain('7.5')
    expect(wrapper.text()).toContain('h')
    expect(wrapper.text()).toContain('6 nights logged')

    wrapper.unmount()
  })

  it('renders a DeltaChip for sleep hours', () => {
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.5, delta: 0.4, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    const chip = wrapper.findComponent(DeltaChip)
    expect(chip.exists()).toBe(true)
    expect(chip.text()).toContain('+0.4')

    wrapper.unmount()
  })

  it('renders a sparkline when at least two nights are logged', () => {
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.5, daysWithData: 2 }),
        days: [day('2026-07-25', { sleepHours: 7 }), day('2026-07-26', { sleepHours: 8 })],
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(Sparkline).exists()).toBe(true)

    wrapper.unmount()
  })

  it('renders no sparkline with a single night', () => {
    // MetricTile.vue:80 / Sparkline.vue:45 — fewer than two points renders nothing. The 1-entry athlete
    // gets a number and no line: never a padded series, never a flat baseline.
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.5, daysWithData: 1 }),
        days: [day('2026-07-26', { sleepHours: 7.5 })],
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.text()).toContain('7.5')
    expect(wrapper.text()).toContain('1 night logged')
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })

  it('renders an em dash and the prompt when nothing is logged', () => {
    const wrapper = mountCard(makeSummary({ hasAnyEntries: false }))

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('Log sleep to see your 7-day average')
    // The 0-entry athlete: no sparkline, no fabricated zero.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)
    expect(wrapper.text()).not.toContain('0 nights')

    wrapper.unmount()
  })

  it('shows the loading state before the summary arrives', () => {
    const wrapper = mountCard(undefined, true)

    expect(wrapper.text()).toContain('Loading…')

    wrapper.unmount()
  })
})
```

`makeSummary`/`metric`/`day` stay **local to each spec file** — that is the house pattern
(`RestingHrCard.spec.ts`'s `makeRecommended`, `FormCard.spec.ts`'s `mountCard`). Do not extract a shared
fixture module; it is not in the contract's file list.

**Verify:**
`cd ui; pnpm exec vitest run src/components/dashboard/__tests__/SleepCard.spec.ts --no-file-parallelism`
— **6 tests pass**. If `renders the 7-day average` reports `8` instead of `7.5`, the card is passing a
number instead of the `toFixed(1)` string (Step 0e fact 1) — fix the card, **do not touch `MetricTile`.**

---

## Step 5 — `ui/src/components/dashboard/RestingHrCard.vue` (rewrite — this task owns the file)

**File:** `ui/src/components/dashboard/RestingHrCard.vue` — replace the whole 28-line file with:

```vue
<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useProfileStore } from '@/stores/profile'
import { useWellnessStore } from '@/stores/wellness'
import { invertedChange, metricSeries } from '@/lib/wellness'

const profile = useProfileStore()
const wellness = useWellnessStore()

onMounted(() => {
  if (!profile.recommended) void profile.loadRecommended()
  if (!wellness.summary) void wellness.loadSummary()
})

// Logged history first, whole bpm (a number, so MetricTile's count-up animates it).
const wellnessAvg = computed(() => {
  const average = wellness.summary?.restingHr.average
  return average == null ? null : Math.round(average)
})

// ADR-0011 §1's read-only fallback: prefer logged history, fall back to the onboarding value so a
// tile that has shipped since Phase 14 never regresses to "—" for an athlete who has not started
// logging. This is a READ. The card writes nothing back to the profile, and a wellness save never
// touches Athlete.RestingHr — the two sources stay independent.
const value = computed(() => wellnessAvg.value ?? profile.recommended?.restingHr ?? null)

const spark = computed(() => metricSeries(wellness.summary?.days ?? [], 'restingHr'))

// No `delta` prop anywhere on this tile: resting HR is inverted (ADR-0011 §5). A DROP is good news, and
// DeltaChip colours `down` red by documented convention (lib/weeklyTarget.ts:21-23). The 7-day change
// goes in the footer instead, coloured by invertedChange.
const change = computed(() =>
  wellnessAvg.value == null ? null : invertedChange(wellness.summary?.restingHr.delta, 'bpm', 0),
)

// Fetched, but neither a logged average nor an onboarding value.
const unset = computed(
  () =>
    wellnessAvg.value == null &&
    profile.recommended != null &&
    profile.recommended.restingHr == null,
)
</script>

<template>
  <MetricTile
    label="Resting HR"
    :value="value"
    unit="bpm"
    :spark="spark"
    :loading="!profile.recommended && !wellness.summary"
  >
    <template #footer>
      <!-- 1. Logged average with a prior week to compare against. -->
      <p v-if="change" class="font-mono text-[11px]" :class="change.className">{{ change.text }}</p>
      <!-- 2. Logged average, no prior-week data yet — no fabricated trend. -->
      <p v-else-if="wellnessAvg != null" class="text-xs text-muted-foreground">7-day average</p>
      <!-- 3. Falling back to the onboarding value (ADR-0011 §1) — say so, and point at logging. -->
      <p v-else-if="profile.recommended?.restingHr != null" class="text-xs text-muted-foreground">
        From profile · log RHR to see a trend
      </p>
      <!-- 4. Empty state: fetched but unset — point at the profile editor. -->
      <router-link
        v-else-if="unset"
        to="/profile"
        class="text-sm font-medium text-primary-hi hover:underline"
      >Set in profile</router-link>
    </template>
  </MetricTile>
</template>
```

Two things to get exactly right:

- **The `router-link` is byte-for-byte the original** — same `to`, same class list, same
  `>Set in profile</router-link>` with no inner whitespace. The only change is that its guard moved from
  the `<template #footer>` wrapper (which now always renders, because three other states use the slot)
  onto the element as `v-else-if="unset"`. The existing spec asserts `link.props('to') === '/profile'` and
  `link.text() === 'Set in profile'`; both hold.
- **`loading` is `!profile.recommended && !wellness.summary`** — loading only while there is *nothing at
  all* to show. The existing third spec case mounts with both null and must still see `Loading…`; a
  fetched profile with an unfetched summary must **not** re-enter the loading state.

**Verify:**
1. `cd ui; pnpm run build` green.
2. `cd ui; pnpm exec vitest run src/components/dashboard/__tests__/RestingHrCard.spec.ts --no-file-parallelism`
   — the **three pre-existing tests pass against the rewritten component with the spec file still
   unmodified**. This is the regression gate; do not edit the spec to make them pass. Walk the states if
   one fails: (1) `restingHr: 48`, no wellness → value 48, footer state 3, no `RouterLink`;
   (2) `restingHr: null`, no wellness → `—`, footer state 4, the link; (3) nothing seeded → `Loading…`.

---

## Step 6 — Extend `ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts`

**File:** `ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts`. **Extend, do not rewrite.** The
three existing `it` blocks stay byte-identical.

**6a.** Add imports and the local wellness fixtures above `makeRecommended`:

```ts
import DeltaChip from '@/components/common/DeltaChip.vue'
import Sparkline from '@/components/common/Sparkline.vue'
import type {
  WellnessDailyPoint,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

function metric(over: Partial<WellnessMetricSummary> = {}): WellnessMetricSummary {
  return { average: null, priorAverage: null, delta: null, daysWithData: 0, ...over }
}

function day(date: string, over: Partial<WellnessDailyPoint> = {}): WellnessDailyPoint {
  return {
    date,
    sleepHours: null,
    sleepQuality: null,
    restingHr: null,
    weightKg: null,
    soreness: null,
    hrvMs: null,
    ...over,
  }
}

function makeSummary(over: Partial<WellnessSummaryResponse> = {}): WellnessSummaryResponse {
  return {
    to: '2026-07-26',
    from: '2026-07-20',
    priorFrom: '2026-07-13',
    sleepHours: metric(),
    sleepQuality: metric(),
    restingHr: metric(),
    weightKg: metric(),
    soreness: metric(),
    hrvMs: metric(),
    days: [],
    hasAnyEntries: false,
    ...over,
  }
}
```

**6b.** Widen `mountCard()` with a second optional argument — the existing call sites (`mountCard()`,
`mountCard(makeRecommended(48))`, `mountCard(makeRecommended(null))`) keep working unchanged:

```ts
// Pass `undefined` to leave `recommended` null (unfetched → loading state).
function mountCard(recommended?: ProfileRecommendedResponse, summary?: WellnessSummaryResponse) {
  const initialState: Record<string, unknown> = {}
  if (recommended) initialState.profile = { recommended }
  if (summary) initialState.wellness = { summary }

  return mount(RestingHrCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: Object.keys(initialState).length > 0 ? initialState : undefined,
        }),
      ],
      stubs: { RouterLink: RouterLinkStub },
    },
    attachTo: document.body,
  })
}
```

**6c.** Append four new `it` blocks inside the existing `describe('RestingHrCard', …)`:

```ts
  it('prefers the wellness 7-day average over the profile value', () => {
    const wrapper = mountCard(
      makeRecommended(55),
      makeSummary({ restingHr: metric({ average: 48.4, daysWithData: 5 }), hasAnyEntries: true }),
    )

    expect(wrapper.text()).toContain('48')
    expect(wrapper.text()).not.toContain('55')

    wrapper.unmount()
  })

  it('falls back to the profile value when the athlete has no wellness entries', () => {
    const wrapper = mountCard(makeRecommended(55), makeSummary({ hasAnyEntries: false }))

    expect(wrapper.text()).toContain('55')
    expect(wrapper.text()).toContain('From profile · log RHR to see a trend')
    // Nothing logged: no sparkline, and no fabricated trend line in the footer.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })

  it('renders the 7-day change in the footer and never as a DeltaChip', () => {
    // THE INVERTED-METRIC GUARD (ADR-0011 §5). A -2 bpm drop is good news; DeltaChip would colour a
    // `down` direction red, so this tile must not pass the `delta` prop at all.
    const wrapper = mountCard(
      makeRecommended(55),
      makeSummary({
        restingHr: metric({ average: 48, priorAverage: 50, delta: -2, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(DeltaChip).exists()).toBe(false)
    expect(wrapper.text()).toContain('-2 bpm vs prior 7d')

    wrapper.unmount()
  })

  it('renders a sparkline when at least two days carry a resting HR', () => {
    const wrapper = mountCard(
      makeRecommended(55),
      makeSummary({
        restingHr: metric({ average: 48, daysWithData: 2 }),
        days: [day('2026-07-25', { restingHr: 49 }), day('2026-07-26', { restingHr: 47 })],
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(Sparkline).exists()).toBe(true)

    wrapper.unmount()
  })
```

All four documented footer states are now covered: change (new case 3), `7-day average` (new case 1,
delta null), `From profile · …` (new case 2 **and** the pre-existing `renders the bpm value` case), and
`Set in profile` (the pre-existing empty-state case).

**Verify:**
`cd ui; pnpm exec vitest run src/components/dashboard/__tests__/RestingHrCard.spec.ts --no-file-parallelism`
— **7 tests pass**, and `git diff ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts` shows the
three original `it` blocks **unmodified** (only additions plus the `mountCard` signature widening).

---

## Step 7 — `WeightCard.vue` + its spec

**7a. File:** `ui/src/components/dashboard/WeightCard.vue` — create with:

```vue
<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useWellnessStore } from '@/stores/wellness'
import { invertedChange, metricSeries } from '@/lib/wellness'

const store = useWellnessStore()

onMounted(() => {
  if (!store.summary) void store.loadSummary()
})

const weight = computed(() => store.summary?.weightKg ?? null)

// One decimal as a STRING — see SleepCard: MetricTile's count-up formats numbers with 0 decimals.
const value = computed(() => {
  const average = weight.value?.average
  return average == null ? null : average.toFixed(1)
})

const spark = computed(() => metricSeries(store.summary?.days ?? [], 'weightKg'))

// No `delta` prop — weight is inverted (ADR-0011 §5): a drop is good news and DeltaChip would render
// it red. The change goes in the footer with invertedChange's own colour.
const change = computed(() =>
  value.value == null ? null : invertedChange(weight.value?.delta, 'kg', 1),
)

// DELIBERATE ASYMMETRY WITH RestingHrCard, and the one place it will read as an oversight:
// there is NO fallback to Athlete.WeightKg (ADR-0011 §1). The profile number is a one-off onboarding
// self-report, and this is a TREND tile — seeding a trend from a single stale self-report would show a
// number the athlete never logged, above a sparkline that cannot move. Resting HR gets the fallback
// because its tile has shipped that exact profile value since Phase 14 and losing it would be a
// regression; weight never had one to lose. An athlete who has never logged sees "—" and the prompt.
</script>

<template>
  <MetricTile
    label="Weight"
    :value="value"
    unit="kg"
    :spark="spark"
    :loading="store.loadingSummary && !store.summary"
  >
    <template #footer>
      <p v-if="change" class="font-mono text-[11px]" :class="change.className">{{ change.text }}</p>
      <p v-else-if="value != null" class="text-xs text-muted-foreground">7-day average</p>
      <p v-else-if="store.summary" class="text-xs text-muted-foreground">
        Log weight to see a trend
      </p>
    </template>
  </MetricTile>
</template>
```

**7b. File:** `ui/src/components/dashboard/__tests__/WeightCard.spec.ts` — create with the same local
`metric`/`day`/`makeSummary`/`mountCard` helpers as Step 4 (copy them; keep them local), plus:

```ts
import DeltaChip from '@/components/common/DeltaChip.vue'
import Sparkline from '@/components/common/Sparkline.vue'
import type { ProfileRequiredResponse } from '@/types/profile'

// A distinctive profile weight, seeded ONLY so the last spec can prove the tile ignores it.
const profileRequired: ProfileRequiredResponse = {
  name: 'Test Athlete',
  gender: 'Female',
  dateOfBirth: '1992-06-15',
  heightCm: 170,
  weightKg: 81.7,
  yearsTraining: 4,
  typicalWeeklyHours: 9,
  methodology: 'Polarized',
}

describe('WeightCard', () => {
  it('renders the 7-day average in kg', () => {
    const wrapper = mountCard(
      makeSummary({
        weightKg: metric({ average: 72.43, daysWithData: 5 }),
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.text()).toContain('72.4')
    expect(wrapper.text()).toContain('kg')

    wrapper.unmount()
  })

  it('renders the change in the footer, not as a DeltaChip', () => {
    // ADR-0011 §5: losing weight is good news, so this tile passes no `delta` prop.
    const wrapper = mountCard(
      makeSummary({
        weightKg: metric({ average: 72.4, priorAverage: 73.0, delta: -0.6, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(DeltaChip).exists()).toBe(false)
    expect(wrapper.text()).toContain('-0.6 kg vs prior 7d')

    wrapper.unmount()
  })

  it('renders the prompt and no value when nothing is logged', () => {
    const wrapper = mountCard(makeSummary({ hasAnyEntries: false }), false, profileRequired)

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('Log weight to see a trend')
    // The deliberate asymmetry with Resting HR: no fallback to Athlete.WeightKg.
    expect(wrapper.text()).not.toContain('81.7')
    // 0-entry athlete: no sparkline, no fabricated number.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })
})
```

`mountCard` here takes a third optional argument and seeds `profile: { required }` when given:

```ts
function mountCard(
  summary?: WellnessSummaryResponse,
  loadingSummary = false,
  required?: ProfileRequiredResponse,
) {
  return mount(WeightCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: {
            wellness: { summary: summary ?? null, loadingSummary },
            ...(required ? { profile: { required } } : {}),
          },
        }),
      ],
    },
    attachTo: document.body,
  })
}
```

The profile seed is the point of the third case: `WeightCard` does not import `useProfileStore` at all, so
the assertion passes today — and fails the moment somebody "fixes" the asymmetry by adding the fallback.

Use the `Gender` / `MethodologyChoice` string literals that `ui/src/types/onboarding.ts` actually exports;
if `'Female'`/`'Polarized'` do not type-check, use the exported enum/const values rather than casting.

**Verify:**
`cd ui; pnpm exec vitest run src/components/dashboard/__tests__/WeightCard.spec.ts --no-file-parallelism`
— **3 tests pass**. `pnpm run build` green.

---

## Step 8 — `HrvCard.vue` + its spec

**8a. File:** `ui/src/components/dashboard/HrvCard.vue` — create with:

```vue
<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useWellnessStore } from '@/stores/wellness'
import { metricSeries, upIsGoodDelta } from '@/lib/wellness'

const store = useWellnessStore()

onMounted(() => {
  if (!store.summary) void store.loadSummary()
})

const hrv = computed(() => store.summary?.hrvMs ?? null)

// Whole ms — a number, so MetricTile's count-up (0 decimals) renders it correctly.
const value = computed(() => {
  const average = hrv.value?.average
  return average == null ? null : Math.round(average)
})

// HRV is the second metric where up is good, so it may carry a DeltaChip (ADR-0011 §5).
const delta = computed(() => upIsGoodDelta(hrv.value?.delta, 0))

const spark = computed(() => metricSeries(store.summary?.days ?? [], 'hrvMs'))

const days = computed(() => hrv.value?.daysWithData ?? 0)
</script>

<template>
  <MetricTile
    label="HRV"
    :value="value"
    unit="ms"
    :delta="delta"
    :spark="spark"
    :loading="store.loadingSummary && !store.summary"
  >
    <template #footer>
      <p v-if="days > 0" class="text-xs text-muted-foreground">{{ days }} days logged</p>
      <p v-else-if="store.summary" class="text-xs text-muted-foreground">Log HRV to see a trend</p>
    </template>
  </MetricTile>
</template>
```

**8b. File:** `ui/src/components/dashboard/__tests__/HrvCard.spec.ts` — create with the Step 4 helper block
(local copy) and:

```ts
describe('HrvCard', () => {
  it('renders the 7-day average in ms', () => {
    const wrapper = mountCard(
      makeSummary({ hrvMs: metric({ average: 88.2, daysWithData: 5 }), hasAnyEntries: true }),
    )

    expect(wrapper.text()).toContain('88')
    expect(wrapper.text()).toContain('ms')
    expect(wrapper.text()).toContain('5 days logged')

    wrapper.unmount()
  })

  it('renders a DeltaChip because up is good for HRV', () => {
    const wrapper = mountCard(
      makeSummary({
        hrvMs: metric({ average: 88, priorAverage: 83, delta: 5, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    const chip = wrapper.findComponent(DeltaChip)
    expect(chip.exists()).toBe(true)
    expect(chip.text()).toContain('+5')

    wrapper.unmount()
  })

  it('renders the prompt when nothing is logged', () => {
    const wrapper = mountCard(makeSummary({ hasAnyEntries: false }))

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('Log HRV to see a trend')
    // 0-entry athlete: no sparkline, no fabricated zero.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })
})
```

**Verify:**
`cd ui; pnpm exec vitest run src/components/dashboard/__tests__/HrvCard.spec.ts --no-file-parallelism`
— **3 tests pass**. `pnpm run build` green.

---

## Step 9 — `ui/src/views/HomeView.vue` (edit — this task is its sole owner)

**File:** `ui/src/views/HomeView.vue`. Exactly two edits to the script block and two to the template.
Nothing else in the file changes: the pre-onboarding hero (L42–83), the `onboarded` gate (L27–29),
`formattedDate` (L31–39), the middle row (L99–104) and the bottom row (L107–109) are untouched. No sport
filter, no date-range picker, no reordering of existing cards.

**9a. Imports (L7–14).** Remove the `PlaceholderCard` import at **L8** — the orphan this change creates —
and add the four new components. The resulting block, in full:

```ts
import AppShell from '@/components/layout/AppShell.vue'
import HrvCard from '@/components/dashboard/HrvCard.vue'
import PrimaryGoalCard from '@/components/dashboard/PrimaryGoalCard.vue'
import RestingHrCard from '@/components/dashboard/RestingHrCard.vue'
import SleepCard from '@/components/dashboard/SleepCard.vue'
import ThisWeekCard from '@/components/dashboard/ThisWeekCard.vue'
import WeeklyLoadCard from '@/components/dashboard/WeeklyLoadCard.vue'
import WeightCard from '@/components/dashboard/WeightCard.vue'
import FormCard from '@/components/dashboard/FormCard.vue'
import RecentActivityCard from '@/components/dashboard/RecentActivityCard.vue'
import WellnessQuickEntryCard from '@/components/wellness/WellnessQuickEntryCard.vue'
```

(The existing alphabetical run and the trailing `FormCard`/`RecentActivityCard` placement are preserved —
one line removed, four added, nothing reordered.)

**9b. Top stat row (L88–96).** Replace the four-line `<PlaceholderCard title="Sleep Avg" … />` block
(**L91–94**) with `<SleepCard />`. The row keeps its four columns, its classes and its existing order:

```html
    <!-- Top stat row -->
    <div class="stagger-in grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <WeeklyLoadCard />
      <RestingHrCard />
      <SleepCard />
      <FormCard />
    </div>
```

**9c. New wellness row.** Insert it immediately after the top stat row's closing `</div>` (L96) and
**before** the `<!-- Middle row: training plan + primary goal -->` comment (L98), separated by one blank
line, exactly as `Tasks-20-4.md` specifies:

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

`RecentActivityCard` stays last (L107–109). Soreness gets **no tile** this phase — it is captured and
shown in `WellnessQuickEntryCard`'s collapsed summary (Task 20-3), which renders no `DeltaChip`.

**9d. `PlaceholderCard.vue` stays on disk.** Its import is now gone and it has no other importer, so it is
unused — that is deliberate and out of scope to remove (CLAUDE.md "surgical changes"). It has no spec, so
nothing breaks; `vue-tsc -b` and `vite build` simply drop it from the graph. Note it in the commit body.

**Verify:**
- `cd ui; pnpm run build` green.
- `git grep -n "Sleep Avg" ui/src` — matches `SleepCard.vue` (and its spec), **not** a `PlaceholderCard`
  usage, and the string `Post-v1 — needs a device or health-app integration.` is gone from `ui/src`.
- `git grep -n "PlaceholderCard" ui/src` — matches **only**
  `ui/src/components/dashboard/PlaceholderCard.vue` itself (the import is gone; the file is not deleted).
- There is deliberately **no `HomeView.spec.ts`**: none exists today, the view is a pure composition
  behind an onboarding gate, and the cards carry the behaviour. Do not add one speculatively — say so in
  the commit body instead.

---

## Step 10 — Full static verification

- `cd ui; pnpm run build` — `vue-tsc -b && vite build` green.
- `cd ui; pnpm exec vitest run --no-file-parallelism` — **full suite**, zero failures. Counts must be
  **~24 tests / 4 files above Step 0's recorded numbers**: `wellness.spec.ts` (8), `SleepCard.spec.ts`
  (6), `WeightCard.spec.ts` (3), `HrvCard.spec.ts` (3), and `RestingHrCard.spec.ts` 3 → 7 (+4). Confirm
  specifically that the **three pre-existing `RestingHrCard` cases** and the **three pre-existing
  `RpeSelector` cases** (20-3's regression gate) still pass. Re-run once if the transient worker-fork
  crash appears with everything reporting passed.
- `dotnet build api/Bryk.sln` — green, warnings **16** on a clean (`--no-incremental`) compile.
- `dotnet test api/Bryk.sln` — green, count **identical** to Step 0's recording (this task touches no
  backend file; a changed number means something leaked).
- `git diff --stat` — exactly these 11 paths and nothing else:
  - `ui/src/lib/wellness.ts`, `ui/src/lib/__tests__/wellness.spec.ts`
  - `ui/src/components/dashboard/SleepCard.vue` + `__tests__/SleepCard.spec.ts`
  - `ui/src/components/dashboard/WeightCard.vue` + `__tests__/WeightCard.spec.ts`
  - `ui/src/components/dashboard/HrvCard.vue` + `__tests__/HrvCard.spec.ts`
  - `ui/src/components/dashboard/RestingHrCard.vue` + `__tests__/RestingHrCard.spec.ts`
  - `ui/src/views/HomeView.vue`
  - **Absent:** anything under `api/`; `DeltaChip.vue`, `MetricTile.vue`, `Sparkline.vue`;
    `router/index.ts`; `AppSidebar.vue`; `PlaceholderCard.vue`; every Task 20-3 file
    (`types/wellness.ts`, `services/wellness.ts`, `stores/wellness.ts`, `schemas/wellness.ts`,
    `ScaleSelector.vue`, `RpeSelector.vue`, `WellnessQuickEntryCard.vue` and their specs);
    `ui/package.json` / `pnpm-lock.yaml`; `FormCard.vue`, `stores/analytics.ts` and the PMC surfaces
    (no HRV-into-TSB blending, ADR-0011 §3). If any of these appear — **STOP**, that is scope creep
    beyond `Tasks-20-4.md`.
- Confirm by eye against the contract's review checklist: Sleep and HRV pass `delta`; Resting HR and
  Weight pass none and have a spec asserting `DeltaChip` is absent; the weight-asymmetry reasoning is a
  code comment; `lib/wellness.ts` imports no Vue, no store and no `Date`; every new SFC is
  `<script setup lang="ts">`; no component calls `fetch` or a service directly.

---

## Step 11 — Runtime browser gate (this is the task where the phase becomes visible)

A green build is not the acceptance signal here — the ROADMAP's Phase 20 success criteria are about what
the dashboard *shows*. Do this; do not infer it.

**Start the dev stack** (two shells, both from the repo root):

```powershell
# Shell 1 — API on https://localhost:60129 (the target ui/vite.config.ts proxies /api to)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
cd api/Bryk.API; dotnet run
```
```powershell
# Shell 2 — Vite dev server
cd ui; pnpm dev
```

`DevAuth:CurrentAthleteId` (user-secrets) selects the athlete; `ICurrentUserService` throws outside
Development, which is why the environment variable is not optional.

**Preview-pane caveat, carried forward from Phases 18 and 19 (project memory
`preview-raf-frozen-transition-shim`).** The in-app Browser pane does not composite frames, so
`requestAnimationFrame` is frozen: Vue's route `<Transition>` stalls in `page-leave-active`, an in-app
`router.push` may not repaint, and **screenshots time out**. Two consequences specific to this task:

- Land on `/` by a **full page load** (HomeView is non-lazy, so it renders without a route transition).
- **Shim rAF before reading any tile**, because `useCountUp` drives the numeric tiles (Resting HR, HRV)
  through it. Note the exact symptom so you do not misdiagnose it: `useCountUp`'s `display` ref starts as
  the **empty string** (`useCountUp.ts:21`) and the non-reduced-motion branch schedules the first frame
  without assigning (`:41–49`), so with rAF frozen the tile renders **blank** — not `0`, and not the `—`
  that a null value produces. A blank numeric tile in the preview pane is the harness, not your data:

  ```js
  window.requestAnimationFrame = (cb) => setTimeout(() => cb(performance.now()), 16)
  ```

- Read `document.body.textContent` rather than `innerText` or a screenshot (`innerText` needs layout the
  frozen tab will not compute). This is a harness artifact, **not an app bug**.

**Checks — all of them:**

1. **The placeholder is gone.** The third tile of the top row is a real Sleep tile: no dashed border
   (`MetricTile.vue:44`), no `soon` badge (`MetricTile.vue:51`), and the copy *"Post-v1 — needs a device
   or health-app integration."* is nowhere on the page.
2. **The wellness row renders** below the top row: the Today entry card spanning two columns, then
   Weight, then HRV.
3. **Fresh-athlete honesty (0 entries).** Sleep / Weight / HRV each show `—` plus their prompt
   (`Log sleep to see your 7-day average`, `Log weight to see a trend`, `Log HRV to see a trend`) and
   **no sparkline**. Resting HR shows the onboarding value with `From profile · log RHR to see a trend`.
   Nowhere does a tile show a zero it invented.
4. **Submit the Today entry card** (sleep `7.5`, resting HR `48`, weight `72.4`, soreness `3`, HRV `88`).
   The card collapses to its summary line and **all four tiles update from server truth without a
   reload** — that is 20-3's `saveToday` re-fetching both reads, with no event plumbing. Sleep reads
   `7.5 h`, Resting HR `48 bpm` (now the logged value, not the profile constant), Weight `72.4 kg`,
   HRV `88 ms`. Then hard-refresh and confirm the same numbers survive.
5. **The 1-entry rule.** With exactly one logged day, **no sparkline renders on any tile** — a value and
   no line. Then seed a second day (`PUT /api/v1/wellness/{date}` for yesterday, via Scalar or curl) and
   confirm sparklines appear on the tiles that now have two points.
6. **Deltas and colour.** Seed 2–3 days in the prior 7-day window with a *higher* resting HR and a
   *lower* HRV so both deltas are non-null. Confirm: Resting HR's footer reads e.g. `-2 bpm vs prior 7d`
   in **green** (`text-good`) — good news, not red; Sleep's `DeltaChip` renders with an arrow; and
   **no `DeltaChip` appears on the Resting HR or Weight tiles** (inspect the DOM: no
   `span.inline-flex…font-mono` chip beside their values). This is ADR-0011 §5 confirmed in the browser,
   not just in a spec.
7. **Console clean** — zero errors and zero warnings across initial load, form submit, and refresh.
   Check the Network tab too: `GET /api/v1/wellness/summary` fires once per load and again after a save.

Record what you observed (values, colours, console state) — Step 13's handoff needs it.

---

## Step 12 — Commit

One commit, exactly the message from `Tasks-20-4.md`. **No AI co-author trailer** (project convention,
`no-ai-coauthor-trailer`).

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

---

## Step 13 — Phase 20 closeout (propose; do not self-approve)

This is Phase 20's final task, so the phase closes here. These are **documentation** changes and belong in
a **separate `docs:` commit** after the feature commit above — the same shape as `34b24be docs: close out
Phase 19`. Propose them to the user and wait for the go-ahead; do not fold them into Step 12's commit and
do not assume the phase is "done" on your own authority.

1. **Handoff doc** — `md/handoffs/2026-07-26-phase-20-complete.md` (use the **actual** completion date if
   it is not 2026-07-26). Follow `md/handoffs/2026-07-26-phase-19-complete.md`'s structure: what shipped
   per task, the migration that was approved and applied (20-1), the verified test counts, the manual
   browser pass from Step 11 verbatim, and carry-forwards. Carry-forwards this task creates or inherits:
   - `ui/src/components/dashboard/PlaceholderCard.vue` is now **unused** — kept deliberately (surgical
     changes); delete it in a future cleanup pass if nothing claims it.
   - **Soreness has no tile.** It is captured and shown in the Today card only; if it earns a tile,
     ADR-0011 §5 puts its change in the footer like the other inverted metrics.
   - **No wellness history view** — there is no `/wellness` route in Phase 20; a history/trends page is a
     later phase.
   - **The preview-pane rAF caveat** (unchanged from Phases 18–19, with the `useCountUp` wrinkle this
     task adds: frozen rAF also stalls numeric tile values, so the shim is required to read them).
   - The two pre-existing `WorkoutsControllerTests.cs:121,150` nullable warnings remain deliberately
     unfixed; `ExceptionHandlingMiddleware` / ProblemDetails remains Phase 21's.
2. **`ROADMAP.md`** — flip the Phase 20 heading from `⏳` to `✅` (line 557), and confirm the success
   criteria in the entry (line 575) are all met by name: today's entry persists and survives reload,
   re-submit updates rather than duplicates, the Sleep tile shows a real 7-day average plus sparkline,
   the Resting HR sparkline reflects entries rather than the onboarding constant, and out-of-range /
   future dates are rejected with field messages. Anything not met is **not** a flip — report it instead.
3. **`CLAUDE.md`** — update the "Project state pointers" first bullet: current phase becomes
   **Phase 20 complete** with a one-line summary and a pointer to the new handoff; next feature phase
   becomes **Phase 21** (production hardening); Phase 12 stays deferred and approval-gated. Add
   **ADR-0011** to the indexed ADR list in the same terse style as 0009/0010 (one line naming its
   six sections' decisions). Keep it lean — resolved detail belongs in the ADR, not in `CLAUDE.md`
   (project memory: `claudemd-stays-lean`).

Suggested message for that second commit:

```
docs: close out Phase 20
```
