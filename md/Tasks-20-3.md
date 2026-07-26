# Task 20-3 — wellness frontend foundation: types, service, store, `ScaleSelector`, Today entry card

## Surface
Frontend (Vue) only. New `types` / `services` / `stores` / `schemas` modules for wellness, one new shared
input (`ui/src/components/common/ScaleSelector.vue`), a **rewrite of `RpeSelector.vue` into a thin
wrapper** over it, the Today quick-entry card (`ui/src/components/wellness/WellnessQuickEntryCard.vue`),
and Vitest coverage for all of it. **No backend change. No dashboard change — this task does not touch
`ui/src/views/HomeView.vue`.** The card ships mounted only by its own spec until Task 20-4 places it;
that is expected, not dead code (Task 19-3 shipped a parser that DI could not resolve for the same
reason).

## Why
Everything the dashboard renders in 20-4 needs a place to come from, and the one piece of UI that cannot
be assembled from existing components is the entry form itself. The scale input is the interesting part:
soreness (1–10) and sleep quality (1–5) want exactly the tap-grid `RpeSelector` already implements, but
`RpeSelector` hardcodes 10 buttons, a `grid-cols-10` class and the labels Easy/Steady/Max. The Sr. Dev's
call (ADR-0011 §4) is to **generalize, not duplicate**: extract `ScaleSelector` with `max` + `labels`
props and leave `RpeSelector` as a wrapper whose props and emits are byte-identical to today's. That
keeps `LogWorkoutForm.vue:252` and the three existing `RpeSelector` specs untouched — and those specs
passing unchanged is this task's regression gate.

## Depends on
- **Task 20-2** — the three endpoints and their exact shapes: `PUT /wellness/{date}` (**200**
  `WellnessEntryResponse` for both create and update), `GET /wellness?from=&to=` (sparse ascending list,
  **400** if a bound is missing), `GET /wellness/summary` (7-day averages + deltas + a sparse 14-day
  daily series + `hasAnyEntries`).
- **ADR-0011 §2** — PUT replaces the whole day; a field left blank is cleared, not preserved.
- **ADR-0011 §4** — the wrapper decision and the literal-Tailwind-class constraint.
- **Task 20-4** consumes this store and mounts this card. Nothing in this task may edit its files.

## Required reading
- `ui/src/components/common/RpeSelector.vue` — all 41 lines. The button loop (L10), the **static**
  `grid-cols-10` on the button row (L15) and again on the label row (L33), the three hardcoded labels
  (L35–37), props `{ modelValue: number | null }` and emits `'update:modelValue': [value: number]`.
  The selected/unselected class pair on L21–25 is the visual contract to preserve verbatim.
- `ui/src/components/common/__tests__/RpeSelector.spec.ts` — all three tests
  (`renders buttons 1 through 10`, `emits the tapped value`, `marks the selected value as pressed`).
  **This file must not change and must keep passing.**
- `ui/src/components/training/LogWorkoutForm.vue:243–259` — the only production consumer, bound through
  vee-validate's `v-slot="{ value, handleChange }"`. **Read only — this file must not appear in
  `git diff`.**
- `ui/src/services/api.ts` — `apiFetch<T>(path, init?): Promise<T | null>`; base URL
  `import.meta.env.VITE_API_BASE_URL ?? '/api/v1'`; `null` on 204 (L34–36); `ApiError { status,
  statusText, body }` on non-ok.
- `ui/src/services/goals.ts` — the service style: one exported async function per endpoint, `apiFetch`
  plus an explicit `if (result === null) throw new Error('Unexpected empty response from …')`.
- `ui/src/services/analytics.ts:46–52, 75–82` — how query strings are built by hand
  (`?from=${from}&to=${to}`).
