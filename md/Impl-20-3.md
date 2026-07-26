# Impl 20-3 — Build order: wellness types/service/store, the shared `ScaleSelector`, and the Today entry card

**Executor:** the architect-implementer. **Acceptance contract:** `md/Tasks-20-3.md`. **Decision lock:**
ADR-0011 §2 (PUT replaces the whole day — a field left blank is *cleared*, not preserved) and **§4** (the
`RpeSelector` → `ScaleSelector` generalization, and the literal-Tailwind-class constraint). ADR-0011 is
written by Task 20-1 and is referenced here without a code dependency on it — the same pattern Impl-19-5
used for ADR-0010 and Impl-18-4 for ADR-0009. **Scope:** Frontend only. New `types` / `services` /
`schemas` / `stores` modules for wellness, one new shared input, a **rewrite** of `RpeSelector.vue` into a
thin wrapper, the Today quick-entry card, and four new Vitest spec files. **No backend change, no
migration, no new npm package, no new route, no sidebar entry, and no view file** — this task does not
own and must not edit `ui/src/views/HomeView.vue`.

This is the step-by-step build order. Execute top-to-bottom; each step's **Verify** is the gate to the
next. One commit at the end with the message in `Tasks-20-3.md`.

**Ordering rationale.** The extraction + its regression gate comes *first* (Steps 1–3), before any data
layer, for two reasons: it is the riskiest piece (it rewrites a file with a live production consumer),
and everything downstream — the card's two scale fields — depends on `ScaleSelector` being correct. If
the regression gate at Step 3 fails, the fix is cheap and isolated because nothing else has been built
yet.

---

## Step 0 — Pre-flight

- `git status` clean on `main` (the coordinator verified a clean tree at `005481e`; Tasks 20-1 and 20-2
  will have added commits ahead of it).
- Baselines, recorded before the first edit:
  - `dotnet build api/Bryk.sln` green. `dotnet test api/Bryk.sln` — record the passing count. It is the
    **343** baseline plus whatever 20-1 and 20-2 added. **This task touches no C# file, so that number
    must be byte-identical at Step 12.** Warnings stay at **16** on a clean (`--no-incremental`)
    compile — 14 design-time `System.Security.Cryptography.Xml` NU1903 advisories plus the two
    pre-existing nullable warnings at `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150`.
    **Do not fix those two.**
  - `cd ui; pnpm run build` green (`vue-tsc -b && vite build`).
  - `cd ui; pnpm exec vitest run --no-file-parallelism` green — record **288 tests / 61 files** (the
    coordinator-verified Phase-20 baseline). This task must **rise** from it by roughly **25 tests across
    4 new files** (7 + 5 + 5 + 8), never fall.
- **Confirm Task 20-2 is actually merged, and confirm its wire shapes before writing `types/wellness.ts`.**
  This task's types are hand-mirrored from C# DTOs; a mismatch is invisible until 20-4 renders `undefined`.
  - Confirm `api/Bryk.API/Controllers/WellnessController.cs` and `api/Bryk.Application/Wellness/*` exist.
  - With the API running (`dotnet run` from `api/Bryk.API`), hit all three endpoints against the seeded
    dev athlete and read the JSON **field names** (System.Text.Json's web defaults ⇒ camelCase,
    `DateOnly` ⇒ `"YYYY-MM-DD"`):
    - `PUT /api/v1/wellness/{today}` with `{"sleepHours":7.5,"restingHr":48}` → **200**
      `WellnessEntryResponse` (`id`, `date`, the six metrics, `notes`).
    - `GET /api/v1/wellness?from={today}&to={today}` → **200** array; omitting a bound → **400**.
    - `GET /api/v1/wellness/summary` → `to`, `from`, `priorFrom`, six
      `{average, priorAverage, delta, daysWithData}` blocks, `days[]`, `hasAnyEntries`.
  - If any field is missing or shaped differently from `Tasks-20-3.md`'s Acceptance Criteria #1,
    **STOP** — do not reimplement or extend 20-2 inline, and do not add a field to the backend. Flag the
    gap and wait.
- Re-read `md/Tasks-20-3.md` in full. Open in the editor (**read-only** unless listed as an edit target):
  - `ui/src/components/common/RpeSelector.vue` — all 41 lines (**the one file this task rewrites**).
  - `ui/src/components/common/__tests__/RpeSelector.spec.ts` — 3 tests (**read-only, never edited**).
  - `ui/src/components/training/LogWorkoutForm.vue:243–259` — the only production consumer
    (**read-only, must not appear in `git diff`**).
  - `ui/src/services/api.ts` (49 lines), `ui/src/services/goals.ts`, `ui/src/services/analytics.ts:46–57`.
  - `ui/src/services/apiErrors.ts:38–47` (`extractApiValidationMessages`).
  - `ui/src/stores/goals.ts` (setup-store shape; `utcTodayIso()` at **L20–26**).
  - `ui/src/schemas/workouts.ts` (`optionalNumber()` at **L4–6**).
  - `ui/src/components/training/PeriodizationPanel.vue:83–136` — the closest form precedent:
    `useForm<T>({ validationSchema: toTypedSchema(refinedSchema), initialValues })`, a
    `form.resetForm({ values: … })` on entering edit mode, and `extractApiValidationMessages` in the
    catch.
  - `ui/src/components/dashboard/WeeklyLoadCard.vue:10–14` (the `onMounted` load guard),
    `ui/src/components/dashboard/PrimaryGoalCard.vue` (`card-surface` + `eyebrow` idiom).
  - Harnesses: `ui/src/services/__tests__/training.spec.ts:1–50` (fetch spy + `jsonResponse`),
    `ui/src/stores/__tests__/goals.spec.ts:1–30` (factory-style `vi.mock`),
    `ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts` (`createTestingPinia` +
    `attachTo: document.body`).

### Fences to hold for the whole task (re-checked at Step 12's `git diff --stat`)

- **Nothing under `api/` changes.** No DTO tweak, no "one more field on the summary". If the UI seems to
  need a shape 20-2 did not ship — **STOP and ask**.
- `ui/src/views/HomeView.vue` is **not edited**. Task 20-4 is its sole owner and mounts this card.
- `ui/src/components/training/LogWorkoutForm.vue` and
  `ui/src/components/common/__tests__/RpeSelector.spec.ts` are **not edited**. Both must be absent from
  `git diff`; the three RPE specs must pass unchanged. That is this task's regression gate.
- `ui/src/components/common/MetricTile.vue`, `Sparkline.vue`, `DeltaChip.vue` are **not edited**. A
  `DeltaChip` `invert` prop or recolour is explicitly rejected (ADR-0011 §5,
  `ui/src/lib/weeklyTarget.ts:21–23`) — **STOP and ask**.
- `ui/src/lib/wellness.ts` is **not created**, and no dashboard tile component is created — Task 20-4 owns
  both. No derived spark/delta computeds in this store.
- `ui/src/router/index.ts` and `ui/src/components/layout/AppSidebar.vue` are **not edited**.
- **No new npm package** (`ui/package.json` unchanged) — **STOP and ask**. vee-validate, zod,
  `@vueuse/core`, `lucide-vue-next` and the shadcn-vue components already present cover everything here.
  No date-picker library, no charting library.
