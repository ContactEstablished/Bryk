# Impl 17-2 — Build order: port `ProgressRing`, refactor `PrimaryGoalCard` to share it

**Executor:** the architect-implementer. **Acceptance contract:** `md/Tasks-17-2.md`. **Decision lock:**
Task 15-3 precedent (`Sparkline.vue` port pattern) + ADR-0007 §"honest date-based progress" framing (no
new ADR — this task documents its own rolling-horizon fallback in the code comment, per the contract).
**Scope:** Frontend only. No new npm package, no data fetching inside the ring, no `GoalsView` (17-3).

## Step 0 — Pre-flight

- `git status` clean (17-1 committed). `pnpm run build` (from `ui/`) green; `pnpm test` green.
- **Design reference check:** `%TEMP%\bryk-design\` is currently **empty** — confirmed via directory
  listing. Per the task, this does **not** block: the `buildRingGeometry` geometry contract in
  `Tasks-17-2.md` (radius/circumference/dashOffset/ticks formulas, defaults `size=160, stroke=8,
  ticks=60`) is authoritative and self-contained. Proceed from the contract; do not invent a look beyond
  it. If the export later appears, a follow-up visual pass can reconcile — out of scope here.
- Open and re-read: `ui/src/components/common/Sparkline.vue` (the port pattern — computed path math,
  per-instance `useId()` gradient, `viewBox`, `pathLength="1"`, `vector-effect="non-scaling-stroke"`,
  `aria-hidden`), `ui/src/components/dashboard/PrimaryGoalCard.vue` (the card to refactor),
  `ui/src/composables/useCountUp.ts` (reduced-motion snap semantics), `ui/src/components/common/pills.ts`
  / `TypePill.vue` (precedent only — not consumed by the ring itself), `ui/src/style.css` (confirm
  `--bryk-accent` / `-hi` / `-lo`, `--bryk-fg-0..3`, `--bryk-accent-glow` tokens), `ui/src/lib/charts/pmc.ts`
  (the pure-geometry-module precedent: exported dims constant + pure builder function, no DOM/Vue),
  `ui/src/lib/charts/__tests__/pmc.spec.ts` (spec style for a pure geometry helper), `ui/src/test-setup.ts`
  (confirms `matchMedia` reports `reduce`, so `useCountUp` snaps synchronously in tests), and
  `ui/src/components/dashboard/__tests__/PrimaryGoalCard.spec.ts` (existing coverage that must keep
  passing).
- Confirm `ui/src/types/profile.ts`'s `EventResponse extends EventDto` shape — no `createdDate` /
  `startDate` field. This is why the dashboard card falls back to a rolling horizon (see Step 4).

## Step 1 — Pure geometry helper (`ui/src/lib/progressRing.ts`)

New file, no DOM, no Vue import — mirrors the `lib/charts/pmc.ts` pattern (exported dims/defaults +
one pure builder function).

```ts
export interface RingGeometry {
  size: number
  radius: number
  cx: number
  cy: number
  circumference: number
  dashArray: number
  dashOffset: number
  ticks: { x1: number; y1: number; x2: number; y2: number }[]
}

export interface RingOptions {
  size?: number
  stroke?: number
  ticks?: number
}

const RING_DEFAULTS: Required<RingOptions> = { size: 160, stroke: 8, ticks: 60 }