- `ui/src/stores/goals.ts` — the store exemplar: setup-style `defineStore('name', () => {…})`, `ref`s
  that are `null` until loaded, `loading`, `error: ApiError | Error | null`, a `loadAll()` that
  try/catch/finally-s, mutation methods that call the API then **re-fetch**, and the local
  `utcTodayIso()` helper at **L20–26** (duplicated in `stores/profile.ts:39–45` on purpose — follow that
  precedent, do not introduce a shared date util).
- `ui/src/schemas/workouts.ts` — the zod exemplar: the `optionalNumber()` preprocessor at L4–6 that
  collapses `''`/`null` to `null`, `z.coerce.number()` bounds with short messages, `notes` as
  `.max(n).nullable()`.
- `ui/src/components/training/LogWorkoutForm.vue:1–30` — the vee-validate wiring to mirror: `useForm` +
  `toTypedSchema(schema)`, `FormField`/`FormItem`/`FormLabel`/`FormControl`/`FormMessage` from
  `@/components/ui/form`, `Input` from `@/components/ui/input`, `Button` from `@/components/ui/button`.
- `ui/src/services/apiErrors.ts:38–47` — `extractApiValidationMessages(err)` returns the server's
  `errors[]` for a 400 `ApiError`, or `null`. Task 20-2's messages are field-prefixed
  (`"SleepHours: …"`), which is what makes the mapping below a one-liner.
- `md/decisions/0011-wellness-metrics.md` §2 and §4.

## Acceptance criteria

### 1. `ui/src/types/wellness.ts` (new)

Mirrors Task 20-2's DTOs; dates are `'YYYY-MM-DD'` strings, decimals are `number`.

```ts
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
- A comment at the top naming the backend types these mirror (`WellnessEntryResponse`,
  `WellnessMetricSummaryDto`, `WellnessDailyPointDto`, `WellnessSummaryResponse`) so the two files can be
  diffed by eye.
- `WellnessEntryRequest` is exactly the PUT body — **no `date` field**, because the date is in the URL.

### 2. `ui/src/services/wellness.ts` (new)

```ts
export async function putWellness(date: string, data: WellnessEntryRequest): Promise<WellnessEntryResponse>
export async function getWellnessRange(from: string, to: string): Promise<WellnessEntryResponse[]>
export async function getWellnessSummary(): Promise<WellnessSummaryResponse>
```
- `putWellness` → `apiFetch<WellnessEntryResponse>(`/wellness/${date}`, { method: 'PUT', body:
  JSON.stringify(data) })`, throwing `Unexpected empty response from PUT /wellness/${date}` on `null`.
  The date is interpolated as `YYYY-MM-DD`; **do not** URL-encode or reformat it.
- `getWellnessRange` → `(await apiFetch<WellnessEntryResponse[]>(`/wellness?from=${from}&to=${to}`)) ?? []`.
- `getWellnessSummary` → `/wellness/summary`, throwing on `null`.
- No headers, no retries, no client-side validation — `apiFetch` owns the JSON header and the server is
  the authority on bounds.

### 3. `ui/src/schemas/wellness.ts` (new)

Mirrors Task 20-2's validator bounds exactly (the server remains the authority; this is for instant
feedback), built on the same `optionalNumber()` preprocessor `schemas/workouts.ts:4–6` uses — copy the
helper locally, as that file did.

```ts
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
  .refine(
    (v) => v.sleepHours != null || v.sleepQuality != null || v.restingHr != null ||
           v.weightKg != null || v.soreness != null || v.hrvMs != null,
    { message: 'Enter at least one metric', path: ['sleepHours'] },
  )