- **No interpolated Tailwind class** anywhere (`grid-cols-${max}`, `col-start-${n}`). See Step 1.
- **No date picker on the card.** It is "Today" by definition; back-dating is not in Phase 20's scope —
  **STOP and ask** if it seems needed.
- No auth code (Phase 12 stays deferred and approval-gated), no `ExceptionHandlingMiddleware` /
  ProblemDetails work (Phase 21 owns the error contract), no migration (**STOP and ask** if one seems
  needed), no device/health sync, no readiness score, no HRV-into-TSB blending.
- Do not revert, stash, or commit unrelated working-tree changes.

### Two build-environment facts worth knowing before you start

- `ui/tsconfig.app.json` sets `"include": ["src/**/*.ts", …]` — **spec files are type-checked by
  `vue-tsc -b`**. A type error in a `.spec.ts` fails `pnpm run build`, not just `vitest`.
- The same file sets `"noUnusedLocals": true` / `"noUnusedParameters": true` — an import you added and
  then stopped using **fails the build**. Relevant when trimming spec fixtures.

---

## Step 1 — `ui/src/components/common/ScaleSelector.vue` (new)

The generalization. The button markup is a **move, not a redesign**: `type="button"`, `:key`, the
selected/unselected class pair from `RpeSelector.vue:21–25`, `:aria-pressed` and the `@click` emit are
copied **verbatim**. No new variants, no size prop, no disabled state, no keyboard-navigation rewrite.

The only deliberate substitution is the label row: the current three-`col-start` grid row
(`RpeSelector.vue:32–38`) cannot generalize across `max` values, so it becomes a three-span
`flex justify-between` row. For the RPE case the result is visually equivalent (Easy left, Steady centre,
Max right) and **no existing spec asserts that markup**. Note the substitution in the commit body.

> ### THE TAILWIND LITERAL-CLASS TRAP — read before typing
> Tailwind v4's scanner generates a utility only when it can see the class as a **literal string in the
> source**. `` `grid grid-cols-${props.max} gap-1` `` produces a class name at runtime that Tailwind
> never generated, so the element gets *no* grid-template — it silently renders a **single column**.
> That bug compiles, type-checks, and passes any test that only counts buttons. Both variants must exist
> as complete literal strings, which is what `gridClass` below does, and Step 2 pins both with a spec.

Paste-ready:

```vue
<script setup lang="ts">
import { computed } from 'vue'

// The tap-grid extracted from RpeSelector (ADR-0011 §4). RPE is 1-10 with Easy/Steady/Max; soreness
// (1-10) and sleep quality (1-5) use this component directly with their own labels.
const props = withDefaults(
  defineProps<{
    modelValue: number | null
    /** 5 or 10 today. Any other value renders that many buttons but falls back to the
     *  grid-cols-10 class — see gridClass. Deliberately not a union type: RpeSelector binds a
     *  plain :max="10" and a union would break that binding. */
    max?: number
    /** left / centre / right. null renders no label row. */
    labels?: [string, string, string] | null
  }>(),
  { max: 10, labels: null },
)

const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

const values = computed(() => Array.from({ length: props.max }, (_, i) => i + 1))

// Tailwind's scanner only generates classes it can see as LITERAL strings. `grid-cols-${max}` would
// compile to nothing and silently render a single column - so both variants are written out.
const gridClass = computed(() =>
  props.max === 5 ? 'grid grid-cols-5 gap-1' : 'grid grid-cols-10 gap-1',
)
</script>

<template>
  <div>
    <div :class="gridClass">
      <button
        v-for="v in values"
        :key="v"
        type="button"
        class="rounded-md border py-2.5 font-mono text-[13px] font-semibold transition-all duration-[120ms]"
        :class="
          modelValue === v
            ? 'border-primary bg-gradient-to-b from-primary to-primary-lo text-primary-foreground shadow-[0_4px_14px_var(--bryk-accent-glow)]'
            : 'border-border-strong bg-[#0d1015] text-subtle hover:border-[#3a4252] hover:text-foreground'
        "
        :aria-pressed="modelValue === v"
        @click="emit('update:modelValue', v)"
      >
        {{ v }}
      </button>
    </div>
    <div
      v-if="labels"
      class="mt-1.5 flex justify-between font-mono text-[9.5px] uppercase tracking-[0.1em] text-faint"
    >
      <span>{{ labels[0] }}</span>
      <span>{{ labels[1] }}</span>
      <span>{{ labels[2] }}</span>
    </div>
  </div>
</template>
```

**Verify:** `cd ui; pnpm run build` green.

---

## Step 2 — `ui/src/components/common/__tests__/ScaleSelector.spec.ts` (new)

Seven cases. Two of them are the Tailwind guard — they assert the **rendered class string** of the row
that actually holds the buttons, so an interpolated class fails them. Reaching the row through
`wrapper.find('button').element.parentElement` is deliberate: it names the element by its role rather
than by a positional `findAll('div')[1]`.

```ts
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ScaleSelector from '@/components/common/ScaleSelector.vue'

// The element that carries the grid class is, by construction, the buttons' parent.
function buttonRowClasses(wrapper: ReturnType<typeof mount>): string {
  return (wrapper.find('button').element.parentElement as HTMLElement).className
}

describe('ScaleSelector', () => {
  it('renders 1 through 10 by default', () => {
    const wrapper = mount(ScaleSelector, { props: { modelValue: null } })

    const labels = wrapper.findAll('button').map((b) => b.text())
    expect(labels).toEqual(['1', '2', '3', '4', '5', '6', '7', '8', '9', '10'])

    wrapper.unmount()
  })

  it('renders 1 through 5 when max is 5', () => {
    const wrapper = mount(ScaleSelector, { props: { modelValue: null, max: 5 } })

    const labels = wrapper.findAll('button').map((b) => b.text())
    expect(labels).toEqual(['1', '2', '3', '4', '5'])

    wrapper.unmount()
  })

  // The Tailwind guard: an interpolated `grid-cols-${max}` would never be generated by the
  // scanner and the grid would silently collapse to one column.
  it('uses a literal grid-cols-10 class by default', () => {
    const wrapper = mount(ScaleSelector, { props: { modelValue: null } })

    expect(buttonRowClasses(wrapper)).toContain('grid-cols-10')

    wrapper.unmount()
  })

  it('uses a literal grid-cols-5 class when max is 5', () => {
    const wrapper = mount(ScaleSelector, { props: { modelValue: null, max: 5 } })

    expect(buttonRowClasses(wrapper)).toContain('grid-cols-5')

    wrapper.unmount()
  })

  it('emits the tapped value', async () => {
    const wrapper = mount(ScaleSelector, { props: { modelValue: null } })

    const three = wrapper.findAll('button').find((b) => b.text() === '3')
    await three!.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[3]])

    wrapper.unmount()
  })

  it('marks the selected value as pressed', () => {
    const wrapper = mount(ScaleSelector, { props: { modelValue: 4 } })

    const pressed = wrapper.findAll('button[aria-pressed="true"]')
    expect(pressed).toHaveLength(1)
    expect(pressed[0].text()).toBe('4')

    wrapper.unmount()
  })

  it('renders no label row when labels is null, and the three labels when provided', () => {
    const bare = mount(ScaleSelector, { props: { modelValue: null } })
    expect(bare.text()).not.toContain('Easy')
    expect(bare.text()).not.toContain('Steady')
    expect(bare.text()).not.toContain('Max')
    bare.unmount()

    const labelled = mount(ScaleSelector, {
      props: { modelValue: null, max: 5, labels: ['Poor', 'OK', 'Great'] },
    })
    expect(labelled.text()).toContain('Poor')
    expect(labelled.text()).toContain('OK')
    expect(labelled.text()).toContain('Great')
    labelled.unmount()
  })
})
```