// Pure: fraction-in-[0,1] + size/stroke/tick-count → everything the SVG needs (track radius/center,
// circumference, the progress arc's dash values, and evenly spaced tick coordinates). No DOM, no Vue —
// unit-tested directly. Clamps out-of-range fractions (overdue → 1, not-yet-started → 0) and guards
// NaN/Infinity → 0 so a bad upstream date calc never renders a broken arc.
export function buildRingGeometry(fraction: number, opts: RingOptions = {}): RingGeometry {
  const { size, stroke, ticks } = { ...RING_DEFAULTS, ...opts }
  const safeFraction = Number.isFinite(fraction) ? Math.min(1, Math.max(0, fraction)) : 0

  const cx = size / 2
  const cy = size / 2
  const radius = size / 2 - stroke / 2
  const circumference = 2 * Math.PI * radius
  const dashArray = circumference
  const dashOffset = circumference * (1 - safeFraction)

  const tickInner = radius - stroke
  const tickOuter = radius + stroke / 2
  const tickMarks = Array.from({ length: ticks }, (_, i) => {
    const angle = (i / ticks) * 2 * Math.PI - Math.PI / 2
    const cos = Math.cos(angle)
    const sin = Math.sin(angle)
    return {
      x1: cx + tickInner * cos,
      y1: cy + tickInner * sin,
      x2: cx + tickOuter * cos,
      y2: cy + tickOuter * sin,
    }
  })

  return { size, radius, cx, cy, circumference, dashArray, dashOffset, ticks: tickMarks }
}
```

Notes:
- `-Math.PI / 2` rotates tick/arc zero to 12 o'clock, matching the arc's own start point (both use the
  standard SVG-circle parametrization starting at 3 o'clock rotated -90°) — keep this consistent so ticks
  and the arc share the same zero angle in Step 2's template.
- `tickInner`/`tickOuter` straddle the track ring so ticks read as marks crossing the stroke, mirroring a
  typical dial — this is presentation detail the Vitest cases below don't pin exactly (they pin `ticks`
  array length and NaN-freedom only), so exact inner/outer radii are not load-bearing.

**Verify:** `pnpm run build` green (type-checks; no consumers yet, but the module compiles standalone).

## Step 2 — Geometry unit tests (`ui/src/lib/__tests__/progressRing.spec.ts`)

Mirror `ui/src/lib/charts/__tests__/pmc.spec.ts`'s style (plain `describe`/`it`, `toBeCloseTo` for
floating-point circumference math).

```ts
import { describe, expect, it } from 'vitest'
import { buildRingGeometry } from '@/lib/progressRing'

describe('buildRingGeometry', () => {
  it('fraction = 0 → dashOffset equals the full circumference', () => {
    const g = buildRingGeometry(0)
    expect(g.dashOffset).toBeCloseTo(g.circumference, 5)
  })

  it('fraction = 1 → dashOffset is 0', () => {
    const g = buildRingGeometry(1)
    expect(g.dashOffset).toBeCloseTo(0, 5)
  })

  it('fraction = 0.5 → dashOffset is half the circumference', () => {
    const g = buildRingGeometry(0.5)
    expect(g.dashOffset).toBeCloseTo(g.circumference / 2, 5)
  })

  it('clamps an overshoot fraction (1.4) to 1', () => {
    const g = buildRingGeometry(1.4)
    const full = buildRingGeometry(1)
    expect(g.dashOffset).toBeCloseTo(full.dashOffset, 5)
  })

  it('guards NaN to 0', () => {
    const g = buildRingGeometry(NaN)
    const zero = buildRingGeometry(0)
    expect(g.dashOffset).toBeCloseTo(zero.dashOffset, 5)
  })

  it('ticks length matches the option', () => {
    const g = buildRingGeometry(0.5, { ticks: 12 })
    expect(g.ticks).toHaveLength(12)
  })

  it('defaults to 60 ticks and size 160', () => {
    const g = buildRingGeometry(0.3)
    expect(g.ticks).toHaveLength(60)
    expect(g.size).toBe(160)
  })

  it('never returns NaN in any numeric field', () => {
    for (const f of [0, 0.5, 1, 1.4, -0.2, NaN, Infinity, -Infinity]) {
      const g = buildRingGeometry(f)
      expect(Number.isFinite(g.radius)).toBe(true)
      expect(Number.isFinite(g.cx)).toBe(true)
      expect(Number.isFinite(g.cy)).toBe(true)
      expect(Number.isFinite(g.circumference)).toBe(true)
      expect(Number.isFinite(g.dashArray)).toBe(true)
      expect(Number.isFinite(g.dashOffset)).toBe(true)
      for (const t of g.ticks) {
        expect(Number.isFinite(t.x1)).toBe(true)
        expect(Number.isFinite(t.y1)).toBe(true)
        expect(Number.isFinite(t.x2)).toBe(true)
        expect(Number.isFinite(t.y2)).toBe(true)
      }
    }
  })
})
```

**Verify:** `pnpm test -- progressRing` (or `pnpm test`) green — all cases above pass, including the
`-0.2`/`Infinity`/`-Infinity` boundary values folded into the NaN-guard sweep.

## Step 3 — `ProgressRing.vue` (`ui/src/components/common/ProgressRing.vue`)

New presentational component. Mirrors `Sparkline.vue`: `computed` geometry via the pure helper,
per-instance `useId()` gradient, `aria-hidden` SVG, CSS-var stroke colors, no hardcoded hex/oklch.

```vue
<script setup lang="ts">
import { computed, useId } from 'vue'
import { buildRingGeometry } from '@/lib/progressRing'