export type WellnessFormValues = z.infer<typeof wellnessEntrySchema>
```
- The `.refine` mirrors the server's `"Entry: At least one metric is required."` rule; `notes` alone does
  not satisfy it. Comment that the message is attached to `sleepHours` so vee-validate has a field to
  render it against.
- **Note for the specs:** a valid submit over a refined schema needs ~6 `flushPromises()`, not 2 (project
  memory, see `md/handoffs/` history). Budget for it rather than debugging a "silent" submit.

### 4. `ui/src/stores/wellness.ts` (new — setup style, per `stores/goals.ts`)

`export const useWellnessStore = defineStore('wellness', () => { … })` with:
- A local `utcTodayIso()` copied from `stores/goals.ts:20–26` (same comment about matching the server's
  `DateOnly` semantics).
- State: `summary: ref<WellnessSummaryResponse | null>(null)`, `today: ref<WellnessEntryResponse |
  null>(null)`, `loadingSummary: ref(false)`, `loadingToday: ref(false)`, `saving: ref(false)`,
  `error: ref<ApiError | Error | null>(null)`.
- `async function loadSummary()` — try/catch/finally, assigns `summary`, records `error`, never throws.
- `async function loadToday()` — `const d = utcTodayIso()`, calls `getWellnessRange(d, d)` and assigns
  `today.value = rows[0] ?? null`. Never throws; a missing entry is the normal case, not an error.
- `async function saveToday(values: WellnessEntryRequest)` — sets `saving`, calls
  `putWellness(utcTodayIso(), values)`, then **re-fetches both** (`await Promise.all([loadToday(),
  loadSummary()])`) so every tile reading this store updates from server truth. On failure it clears
  `saving` and **re-throws**, so the card can map the server's field messages (the
  `stores/training.ts` convention 19-5 documented). Comment the re-throw so it is not "tidied" into a
  swallowed error.
- Returns every ref and function above.
- **No derived spark/delta computeds here** — those are pure helpers owned by Task 20-4
  (`ui/src/lib/wellness.ts`). Do not pre-create that file.

### 5. `ui/src/components/common/ScaleSelector.vue` (new)

`<script setup lang="ts">`, Composition API.

```ts
const props = withDefaults(
  defineProps<{
    modelValue: number | null
    max?: number                              // 5 or 10 — see gridClass
    labels?: [string, string, string] | null  // left / centre / right; null renders no label row
  }>(),
  { max: 10, labels: null },
)
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

const values = computed(() => Array.from({ length: props.max }, (_, i) => i + 1))

// Tailwind's scanner only generates classes it can see as LITERAL strings. `grid-cols-${max}` would
// compile to nothing and silently render a single column — so both variants are written out.
const gridClass = computed(() => (props.max === 5 ? 'grid grid-cols-5 gap-1' : 'grid grid-cols-10 gap-1'))
```
- The button markup — `type="button"`, `:key`, the selected/unselected `:class` pair, `:aria-pressed`,
  `@click="emit('update:modelValue', v)"` — is **copied verbatim** from `RpeSelector.vue:16–30`. This is
  a move, not a redesign: no new variants, no size prop, no disabled state, no keyboard-navigation
  rewrite.
- The label row renders only when `labels` is non-null, as
  `<div class="mt-1.5 flex justify-between font-mono text-[9.5px] uppercase tracking-[0.1em] text-faint">`
  with three `<span>`s. This deliberately replaces the current three-`col-start` grid row
  (`RpeSelector.vue:32–38`), which cannot generalize across `max` values; for the RPE case the result is
  visually equivalent (Easy left, Steady centre, Max right) and **no existing spec asserts that markup**.
  Note the substitution in the commit body.
- `max` is documented as "5 or 10 today"; a value other than those still renders `max` buttons but falls
  back to the `grid-cols-10` class. Comment it — do not add validation or a union type that would break
  the wrapper's plain `:max="10"`.

### 6. `ui/src/components/common/RpeSelector.vue` (rewrite — this task owns the file)

The whole file becomes a wrapper, ~15 lines:

```vue
<script setup lang="ts">
import ScaleSelector from '@/components/common/ScaleSelector.vue'

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
- **The props/emits contract is unchanged**, which is the entire point: `LogWorkoutForm.vue:252` keeps
  working and `RpeSelector.spec.ts`'s three tests keep passing without edits (they assert button labels
  `'1'…'10'`, the emitted value, and a single `aria-pressed="true"` — all still true through the
  wrapper).