**Verify:**
```
cd ui; pnpm exec vitest run ui/src/components/common/__tests__/ScaleSelector.spec.ts --no-file-parallelism
```
All 7 cases green. If `labels: ['Poor', 'OK', 'Great']` trips a tuple-vs-`string[]` type error under
`vue-tsc`, annotate the fixture (`const labels: [string, string, string] = [...]`) — a type-only fix, no
behaviour change.

---

## Step 3 — Rewrite `ui/src/components/common/RpeSelector.vue` — **THE REGRESSION GATE**

> ### This step's Verify runs immediately, before anything else is built.
> `RpeSelector.spec.ts`'s three tests passing **unedited**, plus `LogWorkoutForm.vue` being absent from
> `git diff`, is how this task proves the extraction was behaviour-preserving. If the wrapper cannot
> satisfy those specs, **the extraction is wrong: fix `ScaleSelector`, never the spec.**

Replace the whole 41-line file with the wrapper. The props/emits contract is **byte-compatible** with
what `LogWorkoutForm.vue:252` binds today — that is the entire point:

| `LogWorkoutForm.vue:243–259` binds | Wrapper provides |
|---|---|
| `<RpeSelector … />` imported from `@/components/common/RpeSelector.vue` (L17) | same path, same default export, same component name |
| `:model-value="(value as number \| null) ?? null"` | `defineProps<{ modelValue: number \| null }>()` — same name, same type, still required |
| `@update:model-value="handleChange"` | `defineEmits<{ 'update:modelValue': [value: number] }>()` — same event, same single `number` payload |
| (nothing else) | no new props, no new emits, no renamed component |

```vue
<script setup lang="ts">
import ScaleSelector from '@/components/common/ScaleSelector.vue'

// A thin wrapper over ScaleSelector (ADR-0011 §4): RPE is 1-10 with Easy/Steady/Max, while soreness
// (1-10) and sleep quality (1-5) use ScaleSelector directly. Props and emits are unchanged from the
// pre-extraction component, so LogWorkoutForm.vue:252 and this component's three specs are untouched.
defineProps<{ modelValue: number | null }>()

const emit = defineEmits<{ 'update:modelValue': [value: number] }>()
</script>

<template>
  <ScaleSelector
    :model-value="modelValue"
    :max="10"
    :labels="['Easy', 'Steady', 'Max']"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
```

Notes:
- **Do not** add props, do not rename the component, do not change the import path
  `LogWorkoutForm.vue:17` uses.
- The three specs assert button labels `'1'…'10'`, the emitted value, and exactly one
  `aria-pressed="true"` — all still true through the wrapper because `ScaleSelector` renders the same
  markup with `max: 10`.
- If `vue-tsc` rejects the inline `['Easy', 'Steady', 'Max']` against the tuple prop type (it should
  contextually type it as a tuple), hoist it: `const RPE_LABELS: [string, string, string] = ['Easy',
  'Steady', 'Max']` and bind `:labels="RPE_LABELS"`. Type-only fix; the rendered output is identical.

**Verify — the gate, in this order:**
```
cd ui; pnpm exec vitest run ui/src/components/common/__tests__/RpeSelector.spec.ts --no-file-parallelism
```
→ **3 passed**, and the spec file was not edited. Then:
```
cd ui; pnpm exec vitest run ui/src/components/training/__tests__/LogWorkoutForm.spec.ts --no-file-parallelism
```
→ green (the RPE field still submits through the form). Then:
```
git status --short
```
→ `ui/src/components/common/__tests__/RpeSelector.spec.ts` and
`ui/src/components/training/LogWorkoutForm.vue` must **not** appear. Then `pnpm run build` green.

Do not proceed to Step 4 until all four checks hold.

---

## Step 4 — `ui/src/types/wellness.ts` (new)

Hand-mirrors Task 20-2's DTOs. `DateOnly` serializes as a `'YYYY-MM-DD'` string; `decimal?` and `int?`
both arrive as `number | null`. The header comment names the backend types so the two files can be diffed
by eye.

```ts
// Mirrors Task 20-2's shapes in api/Bryk.Application/Wellness/WellnessResponses.cs -
// WellnessEntryResponse, WellnessMetricSummaryDto, WellnessDailyPointDto, WellnessSummaryResponse -
// so the two files can be diffed by eye. DateOnly serializes as a 'YYYY-MM-DD' string; decimal? and
// int? both arrive as number | null.

// Exactly the PUT body. No `date` field: the date lives in the URL, and the route segment wins over
// anything in the body (Task 20-2, WellnessService.UpsertAsync step 1).
export interface WellnessEntryRequest {
  sleepHours: number | null
  sleepQuality: number | null
  restingHr: number | null
  weightKg: number | null
  soreness: number | null
  hrvMs: number | null
  notes: string | null
}

export interface WellnessEntryResponse extends WellnessEntryRequest {
  id: string
  date: string
}

export interface WellnessMetricSummary {
  average: number | null
  priorAverage: number | null
  delta: number | null
  daysWithData: number
}

export interface WellnessDailyPoint {
  date: string
  sleepHours: number | null
  sleepQuality: number | null
  restingHr: number | null
  weightKg: number | null
  soreness: number | null
  hrvMs: number | null
}

export interface WellnessSummaryResponse {
  to: string
  from: string
  priorFrom: string
  sleepHours: WellnessMetricSummary
  sleepQuality: WellnessMetricSummary
  restingHr: WellnessMetricSummary
  weightKg: WellnessMetricSummary
  soreness: WellnessMetricSummary
  hrvMs: WellnessMetricSummary
  days: WellnessDailyPoint[]
  hasAnyEntries: boolean
}

// The six metric keys, in entry order. Exported because 20-4's tile helpers key off it.
export type WellnessMetricKey =
  | 'sleepHours' | 'sleepQuality' | 'restingHr' | 'weightKg' | 'soreness' | 'hrvMs'
```

**Verify:** `pnpm run build` green (type-checks; no consumers yet). This is also the type-level gate
against Task 20-2 drift: `vue-tsc -b` will surface any mismatch as soon as Steps 5–11 consume these
shapes.

---

## Step 5 — `ui/src/services/wellness.ts` (new)

Mirrors `services/goals.ts`: one exported async function per endpoint, `apiFetch`, an explicit null check
that throws where a body is mandatory. Query strings are built by hand, as in `services/analytics.ts:46–57`.
No headers, no retries, **no client-side validation** — `apiFetch` owns the JSON header and the server is
the authority on bounds.

```ts
import { apiFetch } from '@/services/api'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessSummaryResponse,
} from '@/types/wellness'

// PUT is the whole write surface (ADR-0011 §2): it replaces the day, so a metric sent as null is
// cleared rather than preserved. The server answers 200 for both create and update - never 201.
// `date` is already 'YYYY-MM-DD'; interpolate it as-is. Do NOT encodeURIComponent it or reformat it -
// the route carries a {date:datetime} constraint that a re-encoded segment would fail (404).
export async function putWellness(
  date: string,
  data: WellnessEntryRequest,
): Promise<WellnessEntryResponse> {
  const result = await apiFetch<WellnessEntryResponse>(`/wellness/${date}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
  if (result === null) {
    throw new Error(`Unexpected empty response from PUT /wellness/${date}`)
  }
  return result
}