const props = withDefaults(
  defineProps<{
    fraction: number
    centerValue?: string | number
    centerLabel?: string
    size?: number
    animate?: boolean
  }>(),
  { size: 160, animate: true },
)

const gradientId = useId()

const geometry = computed(() => buildRingGeometry(props.fraction, { size: props.size }))
</script>

<template>
  <div class="relative inline-flex items-center justify-center" :style="{ width: `${geometry.size}px`, height: `${geometry.size}px` }">
    <svg
      :viewBox="`0 0 ${geometry.size} ${geometry.size}`"
      :width="geometry.size"
      :height="geometry.size"
      aria-hidden="true"
    >
      <defs>
        <linearGradient :id="gradientId" x1="0" x2="1" y1="0" y2="1">
          <stop offset="0%" stop-color="var(--bryk-accent-hi)" />
          <stop offset="100%" stop-color="var(--bryk-accent-lo)" />
        </linearGradient>
      </defs>

      <!-- Track -->
      <circle
        :cx="geometry.cx"
        :cy="geometry.cy"
        :r="geometry.radius"
        fill="none"
        stroke="var(--bryk-fg-3)"
        stroke-opacity="0.25"
        :stroke-width="8"
        vector-effect="non-scaling-stroke"
      />

      <!-- Ticks -->
      <line
        v-for="(t, i) in geometry.ticks"
        :key="i"
        :x1="t.x1"
        :y1="t.y1"
        :x2="t.x2"
        :y2="t.y2"
        stroke="var(--bryk-fg-3)"
        stroke-opacity="0.4"
        stroke-width="1"
        vector-effect="non-scaling-stroke"
      />

      <!-- Progress arc -->
      <circle
        class="progress-ring-arc"
        :class="{ 'progress-ring-arc--animate': animate }"
        :cx="geometry.cx"
        :cy="geometry.cy"
        :r="geometry.radius"
        fill="none"
        :stroke="`url(#${gradientId})`"
        :stroke-width="8"
        stroke-linecap="round"
        :stroke-dasharray="geometry.dashArray"
        :stroke-dashoffset="geometry.dashOffset"
        vector-effect="non-scaling-stroke"
        :transform="`rotate(-90 ${geometry.cx} ${geometry.cy})`"
      />
    </svg>

    <div class="absolute inset-0 flex flex-col items-center justify-center gap-1">
      <slot name="center">
        <span
          v-if="centerValue !== undefined"
          class="bg-[linear-gradient(180deg,var(--bryk-fg-0),#888c98)] bg-clip-text text-4xl font-bold leading-none tracking-[-0.03em] tabular-nums text-transparent"
        >{{ centerValue }}</span>
        <span v-if="centerLabel" class="eyebrow">{{ centerLabel }}</span>
      </slot>
    </div>
  </div>
</template>

<style scoped>
.progress-ring-arc--animate {
  transition: stroke-dashoffset 600ms ease-out;
}