- A one-line comment naming ADR-0011 §4 and stating that RPE is 1–10 with Easy/Steady/Max, while
  soreness (1–10) and sleep quality (1–5) use `ScaleSelector` directly.
- **Do not** add props, do not rename the component, do not change the import path used by
  `LogWorkoutForm.vue`.

### 7. `ui/src/components/wellness/WellnessQuickEntryCard.vue` (new)

`<script setup lang="ts">`, reads `useWellnessStore()`. No props. No emits (the tiles refresh because
they read the same store — say so in a comment rather than plumbing an event).

- `onMounted`: `if (!store.today) void store.loadToday()`, guarded the way
  `WeeklyLoadCard.vue:10–14` guards its loads.
- Two states inside one `card-surface` panel:
  - **Collapsed, no entry today** — an eyebrow `Today`, a line such as `No wellness logged today.`, and a
    `Button` reading `Log today` that expands the form.
  - **Collapsed, entry exists** — the same eyebrow plus a `font-mono` summary line built from the
    non-null values of `store.today` (e.g. `7.5 h · Q4 · 48 bpm · 72.4 kg · Sore 3 · HRV 88`), and an
    `Edit` button that expands the form pre-filled from `store.today`.
  - **Expanded** — the form; `Cancel` collapses without saving.
- The form uses `useForm({ validationSchema: toTypedSchema(wellnessEntrySchema), initialValues })` with
  `initialValues` derived from `store.today` (all `null` when there is none). Fields, in this order:
  `sleepHours` (`Input type="number" step="0.25"`), `sleepQuality` (**`ScaleSelector` with `:max="5"` and
  labels `['Poor', 'OK', 'Great']`**), `restingHr` (`Input type="number"`), `weightKg`
  (`Input type="number" step="0.1"`), `soreness` (**`ScaleSelector` with `:max="10"` and labels
  `['None', 'Sore', 'Severe']`**), `hrvMs` (`Input type="number"`), `notes` (`Input`).
  The two scale fields bind through `FormField v-slot="{ value, handleChange }"` exactly as
  `LogWorkoutForm.vue:243–259` binds RPE.
- Submit → `store.saveToday(values)`; on success collapse and leave the store to refresh. On failure map
  the error: `extractApiValidationMessages(err)` → for each message take the text before the first `':'`
  and look it up in a **literal** record `{ SleepHours: 'sleepHours', SleepQuality: 'sleepQuality',
  RestingHr: 'restingHr', WeightKg: 'weightKg', Soreness: 'soreness', HrvMs: 'hrvMs', Notes: 'notes' }`;
  matched messages go to `setFieldError(path, message)`, unmatched (including `Date:` and `Entry:`) go to
  a local `formError` rendered in the destructive style. Non-`ApiError` failures set a generic
  `Couldn't save that. Try again.`
- The Save button is disabled while `store.saving`.
- **No date picker.** The card is "Today" by definition; back-dating is not in Phase 20's scope. If it
  seems needed — **STOP and ask**.

## Non-goals
- **This task does not edit `ui/src/views/HomeView.vue`.** Task 20-4 is its sole owner and mounts this
  card. The card being unmounted in the app after this commit is expected.
- **Do not edit `ui/src/components/training/LogWorkoutForm.vue`** or
  `ui/src/components/common/__tests__/RpeSelector.spec.ts`. Both must be absent from `git diff` and the
  three RPE specs must pass unchanged — that is this task's regression gate. If the wrapper cannot
  satisfy them, the extraction is wrong: fix `ScaleSelector`, do not touch the spec.
- **Do not edit `ui/src/components/common/MetricTile.vue`, `Sparkline.vue` or `DeltaChip.vue`.**
  Recolouring or adding an `invert` prop to `DeltaChip` is explicitly rejected (ADR-0011 §5,
  `ui/src/lib/weeklyTarget.ts:21–23`) — **STOP and ask** if a design seems to need it.