// Sparse, ascending. Both bounds are required by the server (400 otherwise); an empty body means
// "no entries in that window", which is a normal answer, not an error.
export async function getWellnessRange(
  from: string,
  to: string,
): Promise<WellnessEntryResponse[]> {
  return (await apiFetch<WellnessEntryResponse[]>(`/wellness?from=${from}&to=${to}`)) ?? []
}

// 7-day averages + deltas versus the prior 7 + a sparse 14-day daily series, in one call.
export async function getWellnessSummary(): Promise<WellnessSummaryResponse> {
  const result = await apiFetch<WellnessSummaryResponse>('/wellness/summary')
  if (result === null) {
    throw new Error('Unexpected empty response from GET /wellness/summary')
  }
  return result
}
```

**Verify:** `pnpm run build` green.

---

## Step 6 — `ui/src/services/__tests__/wellness.spec.ts` (new)

Fetch-spy harness copied from `services/__tests__/training.spec.ts:1–50`. Five cases, exactly the ones
`Tasks-20-3.md` names.

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getWellnessRange, getWellnessSummary, putWellness } from '@/services/wellness'
import { ApiError } from '@/services/api'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

// Explicit nulls are part of the contract: PUT replaces the day, so an omitted metric must travel
// as null rather than being dropped from the body.
const request: WellnessEntryRequest = {
  sleepHours: 7.5,
  sleepQuality: 4,
  restingHr: 48,
  weightKg: null,
  soreness: 3,
  hrvMs: null,
  notes: null,
}

const entry: WellnessEntryResponse = { id: 'w1', date: '2026-07-26', ...request }

function emptyMetric(): WellnessMetricSummary {
  return { average: null, priorAverage: null, delta: null, daysWithData: 0 }
}

const summary: WellnessSummaryResponse = {
  to: '2026-07-26',
  from: '2026-07-20',
  priorFrom: '2026-07-13',
  sleepHours: emptyMetric(),
  sleepQuality: emptyMetric(),
  restingHr: emptyMetric(),
  weightKg: emptyMetric(),
  soreness: emptyMetric(),
  hrvMs: emptyMetric(),
  days: [],
  hasAnyEntries: false,
}

describe('wellness service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('putWellness PUTs /api/v1/wellness/{date} with the metric body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(entry))

    const result = await putWellness('2026-07-26', request)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/wellness/2026-07-26`)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual(request)
    expect(result).toEqual(entry)
  })

  it('getWellnessRange builds the from/to query string', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse([entry]))

    const result = await getWellnessRange('2026-07-13', '2026-07-26')

    const [url] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/wellness?from=2026-07-13&to=2026-07-26`)
    expect(result).toEqual([entry])
  })

  it('getWellnessRange returns [] when the body is null', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))

    await expect(getWellnessRange('2026-07-13', '2026-07-26')).resolves.toEqual([])
  })

  it('getWellnessSummary GETs /api/v1/wellness/summary', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(summary))

    const result = await getWellnessSummary()

    const [url] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/wellness/summary`)
    expect(result).toEqual(summary)
  })

  // The card maps the server's field-prefixed messages, so the ApiError must survive the service.
  it('putWellness throws ApiError for a 400', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse(
        { errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'] },
        { status: 400 },
      ),
    )

    const err = await putWellness('2026-07-26', request).catch((e) => e)

    expect(err).toBeInstanceOf(ApiError)
    expect((err as ApiError).status).toBe(400)
    expect((err as ApiError).body).toEqual({
      errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'],
    })
  })
})
```

**Verify:**
```
cd ui; pnpm exec vitest run ui/src/services/__tests__/wellness.spec.ts --no-file-parallelism
```
All 5 cases green.

---

## Step 7 — `ui/src/schemas/wellness.ts` (new)

Mirrors Task 20-2's validator bounds **exactly** (0–16 / 1–5 / 25–120 / 30–250 / 1–10 / 10–250, notes
≤ 1000). This is instant feedback only; the server remains the authority. The `optionalNumber()`
preprocessor is copied locally, exactly as `schemas/workouts.ts:4–6` copied it from
`schemas/onboarding.ts` — **do not** extract a shared helper.

```ts
import { z } from 'zod'

// '' / null inputs collapse to null; otherwise coerce and validate. Copied from
// schemas/workouts.ts:4-6 - the local copy is the established precedent here, not a shared util.
function optionalNumber<T extends z.ZodType<number>>(check: T) {
  return z.preprocess((v) => (v === '' || v == null ? null : v), check.nullable())
}

// Bounds mirror Task 20-2's WellnessEntryRequestValidator exactly (the ROADMAP's Phase 20 numbers,
// inclusive). Client-side validation is for instant feedback only - the server stays the authority.
export const wellnessEntrySchema = z
  .object({
    sleepHours: optionalNumber(z.coerce.number().gte(0, 'Min 0').lte(16, 'Max 16')),
    sleepQuality: optionalNumber(z.coerce.number().int('Whole number').gte(1, 'Min 1').lte(5, 'Max 5')),
    restingHr: optionalNumber(z.coerce.number().int('Whole number').gte(25, 'Min 25').lte(120, 'Max 120')),
    weightKg: optionalNumber(z.coerce.number().gte(30, 'Min 30').lte(250, 'Max 250')),
    soreness: optionalNumber(z.coerce.number().int('Whole number').gte(1, 'Min 1').lte(10, 'Max 10')),
    hrvMs: optionalNumber(z.coerce.number().int('Whole number').gte(10, 'Min 10').lte(250, 'Max 250')),
    notes: z.string().max(1000, 'Notes must be 1000 characters or fewer').nullable(),
  })
  // Mirrors the server's "Entry: At least one metric is required." rule. Notes alone does not
  // satisfy it - a row carrying only prose contributes to no tile and no average. The message is
  // attached to `sleepHours` so vee-validate has a field to render it against (the first field in
  // the form, so it lands where the eye already is).
  .refine(
    (v) =>
      v.sleepHours != null ||
      v.sleepQuality != null ||
      v.restingHr != null ||
      v.weightKg != null ||
      v.soreness != null ||
      v.hrvMs != null,
    { message: 'Enter at least one metric', path: ['sleepHours'] },
  )