@media (prefers-reduced-motion: reduce) {
  .progress-ring-arc--animate {
    transition: none;
  }
}
</style>
```

Notes:
- `stroke-width="8"` is inlined per the contract's `stroke = 8` default rather than threaded through a
  second prop — the task only specifies `size` as a caller-facing dimension; do not add a `stroke` prop
  (keeps the call-site surface to what the acceptance criteria list).
- The `rotate(-90 cx cy)` transform on the arc puts its dash start at 12 o'clock, consistent with the
  ticks' `-Math.PI / 2` zero-angle from Step 1 — both read clockwise from 12.
- `#center` slot fallback renders `centerValue`/`centerLabel` exactly as the contract specifies; passing
  neither and using `#center` (as `PrimaryGoalCard`'s race-day branch will in Step 4) overrides the whole
  block.
- No `oklch(...)` literal is introduced — only the existing `--bryk-accent-hi`/`-lo`/`--bryk-fg-0`/`-3`
  var() references, per "What NOT to modify."

**Verify:** `pnpm run build` green (vue-tsc typechecks the new SFC; no consumers yet).

## Step 4 — `ProgressRing.vue` component tests (`ui/src/components/common/__tests__/ProgressRing.spec.ts`)

Mirror `Sparkline.spec.ts` / `MetricTile.spec.ts` style — plain `mount`, no Pinia needed (purely
presentational).

```ts
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ProgressRing from '@/components/common/ProgressRing.vue'
import { buildRingGeometry } from '@/lib/progressRing'

describe('ProgressRing', () => {
  it('renders the track, ticks, and progress arc with the expected dashoffset', () => {
    const wrapper = mount(ProgressRing, { props: { fraction: 0.5 } })

    const circles = wrapper.findAll('circle')
    expect(circles.length).toBeGreaterThanOrEqual(2) // track + arc

    const expected = buildRingGeometry(0.5, { size: 160 })
    const arc = circles.find((c) => c.classes().includes('progress-ring-arc'))
    expect(arc).toBeTruthy()
    expect(Number(arc!.attributes('stroke-dashoffset'))).toBeCloseTo(expected.dashOffset, 5)

    wrapper.unmount()
  })

  it('renders centerValue and centerLabel by default', () => {
    const wrapper = mount(ProgressRing, {
      props: { fraction: 0.3, centerValue: 12, centerLabel: 'weeks to go' },
    })

    expect(wrapper.text()).toContain('12')
    expect(wrapper.text()).toContain('weeks to go')

    wrapper.unmount()
  })

  it('renders the #center slot override instead of the default', () => {
    const wrapper = mount(ProgressRing, {
      props: { fraction: 0.3, centerValue: 12, centerLabel: 'weeks to go' },
      slots: { center: '<span>Tomorrow</span>' },
    })

    expect(wrapper.text()).toContain('Tomorrow')
    expect(wrapper.text()).not.toContain('weeks to go')

    wrapper.unmount()
  })
})
```

**Verify:** `pnpm test` green — all three `ProgressRing` cases pass alongside the Step 2 geometry suite.

## Step 5 — Refactor `PrimaryGoalCard.vue` to render its countdown through `ProgressRing`

**Edit** `ui/src/components/dashboard/PrimaryGoalCard.vue`. Preserve every line of outer copy (`Primary
Goal` eyebrow, event name, sport · date line, loading state, empty state) — only the week-number block
(the `v-if="days != null && days <= 1"` / `v-else` pair around the `88px` gradient number) is replaced.

```vue
<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useProfileStore } from '@/stores/profile'
import { useCountUp } from '@/composables/useCountUp'
import ProgressRing from '@/components/common/ProgressRing.vue'

const store = useProfileStore()

onMounted(() => {
  if (!store.goals) void store.loadGoals()
})

// ...daysUntil / formattedDate / days / weeks / animatedWeeks unchanged...

// Rolling-horizon fallback: EventResponse carries no creation/plan-start date, so the
// dashboard card cannot compute a true [start, target] elapsed fraction. Approximate with
// a fixed look-back horizon — the honest date-based signal until 17-3's GoalsView (which
// has the linked plan's startDate) supplies the true window.
const HORIZON_DAYS = 168 // 24 weeks

const fraction = computed(() => {
  if (days.value == null) return 0
  return Math.min(1, Math.max(0, 1 - days.value / HORIZON_DAYS))
})
</script>
```

Template — replace the `v-if="days != null && days <= 1"` / `v-else` block with:

```vue
<ProgressRing v-if="store.primaryEvent" class="mt-5" :fraction="fraction" :size="160">
  <template #center>
    <template v-if="days != null && days <= 1">
      <span
        class="bg-[linear-gradient(180deg,var(--bryk-fg-0),#888c98)] bg-clip-text text-3xl font-bold leading-[0.9] tracking-[-0.05em] text-transparent"
      >{{ days <= 0 ? 'Today' : 'Tomorrow' }}</span>
    </template>
    <template v-else>
      <span
        class="bg-[linear-gradient(180deg,var(--bryk-fg-0),#888c98)] bg-clip-text text-4xl font-bold leading-[0.9] tracking-[-0.05em] tabular-nums text-transparent"
      >{{ animatedWeeks }}</span>
      <span class="eyebrow">weeks to go</span>
      <span class="font-mono text-xs text-subtle">{{ days }} days</span>
    </template>
  </template>
</ProgressRing>
```

Notes:
- `fraction` is intentionally computed from `days`, not `weeks`, so it updates smoothly day-to-day rather
  than jumping only on week boundaries.
- Font sizes inside the slot shrink from the old bare `88px`/`5xl` to fit the ring's default `160px`
  diameter — this is the one visual delta the refactor introduces (the contract calls it a refactor, not
  pixel-for-pixel size parity of the number itself); the **text content and branch logic** (Today /
  Tomorrow / `{weeks}` + "weeks to go" + "{days} days") must match exactly, which is what the spec in
  Step 6 pins.
- Loading (`v-else-if="!store.goals"`) and empty (`v-else`, "No upcoming events." + "Set a goal" link)
  blocks are untouched — do not touch their markup or the `store.primaryEvent` / `store.goals` conditions.

**Verify:** `pnpm run build` green (vue-tsc). Do not run tests yet — Step 6 updates the spec that currently
targets the removed inline markup.

## Step 6 — Update `PrimaryGoalCard.spec.ts` for the ring refactor

**Edit** `ui/src/components/dashboard/__tests__/PrimaryGoalCard.spec.ts`. The existing three tests
(priority-trumps-proximity, excludes-past-events, empty-state) assert on `wrapper.text()`, which still
contains the event name / "No upcoming events." / "Set a goal" link — those should keep passing unchanged
against the refactored card. Add cases for the ring integration named in the contract:

```ts
import ProgressRing from '@/components/common/ProgressRing.vue'

// ...existing makeEvent/mountCard helpers unchanged...

it('renders a ProgressRing with the animated week count for a future event', () => {
  const wrapper = mountCard([
    makeEvent({ id: 'a', name: 'Boston Marathon', priority: 'A', eventDate: '2099-09-01' }),
  ])

  const ring = wrapper.findComponent(ProgressRing)
  expect(ring.exists()).toBe(true)
  expect(wrapper.text()).toContain('weeks to go')

  wrapper.unmount()
})

it('renders "Today" through the ring for a same-day event', () => {
  const today = new Date()
  const iso = `${today.getUTCFullYear()}-${String(today.getUTCMonth() + 1).padStart(2, '0')}-${String(today.getUTCDate()).padStart(2, '0')}`
  const wrapper = mountCard([makeEvent({ id: 'race', name: 'Race Day', priority: 'A', eventDate: iso })])

  expect(wrapper.findComponent(ProgressRing).exists()).toBe(true)
  expect(wrapper.text()).toContain('Today')

  wrapper.unmount()
})
```

Keep the pre-existing empty-state test ("No upcoming events." link) as-is — it does not touch the ring.

**Verify:** `pnpm test -- PrimaryGoalCard` green — all five cases (three pre-existing + two new) pass. If
the known transient worker crash appears with all tests reporting passed, re-run with
`pnpm exec vitest run --no-file-parallelism` before treating it as a real failure.

## Step 7 — Full verification + commit

- `pnpm run build` (vue-tsc + vite build) green.
- `pnpm test` green — full suite, including `progressRing.spec.ts`, `ProgressRing.spec.ts`, and the
  updated `PrimaryGoalCard.spec.ts`. If a transient worker crash shows with all tests passing, re-run
  `pnpm exec vitest run --no-file-parallelism` to confirm before investigating further.
- Manual smoke: run the dashboard (`pnpm dev`), confirm the Primary Goal card renders the ring with the
  countdown centered, the race-day/eve headline still swaps in at ≤1 day out, and the empty/loading states
  are unchanged from before the refactor.
- `git diff --stat` — only `ui/src/lib/progressRing.ts` + spec, `ui/src/components/common/ProgressRing.vue`
  + spec, `ui/src/components/dashboard/PrimaryGoalCard.vue`, and
  `ui/src/components/dashboard/__tests__/PrimaryGoalCard.spec.ts`. No `package.json` change, no other
  component/store/service touched.
- Commit with the message from `Tasks-17-2.md`:

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