- **Do not create `ui/src/lib/wellness.ts`** or any dashboard tile component — Task 20-4 owns both.
- **Do not use an interpolated Tailwind class** (`grid-cols-${max}`, `col-start-${n}`). Both grid
  variants must exist as literal strings or the grid silently collapses to one column.
- **No backend change of any kind.** Nothing under `api/` may appear in `git diff` — no DTO tweak, no
  "one more field on the summary". If the UI seems to need a shape 20-2 did not ship — **STOP and ask**.
- **No new npm package** (**STOP and ask**) — vee-validate, zod, `@vueuse/core` and the shadcn-vue
  components already present cover this. No date-picker library, no charting library, no icon set beyond
  `lucide-vue-next`.
- **No new route and no sidebar entry.** The card lives on the dashboard; `ui/src/router/index.ts` and
  `ui/src/components/layout/AppSidebar.vue` must not appear in `git diff`.
- **No migration** — nothing here can need one; if it seems to, **STOP and ask**.
- **No auth code** — Phase 12 stays deferred and approval-gated.
- No device/health sync (Whoop/Oura/Apple Health), no readiness score, no hydration/nutrition/
  menstruation fields, no logging reminders or notifications, **no HRV-into-TSB blending**.
- **No `ExceptionHandlingMiddleware` change / ProblemDetails rework** — Phase 21 owns the error contract;
  this card parses the shape that exists today.
- Do not write files owned by siblings: anything under `api/` (20-1, 20-2); `ui/src/views/HomeView.vue`,
  `ui/src/lib/wellness.ts`, `ui/src/components/dashboard/*` including `RestingHrCard.vue` (20-4).
- **Do not fix** the two pre-existing nullable warnings in
  `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

### `ui/src/components/common/__tests__/ScaleSelector.spec.ts` (new)
- `renders 1 through 10 by default` — button texts equal `['1',…,'10']`.
- `renders 1 through 5 when max is 5` — button texts equal `['1','2','3','4','5']`.
- `uses a literal grid-cols-5 class when max is 5` — the button row's `classes()` contains
  `'grid-cols-5'`; and `uses a literal grid-cols-10 class by default` contains `'grid-cols-10'`. These
  two are the Tailwind guard: an interpolated class would fail them.
- `emits the tapped value` — clicking `'3'` emits `[[3]]`.
- `marks the selected value as pressed` — `modelValue: 4` → exactly one `button[aria-pressed="true"]`
  whose text is `'4'`.
- `renders no label row when labels is null` — the component's text does not contain any of the RPE
  labels; `renders the three labels when provided` — text contains `Poor`, `OK`, `Great`.

### `ui/src/components/common/__tests__/RpeSelector.spec.ts` — **unchanged**
Do not edit, do not add cases. Its three tests passing against the wrapper is the acceptance signal.

### `ui/src/services/__tests__/wellness.spec.ts` (new)
`vi.spyOn(globalThis, 'fetch')` with a local `jsonResponse()` helper, mirroring
`ui/src/services/__tests__/training.spec.ts:1–50`.
- `putWellness PUTs /api/v1/wellness/{date} with the metric body` — assert the URL is
  `/api/v1/wellness/2026-07-26`, `init.method === 'PUT'`, and `JSON.parse(String(init.body))` equals the
  request object (including its explicit `null`s — the server clears them).
- `getWellnessRange builds the from/to query string` — URL is
  `/api/v1/wellness?from=2026-07-13&to=2026-07-26`.
- `getWellnessRange returns [] when the body is null` (a 204).
- `getWellnessSummary GETs /api/v1/wellness/summary`.
- `putWellness throws ApiError for a 400` — the card depends on the error surviving.

### `ui/src/stores/__tests__/wellness.spec.ts` (new)
`createPinia`/`setActivePinia` with `vi.mock('@/services/wellness')`.
- `loadToday assigns the single row returned for today` — and the range call used the same date for
  `from` and `to`.
- `loadToday leaves today null when the range comes back empty`.
- `loadSummary assigns the summary and clears error`.
- `saveToday PUTs today's date and re-fetches both reads` — assert `putWellness` was called with
  `utcTodayIso()`-shaped date, and that `getWellnessRange` + `getWellnessSummary` were each called again
  after it.