export type WellnessFormValues = z.infer<typeof wellnessEntrySchema>
```

**Verify:** `pnpm run build` green.

---

## Step 8 — `ui/src/stores/wellness.ts` (new)

Setup-style store per `stores/goals.ts`. `utcTodayIso()` is copied locally with the same comment — the
duplication in `stores/goals.ts:20–26` and `stores/profile.ts:39–45` is deliberate precedent; **do not**
introduce a shared date util. **No derived spark/delta computeds here** — those are pure helpers owned by
Task 20-4 (`ui/src/lib/wellness.ts`, which this task does not create).

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import { getWellnessRange, getWellnessSummary, putWellness } from '@/services/wellness'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessSummaryResponse,
} from '@/types/wellness'

// Today as YYYY-MM-DD in UTC, so the date in the PUT URL and the range read match the server's
// DateOnly semantics. Mirrors the local helpers in stores/goals.ts:20-26 and stores/profile.ts.
function utcTodayIso(): string {
  const now = new Date()
  const yyyy = now.getUTCFullYear()
  const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(now.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

export const useWellnessStore = defineStore('wellness', () => {
  const summary = ref<WellnessSummaryResponse | null>(null)
  const today = ref<WellnessEntryResponse | null>(null)
  const loadingSummary = ref(false)
  const loadingToday = ref(false)
  const saving = ref(false)
  const error = ref<ApiError | Error | null>(null)

  async function loadSummary() {
    loadingSummary.value = true
    error.value = null
    try {
      summary.value = await getWellnessSummary()
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loadingSummary.value = false
    }
  }

  // A missing entry is the normal case for most of the day, not an error: the range read asks for a
  // single day and `today` simply stays null when it comes back empty.
  async function loadToday() {
    const d = utcTodayIso()
    loadingToday.value = true
    try {
      const rows = await getWellnessRange(d, d)
      today.value = rows[0] ?? null
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loadingToday.value = false
    }
  }

  // PUT replaces the whole day (ADR-0011 §2), then BOTH reads are re-fetched so every surface
  // bound to this store renders server truth rather than an optimistic guess.
  async function saveToday(values: WellnessEntryRequest) {
    saving.value = true
    error.value = null
    try {
      await putWellness(utcTodayIso(), values)
      await Promise.all([loadToday(), loadSummary()])
    } catch (e) {
      error.value = e as ApiError | Error
      // DELIBERATE re-throw. WellnessQuickEntryCard maps the server's field-prefixed messages
      // ("RestingHr: ...") onto its vee-validate fields, which it can only do if the ApiError
      // reaches it. Do NOT "tidy" this into a swallowed error - the card would then show nothing
      // on a 400. (Same convention as the re-throwing writes in stores/training.ts.)
      throw e
    } finally {
      saving.value = false
    }
  }

  return {
    summary,
    today,
    loadingSummary,
    loadingToday,
    saving,
    error,
    loadSummary,
    loadToday,
    saveToday,
  }
})
```

Note that `loadToday()` and `loadSummary()` never throw, so the only error that can escape `saveToday` is
the PUT's — the re-throw is unambiguous.

**Verify:** `pnpm run build` green.

---

## Step 9 — `ui/src/stores/__tests__/wellness.spec.ts` (new)

`createPinia`/`setActivePinia` plus the factory-style `vi.mock` the repo uses for store specs
(`stores/__tests__/goals.spec.ts:10–25`), not automock.

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useWellnessStore } from '@/stores/wellness'
import { getWellnessRange, getWellnessSummary, putWellness } from '@/services/wellness'
import { ApiError } from '@/services/api'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

vi.mock('@/services/wellness', () => ({
  putWellness: vi.fn(),
  getWellnessRange: vi.fn(),
  getWellnessSummary: vi.fn(),
}))

const putWellnessMock = vi.mocked(putWellness)
const getWellnessRangeMock = vi.mocked(getWellnessRange)
const getWellnessSummaryMock = vi.mocked(getWellnessSummary)

// The store's own helper, copied so the expected URL date is exact rather than approximate.
function utcTodayIso(): string {
  const now = new Date()
  return [
    now.getUTCFullYear(),
    String(now.getUTCMonth() + 1).padStart(2, '0'),
    String(now.getUTCDate()).padStart(2, '0'),
  ].join('-')
}

const request: WellnessEntryRequest = {
  sleepHours: 7.5,
  sleepQuality: null,
  restingHr: 48,
  weightKg: null,
  soreness: null,
  hrvMs: null,
  notes: null,
}

const entry: WellnessEntryResponse = { id: 'w1', date: utcTodayIso(), ...request }

function emptyMetric(): WellnessMetricSummary {
  return { average: null, priorAverage: null, delta: null, daysWithData: 0 }
}

const summary: WellnessSummaryResponse = {
  to: utcTodayIso(),
  from: '2026-07-20',
  priorFrom: '2026-07-13',
  sleepHours: emptyMetric(),
  sleepQuality: emptyMetric(),
  restingHr: emptyMetric(),
  weightKg: emptyMetric(),
  soreness: emptyMetric(),
  hrvMs: emptyMetric(),
  days: [],
  hasAnyEntries: false,
}

describe('wellness store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loadToday assigns the single row returned for today', async () => {
    getWellnessRangeMock.mockResolvedValue([entry])
    const store = useWellnessStore()

    await store.loadToday()

    expect(store.today).toEqual(entry)
    // The single-day read uses the same date for both bounds.
    expect(getWellnessRangeMock).toHaveBeenCalledWith(utcTodayIso(), utcTodayIso())
    expect(store.loadingToday).toBe(false)
  })

  it('loadToday leaves today null when the range comes back empty', async () => {
    getWellnessRangeMock.mockResolvedValue([])
    const store = useWellnessStore()

    await store.loadToday()

    expect(store.today).toBeNull()
    expect(store.error).toBeNull()
  })

  it('loadSummary assigns the summary and clears error', async () => {
    getWellnessSummaryMock.mockResolvedValue(summary)
    const store = useWellnessStore()
    store.error = new Error('stale')

    await store.loadSummary()

    expect(store.summary).toEqual(summary)
    expect(store.error).toBeNull()
    expect(store.loadingSummary).toBe(false)
  })

  it("saveToday PUTs today's date and re-fetches both reads", async () => {
    putWellnessMock.mockResolvedValue(entry)
    getWellnessRangeMock.mockResolvedValue([entry])
    getWellnessSummaryMock.mockResolvedValue(summary)
    const store = useWellnessStore()

    await store.saveToday(request)

    expect(putWellnessMock).toHaveBeenCalledWith(utcTodayIso(), request)
    expect(getWellnessRangeMock).toHaveBeenCalledTimes(1)
    expect(getWellnessSummaryMock).toHaveBeenCalledTimes(1)
    // Both re-fetches happen AFTER the write, so the store ends on server truth.
    expect(putWellnessMock.mock.invocationCallOrder[0]).toBeLessThan(
      getWellnessRangeMock.mock.invocationCallOrder[0],
    )
    expect(putWellnessMock.mock.invocationCallOrder[0]).toBeLessThan(
      getWellnessSummaryMock.mock.invocationCallOrder[0],
    )
    expect(store.saving).toBe(false)
  })

  it('saveToday re-throws an ApiError and clears saving', async () => {
    putWellnessMock.mockRejectedValue(
      new ApiError(400, 'Bad Request', {
        errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'],
      }),
    )
    const store = useWellnessStore()

    await expect(store.saveToday(request)).rejects.toBeInstanceOf(ApiError)

    expect(store.saving).toBe(false)
    expect(getWellnessRangeMock).not.toHaveBeenCalled()
    expect(getWellnessSummaryMock).not.toHaveBeenCalled()
  })
})
```

**Verify:**
```
cd ui; pnpm exec vitest run ui/src/stores/__tests__/wellness.spec.ts --no-file-parallelism
```
All 5 cases green. The last case is the one that fails loudly if anyone later "tidies" the re-throw.

---

## Step 10 — `ui/src/components/wellness/WellnessQuickEntryCard.vue` (new)

New folder `ui/src/components/wellness/`. No props. **No emits** — the tiles refresh because they read the
same store and `saveToday` re-fetches both reads; that is stated in a comment rather than plumbed as an
event.

Form wiring mirrors `PeriodizationPanel.vue:83–136` (`useForm<T>` + `toTypedSchema(refinedSchema)`,
`form.resetForm({ values })` on entering edit mode, `extractApiValidationMessages` in the catch) and
`LogWorkoutForm.vue:243–259` (the `FormField v-slot="{ value, handleChange }"` binding for a scale input).

Field order is exactly `sleepHours`, `sleepQuality`, `restingHr`, `weightKg`, `soreness`, `hrvMs`,
`notes` — the two `ScaleSelector`s must therefore be the 1st (sleep quality, `max=5`) and 2nd (soreness,
`max=10`) instances in the DOM, which is what Step 11's spec asserts.

```vue
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import ScaleSelector from '@/components/common/ScaleSelector.vue'
import { useWellnessStore } from '@/stores/wellness'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { wellnessEntrySchema, type WellnessFormValues } from '@/schemas/wellness'
import type { WellnessEntryRequest } from '@/types/wellness'