- `saveToday re-throws an ApiError and clears saving` — `await expect(...).rejects.toBeInstanceOf(ApiError)`
  and `store.saving === false`.

### `ui/src/components/wellness/__tests__/WellnessQuickEntryCard.spec.ts` (new)
`createTestingPinia({ createSpy: … })` with `initialState` on the `wellness` store, mounted
`attachTo: document.body` (the pattern `RestingHrCard.spec.ts` uses).
- `renders the collapsed prompt when today has no entry` — text contains `No wellness logged today.` and
  a `Log today` button; no form inputs are rendered.
- `renders today's values in the collapsed summary when an entry exists` — text contains `7.5`, `48`,
  `72.4`.
- `expands to the form when the button is clicked` — after `await` the click, the sleep-hours input
  exists.
- `renders a 5-button sleep-quality scale and a 10-button soreness scale` — count the buttons inside each
  `ScaleSelector` (proves the `max` prop is wired, not defaulted).
- `submits the entered metrics through the store` — fill sleep hours `7.5`, submit, **~6
  `flushPromises()`** (refined-array/refined-object schemas need the extra ticks), assert
  `store.saveToday` was called once with an object whose `sleepHours === 7.5` and whose untouched
  metrics are `null`.
- `does not submit when every metric is blank` — submit with only `notes` filled; `store.saveToday` was
  **not** called and the `Enter at least one metric` message renders.
- `maps a field-prefixed server error onto its field` — `saveToday` rejects with
  `new ApiError(400, 'Bad Request', { errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'] })`
  → after flushes, that message is rendered.
- `renders an unmapped server message in the form-level error` — the same with
  `['Entry: At least one metric is required.']`.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
Vitest must **rise** from the **288 / 61 files** baseline by roughly 25 tests across 4 new files, with
zero failures — and `ui/src/components/common/__tests__/RpeSelector.spec.ts` must still report its
**3** passing tests without having been edited. `pnpm run build` (`vue-tsc -b && vite build`) must stay
green; the new types are type-checked there. xUnit must stay **exactly** where Tasks 20-1 and 20-2 left
it (the **343** baseline plus their additions) because this task touches no backend file, and backend
warnings must stay at **16** on a clean compile. If the transient Vitest worker-fork crash appears with
all tests passing, re-run once before debugging (project memory).

## Review checklist
- [ ] `RpeSelector.vue` is a wrapper with the same props/emits; `LogWorkoutForm.vue` and
      `RpeSelector.spec.ts` are absent from `git diff` and the three RPE specs pass unchanged.
- [ ] `ScaleSelector` writes `grid-cols-5` and `grid-cols-10` as **literal** strings, with specs
      asserting both.
- [ ] The button markup, including the selected-state gradient classes and `aria-pressed`, was moved
      verbatim — no visual redesign rode along.
- [ ] Every HTTP call goes through `src/services/`; state lives in Pinia; every SFC is
      `<script setup lang="ts">` with typed `defineProps`/`defineEmits`.
- [ ] `saveToday` re-fetches both reads and re-throws, and the card maps field-prefixed server messages
      onto vee-validate fields.
- [ ] The form's bounds match Task 20-2's validator exactly (0–16 / 1–5 / 25–120 / 30–250 / 1–10 /
      10–250, notes ≤ 1000) and notes alone does not satisfy the at-least-one-metric rule.
- [ ] `git diff --stat` shows **nothing under `api/`**, no `HomeView.vue`, no
      `ui/src/components/dashboard/*`, no `ui/src/lib/wellness.ts`, no `router/index.ts`, no
      `AppSidebar.vue`, no `MetricTile.vue` / `Sparkline.vue` / `DeltaChip.vue`.
- [ ] No new npm package in `ui/package.json`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