// No props and no emits by design: every surface that shows wellness reads this same Pinia store,
// and saveToday re-fetches both the day and the summary, so the tiles refresh without an event to
// plumb. There is deliberately NO date picker either - the card is "Today" by definition; back-dating
// is out of Phase 20's scope.
const store = useWellnessStore()

onMounted(() => {
  if (!store.today) void store.loadToday()
})

const expanded = ref(false)
const formError = ref<string | null>(null)

// Server field prefix -> form field. A LITERAL record: Task 20-2's messages are field-prefixed
// ("RestingHr: ...") because ValidateOrThrowAsync collects ErrorMessage only and drops the property
// name, so the prefix is the only handle we have. "Date:" and "Entry:" have no field to land on and
// fall through to the form-level line.
const SERVER_FIELD_MAP: Record<string, keyof WellnessFormValues> = {
  SleepHours: 'sleepHours',
  SleepQuality: 'sleepQuality',
  RestingHr: 'restingHr',
  WeightKg: 'weightKg',
  Soreness: 'soreness',
  HrvMs: 'hrvMs',
  Notes: 'notes',
}

// PUT replaces the whole day, so the form always starts from the stored day in full (or all-null).
function valuesFromStore(): WellnessFormValues {
  const t = store.today
  return {
    sleepHours: t?.sleepHours ?? null,
    sleepQuality: t?.sleepQuality ?? null,
    restingHr: t?.restingHr ?? null,
    weightKg: t?.weightKg ?? null,
    soreness: t?.soreness ?? null,
    hrvMs: t?.hrvMs ?? null,
    notes: t?.notes ?? null,
  }
}

const form = useForm<WellnessFormValues>({
  validationSchema: toTypedSchema(wellnessEntrySchema),
  initialValues: valuesFromStore(),
})

const summaryLine = computed(() => {
  const t = store.today
  if (!t) return ''
  const parts: string[] = []
  if (t.sleepHours != null) parts.push(`${t.sleepHours} h`)
  if (t.sleepQuality != null) parts.push(`Q${t.sleepQuality}`)
  if (t.restingHr != null) parts.push(`${t.restingHr} bpm`)
  if (t.weightKg != null) parts.push(`${t.weightKg} kg`)
  if (t.soreness != null) parts.push(`Sore ${t.soreness}`)
  if (t.hrvMs != null) parts.push(`HRV ${t.hrvMs}`)
  return parts.join(' · ')
})

function expand() {
  // loadToday may resolve after setup ran, so re-seed from the store on open
  // (mirrors PeriodizationPanel.vue:133).
  form.resetForm({ values: valuesFromStore() })
  formError.value = null
  expanded.value = true
}

function cancel() {
  expanded.value = false
  formError.value = null
}

const onSubmit = form.handleSubmit(async (values) => {
  formError.value = null
  const request: WellnessEntryRequest = {
    sleepHours: values.sleepHours ?? null,
    sleepQuality: values.sleepQuality ?? null,
    restingHr: values.restingHr ?? null,
    weightKg: values.weightKg ?? null,
    soreness: values.soreness ?? null,
    hrvMs: values.hrvMs ?? null,
    notes: values.notes ?? null,
  }

  try {
    await store.saveToday(request)
    expanded.value = false
  } catch (e) {
    const messages = extractApiValidationMessages(e)
    if (!messages) {
      formError.value = "Couldn't save that. Try again."
      return
    }
    const unmapped: string[] = []
    for (const message of messages) {
      const colon = message.indexOf(':')
      const field = colon > 0 ? SERVER_FIELD_MAP[message.slice(0, colon)] : undefined
      if (field) {
        form.setFieldError(field, message)
      } else {
        unmapped.push(message)
      }
    }
    if (unmapped.length > 0) {
      formError.value = unmapped.join(' ')
    }
  }
})
</script>

<template>
  <div class="card-surface p-6">
    <h3 class="eyebrow">Today</h3>

    <!-- Collapsed: the prompt (no entry) or the summary line (entry exists). -->
    <div v-if="!expanded" class="mt-3 flex flex-wrap items-center justify-between gap-3">
      <p v-if="store.today" class="font-mono text-sm">{{ summaryLine }}</p>
      <p v-else class="text-sm text-muted-foreground">No wellness logged today.</p>
      <Button type="button" variant="outline" size="sm" @click="expand">
        {{ store.today ? 'Edit' : 'Log today' }}
      </Button>
    </div>

    <!-- Expanded: the entry form. Field order is the entry order the store and the tiles use. -->
    <form v-else class="mt-3 space-y-4" @submit="onSubmit">
      <FormField v-slot="{ componentField }" name="sleepHours">
        <FormItem>
          <FormLabel>Sleep (h)</FormLabel>
          <FormControl><Input type="number" step="0.25" v-bind="componentField" /></FormControl>
          <!-- Also renders the at-least-one-metric message (schemas/wellness.ts pins it here). -->
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ value, handleChange }" name="sleepQuality">
        <FormItem>
          <FormLabel>Sleep quality</FormLabel>
          <FormControl>
            <ScaleSelector
              :model-value="(value as number | null) ?? null"
              :max="5"
              :labels="['Poor', 'OK', 'Great']"
              @update:model-value="handleChange"
            />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="restingHr">
        <FormItem>
          <FormLabel>Resting HR (bpm)</FormLabel>
          <FormControl><Input type="number" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="weightKg">
        <FormItem>
          <FormLabel>Weight (kg)</FormLabel>
          <FormControl><Input type="number" step="0.1" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ value, handleChange }" name="soreness">
        <FormItem>
          <FormLabel>Soreness</FormLabel>
          <FormControl>
            <ScaleSelector
              :model-value="(value as number | null) ?? null"
              :max="10"
              :labels="['None', 'Sore', 'Severe']"
              @update:model-value="handleChange"
            />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="hrvMs">
        <FormItem>
          <FormLabel>HRV (ms)</FormLabel>
          <FormControl><Input type="number" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="notes">
        <FormItem>
          <FormLabel>Notes</FormLabel>
          <FormControl><Input v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <p v-if="formError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ formError }}
      </p>

      <div class="flex items-center justify-end gap-3">
        <Button type="button" variant="ghost" size="sm" @click="cancel">Cancel</Button>
        <Button type="submit" :disabled="store.saving">Save</Button>
      </div>
    </form>
  </div>
</template>
```

Notes for the implementer:
- `useForm<WellnessFormValues>` over a top-level-`.refine`d schema is exactly what
  `PeriodizationPanel.vue:83–85` does with `planMetadataSchema`; it compiles today.
- If `form.setFieldError(field, message)` trips on `keyof WellnessFormValues` vs vee-validate's
  `Path<TValues>`, narrow the map's value type to `'sleepHours' | 'sleepQuality' | …` explicitly. Do not
  reach for `as any`.
- `colon > 0` (not `!== -1`) is deliberate: it also rejects a message that merely starts with `':'`,
  and avoids `slice(0, -1)` nonsense.
- **This component is mounted by nothing but its own spec after this commit — that is expected, not dead
  code.** Task 19-3 shipped a parser that DI could not resolve on the same reasoning. Task 20-4 mounts it
  on `HomeView`. **Do not mount it yourself to "try it out"** — `HomeView.vue` is 20-4's file.

**Verify:** `pnpm run build` green. Every new SFC is `<script setup lang="ts">` with typed
`defineProps`/`defineEmits`; the only HTTP-adjacent call is through the store, which is backed by
`src/services/`.

---

## Step 11 — `ui/src/components/wellness/__tests__/WellnessQuickEntryCard.spec.ts` (new)

`createTestingPinia({ createSpy: vi.fn })` with `initialState` on the `wellness` store, mounted
`attachTo: document.body` (the `RestingHrCard.spec.ts` pattern). Eight cases.

> ### The submit-timing gotcha
> A **valid** submit over a `.refine`d zod schema does not land after two ticks: vee-validate re-runs
> the whole-object validation and each refine adds a microtask hop. Project memory pins the budget at
> **~6 `flushPromises()`**. Budget for it up front rather than debugging a "silent" submit that is
> merely un-awaited. If a case is still flaky after 6, fall back to `vi.waitFor(...)`, which is what
> `LogWorkoutForm.spec.ts:35` and `GoalsGoalForm.spec.ts:33` use — do **not** "fix" it by weakening the
> assertion.

```ts
import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WellnessQuickEntryCard from '@/components/wellness/WellnessQuickEntryCard.vue'
import ScaleSelector from '@/components/common/ScaleSelector.vue'
import { useWellnessStore } from '@/stores/wellness'
import { ApiError } from '@/services/api'
import type { WellnessEntryResponse } from '@/types/wellness'

const entry: WellnessEntryResponse = {
  id: 'w1',
  date: '2026-07-26',
  sleepHours: 7.5,
  sleepQuality: 4,
  restingHr: 48,
  weightKg: 72.4,
  soreness: 3,
  hrvMs: 88,
  notes: null,
}

function mountCard(wellness: Record<string, unknown> = { today: null }) {
  const wrapper = mount(WellnessQuickEntryCard, {
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, initialState: { wellness } })],
    },
    attachTo: document.body,
  })
  return { wrapper, store: useWellnessStore() }
}

async function openForm(wrapper: VueWrapper) {
  const btn = wrapper
    .findAll('button')
    .find((b) => b.text() === 'Log today' || b.text() === 'Edit')
  await btn!.trigger('click')
}

// A valid submit over a REFINED zod schema needs ~6 ticks before the store call lands - the whole
// object is re-validated and each refine adds a microtask hop (project memory).
async function flushSubmit() {
  for (let i = 0; i < 6; i++) {
    await flushPromises()
  }
}

describe('WellnessQuickEntryCard', () => {
  it('renders the collapsed prompt when today has no entry', () => {
    const { wrapper } = mountCard()

    expect(wrapper.text()).toContain('No wellness logged today.')
    expect(wrapper.findAll('button').some((b) => b.text() === 'Log today')).toBe(true)
    expect(wrapper.find('input[name="sleepHours"]').exists()).toBe(false)

    wrapper.unmount()
  })

  it("renders today's values in the collapsed summary when an entry exists", () => {
    const { wrapper } = mountCard({ today: entry })

    expect(wrapper.text()).toContain('7.5')
    expect(wrapper.text()).toContain('48')
    expect(wrapper.text()).toContain('72.4')

    wrapper.unmount()
  })

  it('expands to the form when the button is clicked', async () => {
    const { wrapper } = mountCard()

    await openForm(wrapper)

    expect(wrapper.find('input[name="sleepHours"]').exists()).toBe(true)

    wrapper.unmount()
  })

  // Proves the max prop is wired rather than defaulted: 5 for sleep quality, 10 for soreness.
  it('renders a 5-button sleep-quality scale and a 10-button soreness scale', async () => {
    const { wrapper } = mountCard()

    await openForm(wrapper)

    const scales = wrapper.findAllComponents(ScaleSelector)
    expect(scales).toHaveLength(2)
    expect(scales[0].findAll('button')).toHaveLength(5)
    expect(scales[1].findAll('button')).toHaveLength(10)

    wrapper.unmount()
  })

  it('submits the entered metrics through the store', async () => {
    const { wrapper, store } = mountCard()

    await openForm(wrapper)
    await wrapper.find('input[name="sleepHours"]').setValue('7.5')
    await wrapper.find('form').trigger('submit')
    await flushSubmit()

    expect(store.saveToday).toHaveBeenCalledTimes(1)
    expect(store.saveToday).toHaveBeenCalledWith({
      sleepHours: 7.5,
      sleepQuality: null,
      restingHr: null,
      weightKg: null,
      soreness: null,
      hrvMs: null,
      notes: null,
    })

    wrapper.unmount()
  })

  it('does not submit when every metric is blank', async () => {
    const { wrapper, store } = mountCard()

    await openForm(wrapper)
    await wrapper.find('input[name="notes"]').setValue('felt rough')
    await wrapper.find('form').trigger('submit')
    await flushSubmit()

    expect(store.saveToday).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Enter at least one metric')

    wrapper.unmount()
  })

  it('maps a field-prefixed server error onto its field', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.saveToday).mockRejectedValue(
      new ApiError(400, 'Bad Request', {
        errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'],
      }),
    )

    await openForm(wrapper)
    await wrapper.find('input[name="sleepHours"]').setValue('7.5')
    await wrapper.find('form').trigger('submit')
    await flushSubmit()

    expect(wrapper.text()).toContain('Resting HR must be between 25 and 120 bpm.')

    wrapper.unmount()
  })

  it('renders an unmapped server message in the form-level error', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.saveToday).mockRejectedValue(
      new ApiError(400, 'Bad Request', { errors: ['Entry: At least one metric is required.'] }),
    )

    await openForm(wrapper)
    await wrapper.find('input[name="sleepHours"]').setValue('7.5')
    await wrapper.find('form').trigger('submit')
    await flushSubmit()

    expect(wrapper.text()).toContain('Entry: At least one metric is required.')

    wrapper.unmount()
  })
})
```

**Verify:**
```
cd ui; pnpm exec vitest run ui/src/components/wellness/__tests__/WellnessQuickEntryCard.spec.ts --no-file-parallelism
```
All 8 cases green. Then re-run the whole `common` folder to confirm the extraction still holds under the
new sibling:
```
cd ui; pnpm exec vitest run ui/src/components/common/__tests__ --no-file-parallelism
```

---

## Step 12 — Full verification + runtime checks + commit

### Build and suites

- `cd ui; pnpm run build` (`vue-tsc -b && vite build`) green. This is the **type-level gate against Task
  20-2**: `types/wellness.ts` is hand-mirrored from C# DTOs, and every consumer added in Steps 5–11 is
  type-checked here (spec files included — `tsconfig.app.json`'s `include` covers `src/**/*.ts`).
- `cd ui; pnpm exec vitest run --no-file-parallelism` — full suite green, risen from the **288 / 61
  files** Step-0 baseline to roughly **313 / 65 files** (+7 ScaleSelector, +5 service, +5 store, +8 card),
  with **zero failures**, and `ui/src/components/common/__tests__/RpeSelector.spec.ts` still reporting its
  **3** passing tests. If the known transient Vitest worker-fork crash appears while every test still
  reports passed, **re-run once** before treating it as real (repo memory: `vitest-worker-crash-transient`).
- `dotnet build api/Bryk.sln` and `dotnet test api/Bryk.sln` — green, and the passing count is
  **identical** to the Step-0 baseline (no C# file is in this task's diff). Warnings still **16** on a
  clean compile.

### Runtime checks (this task mounts nothing on a route — these are its runtime gates)

The card and `ScaleSelector` are not reachable in the running app after this commit; `HomeView.vue`
belongs to Task 20-4. So the runtime evidence is: (a) the specs above, (b) the type check, and (c) two
in-browser checks of the surfaces that *are* reachable:

1. **The extraction, live.** With the dev stack up (`dotnet run` from `api/Bryk.API`; `pnpm dev` from
   `ui/`), open a planned workout's **Log workout** form (`/workouts` → log, or a plan's session) and
   confirm the RPE grid still renders as **ten buttons in one row** with Easy / Steady / Max beneath,
   that tapping a number highlights exactly one button with the gradient/glow, and that the chosen value
   appears next to the "Perceived effort · RPE" label. This is the visual half of the regression gate —
   the specs assert behaviour, this confirms the grid did not collapse to one column (the Tailwind trap
   would be invisible to the specs but obvious here).
2. **The wire shapes, live.** Hit the three endpoints directly (Scalar, `curl`, or the browser) and
   eyeball the JSON against `ui/src/types/wellness.ts`:
   - `PUT /api/v1/wellness/{today}` with `{"sleepHours":7.5,"restingHr":48}` → **200**, camelCase
     `id`/`date`/metrics, `date` equal to the segment you sent (confirms `putWellness`'s raw
     interpolation satisfies the `{date:datetime}` constraint).
   - `GET /api/v1/wellness?from={today}&to={today}` → the row you just wrote.
   - `GET /api/v1/wellness/summary` → `to`/`from`/`priorFrom`, the six metric blocks with
     `average`/`priorAverage`/`delta`/`daysWithData`, `days[]`, `hasAnyEntries`.
   Any field name that differs from `types/wellness.ts` is a **STOP** — fix the TypeScript, never the
   backend.
3. The browser console stays clean throughout (no Vue warnings, no unhandled rejections).

### Diff sanity

`git diff --stat` must show exactly these, and nothing else:

- `ui/src/components/common/ScaleSelector.vue` (new)
- `ui/src/components/common/__tests__/ScaleSelector.spec.ts` (new)
- `ui/src/components/common/RpeSelector.vue` (**rewritten** — the only modified file in the task)
- `ui/src/types/wellness.ts` (new)
- `ui/src/services/wellness.ts` (new)
- `ui/src/services/__tests__/wellness.spec.ts` (new)
- `ui/src/schemas/wellness.ts` (new)
- `ui/src/stores/wellness.ts` (new)
- `ui/src/stores/__tests__/wellness.spec.ts` (new)
- `ui/src/components/wellness/WellnessQuickEntryCard.vue` (new)
- `ui/src/components/wellness/__tests__/WellnessQuickEntryCard.spec.ts` (new)

If the diff shows **anything under `api/`**, `ui/src/views/HomeView.vue`,
`ui/src/components/training/LogWorkoutForm.vue`,
`ui/src/components/common/__tests__/RpeSelector.spec.ts`, `ui/src/components/dashboard/*`,
`ui/src/lib/wellness.ts`, `ui/src/router/index.ts`, `ui/src/components/layout/AppSidebar.vue`,
`MetricTile.vue` / `Sparkline.vue` / `DeltaChip.vue`, or `ui/package.json` — **STOP**. That is scope creep
beyond `Tasks-20-3.md`.

### Review checklist (from `Tasks-20-3.md`, confirm each before committing)

- [ ] `RpeSelector.vue` is a wrapper with the same props/emits; `LogWorkoutForm.vue` and
      `RpeSelector.spec.ts` are absent from `git diff` and the three RPE specs pass unchanged.
- [ ] `ScaleSelector` writes `grid-cols-5` and `grid-cols-10` as **literal** strings, with specs
      asserting both rendered class strings.
- [ ] The button markup — selected-state gradient classes, `aria-pressed`, `type="button"` — was moved
      verbatim; no visual redesign rode along beyond the documented label-row substitution.
- [ ] Every HTTP call goes through `src/services/`; state lives in Pinia; every SFC is
      `<script setup lang="ts">` with typed `defineProps`/`defineEmits`.
- [ ] `saveToday` re-fetches both reads and re-throws (with the comment), and the card maps
      field-prefixed server messages onto vee-validate fields.
- [ ] Form bounds match Task 20-2's validator exactly (0–16 / 1–5 / 25–120 / 30–250 / 1–10 / 10–250,
      notes ≤ 1000) and notes alone does not satisfy the at-least-one-metric rule.
- [ ] No new npm package in `ui/package.json`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

### Commit

One commit, the message from `Tasks-20-3.md` verbatim (no AI co-author trailer):

```
feat(ui): wellness store, shared ScaleSelector and the Today entry card

Generalize rather than duplicate. RpeSelector hardcoded ten buttons, a
grid-cols-10 class and the labels Easy/Steady/Max, and soreness (1-10) and
sleep quality (1-5) want the same tap grid at different sizes. The grid moves
verbatim into ScaleSelector behind max + labels props, and RpeSelector becomes
a wrapper whose props and emits are unchanged - so LogWorkoutForm and the three
existing RpeSelector specs are untouched and still pass, which is the
regression gate for this change. Both grid-cols variants are written as literal
class strings: an interpolated grid-cols-${max} is invisible to Tailwind's
scanner and would silently render one column.

The rest is the foundation the dashboard needs: types mirroring the wellness
DTOs, a thin service over the three endpoints, and a Pinia store whose
saveToday PUTs today's date and then re-fetches both the day and the summary,
so every surface reading it updates from server truth rather than from an
optimistic guess. It re-throws on failure so the entry card can map the
server's field-prefixed messages (SleepHours:, RestingHr:) onto vee-validate
fields, with anything unmapped falling back to a form-level line.

The Today card collapses to a summary line when the day is already logged and
expands to the form otherwise, with the two scale fields bound through
vee-validate the same way RPE is. Its zod schema mirrors the server bounds
exactly for instant feedback, including the rule that notes alone is not a
metric; the server stays the authority. Nothing here mounts on the dashboard -
Task 20-4 owns HomeView.
```

When committing, add one line to the body noting the label-row substitution if it is not already implied:
the three-`col-start` grid row became a three-span `flex justify-between` row, because `col-start-*`
cannot generalize across `max` values — visually equivalent for RPE, and no existing spec asserts that
markup.
