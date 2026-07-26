# Impl 18-4 — Build order: Periodization panel on plan detail (edit form + target ramp)

**Executor:** the architect-implementer. **Acceptance contract:** `md/Tasks-18-4.md`. **Decision lock:**
ADR-0009 §5 (orphan-window policy — the `PlanWindow:` 400 this form must surface verbatim) and §6
(`RecoveryWeekPercentage` is percent-scale, 30–90; ADR-0009 is written by Task 18-1 and referenced here
without a code dependency on it).
**Scope:** Frontend only. No backend change, no migration, no new npm package, no new shadcn-vue
primitive. Depends on Task 18-2 (`PUT /api/v1/trainingplans/{id}`) and Task 18-3
(`GET /api/v1/trainingplans/{id}/weekly-targets`) — **Step 0 verifies both endpoints respond** before any
file is touched.

## Step 0 — Pre-flight

- `git status` clean on `main`. `pnpm run build` (from `ui/`) green; `pnpm exec vitest run
  --no-file-parallelism` green — record the baseline count (Vitest **229 / 53 files** per
  `Tasks-18-4.md`; must rise, never fall, by the end of this task).
- **Confirm 18-2 and 18-3 are actually merged** — this task cannot proceed without them:
  - Backend: with the dev API running (`dotnet run` from `api/Bryk.API`, `https://localhost:60129`),
    smoke-check `PUT /api/v1/trainingplans/{id}` (a metadata-only body: `name`, `methodology`,
    `startDate`, `endDate`, `eventId`, `buildWeeks`, `recoveryWeeks`, `recoveryWeekPercentage`) and
    `GET /api/v1/trainingplans/{id}/weekly-targets` (`planId`, `startDate`, `endDate`, `baseline`,
    `baselineSource`, `weeks[]` each with `weekStart`/`targetLoad`/`isRecoveryWeek`/`isTaperWeek`/
    `plannedLoad`/`actualLoad`) against a seeded plan. Confirm exact camelCase field names (.NET's
    default JSON casing) before writing types.
  - Confirm `api/Bryk.Application/Training/TrainingPlanUpdateRequest.cs` and
    `api/Bryk.Application/Training/Periodization/PeriodizationService.cs` exist in the tree.
  - If either endpoint is missing or shaped differently than `Tasks-18-2.md`/`Tasks-18-3.md` describe,
    **STOP** — do not reimplement 18-2/18-3 inline; flag the gap and wait.
- Re-read `md/Tasks-18-4.md` in full. Open in the editor (read-only unless listed as an edit target
  below): `ui/src/views/PlanDetailView.vue`, `ui/src/components/charts/LoadChart.vue`,
  `ui/src/lib/charts/load.ts`, `ui/src/components/charts/LoadChartSection.vue`,
  `ui/src/components/goals/GoalsEventForm.vue`, `ui/src/views/TrainingView.vue`,
  `ui/src/schemas/training.ts`, `ui/src/services/training.ts`, `ui/src/stores/training.ts`,
  `ui/src/stores/goals.ts`, `ui/src/types/training.ts`, `ui/src/types/goals.ts`,
  `ui/src/services/__tests__/events.spec.ts`, `ui/src/views/__tests__/PlanDetailView.spec.ts`,
  `ui/src/components/goals/__tests__/GoalsEventForm.spec.ts`.
- **Note the shared-file lock:** `ui/src/types/training.ts` is also touched by Task 18-5. Land 18-4
  first, in one session, before 18-5 starts.

## Step 1 — Types (`ui/src/types/training.ts`, additive)

**Edit.** Append after the existing `TrainingPlanResponse` (end of file, `training.ts:182`) — do not
touch `TrainingPlanRequest` or `TrainingPlanResponse`:

```ts
// Mirrors Bryk.Application.Training.TrainingPlanUpdateRequest (Task 18-2). Metadata only —
// planned workouts are edited through their own endpoints. recoveryWeekPercentage is percent-scale
// (60 = 60% of a build week, ADR-0009 §6); eventId null clears the link.
export interface TrainingPlanUpdateRequest {
  name: string
  methodology: MethodologyChoice
  startDate: string
  endDate: string
  eventId: string | null
  buildWeeks: number | null
  recoveryWeeks: number | null
  recoveryWeekPercentage: number | null
}

// Mirrors Bryk.Application.Training.Periodization.* (Task 18-3).
export type TargetBaselineSource = 'None' | 'TrailingActual' | 'FirstWeekPlanned'

export interface WeeklyTargetWeek {
  weekStart: string
  targetLoad: number
  isRecoveryWeek: boolean
  isTaperWeek: boolean
  plannedLoad: number
  actualLoad: number
}

export interface WeeklyTargetsResponse {
  planId: string
  startDate: string
  endDate: string
  baseline: number | null
  baselineSource: TargetBaselineSource
  weeks: WeeklyTargetWeek[]
}
```

`MethodologyChoice` is already imported at the top of the file (line 1) — no new import needed.

**Verify:** `pnpm run build` green (type-checks; no consumers yet).

## Step 2 — Service additions (`ui/src/services/training.ts`)

**Edit.** Add two functions at the end of the file, after `deleteWorkout`, mirroring `updateWorkout`'s
and `getPlan`'s null-guard style exactly:

```ts
// Plan-metadata edit (Task 18-2). Planned workouts are untouched server-side; the response still
// carries them (the service re-attaches the plan's existing children for the projection).
export async function updatePlan(id: string, req: TrainingPlanUpdateRequest): Promise<TrainingPlanResponse> {
  const result = await apiFetch<TrainingPlanResponse>(`/trainingplans/${id}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  })
  if (result === null) {
    throw new Error('Unexpected empty response from PUT /trainingplans/{id}')
  }
  return result
}

// Weekly load targets (Task 18-3). No query params — the plan's own window is the range.
export async function getWeeklyTargets(id: string): Promise<WeeklyTargetsResponse> {
  const result = await apiFetch<WeeklyTargetsResponse>(`/trainingplans/${id}/weekly-targets`)
  if (result === null) {
    throw new Error('Unexpected empty response from GET /trainingplans/{id}/weekly-targets')
  }
  return result
}
```

Add `TrainingPlanUpdateRequest` and `WeeklyTargetsResponse` to the existing `import type { ... } from
'@/types/training'` block at the top of the file (do not add a second `import type` statement).

**Verify:** `pnpm run build` green.

## Step 3 — Service spec (`ui/src/services/__tests__/training.spec.ts`, new)

Mirror `services/__tests__/events.spec.ts`'s shape (spy on `globalThis.fetch`, assert URL + method +
parsed body), scoped to just the two new functions:

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { updatePlan, getWeeklyTargets } from '@/services/training'
import type { TrainingPlanUpdateRequest, TrainingPlanResponse, WeeklyTargetsResponse } from '@/types/training'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

const updateReq: TrainingPlanUpdateRequest = {
  name: 'Renamed Plan',
  methodology: 'Polarized',
  startDate: '2026-06-08',
  endDate: '2026-08-03',
  eventId: null,
  buildWeeks: 3,
  recoveryWeeks: 1,
  recoveryWeekPercentage: 60,
}

describe('training service — periodization additions', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('updatePlan PUTs the metadata body to /trainingplans/{id}', async () => {
    const updated: TrainingPlanResponse = { id: 'p1', ...updateReq, plannedWorkouts: [] }
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(updated))

    const result = await updatePlan('p1', updateReq)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/trainingplans/p1`)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(init?.body as string)).toEqual(updateReq)
    expect(result).toEqual(updated)
  })

  it('getWeeklyTargets GETs /trainingplans/{id}/weekly-targets', async () => {
    const payload: WeeklyTargetsResponse = {
      planId: 'p1',
      startDate: '2026-06-08',
      endDate: '2026-08-03',
      baseline: 200,
      baselineSource: 'TrailingActual',
      weeks: [],
    }
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(payload))

    const result = await getWeeklyTargets('p1')

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/trainingplans/p1/weekly-targets`)
    expect(init?.method ?? 'GET').toBe('GET')
    expect(result).toEqual(payload)
  })
})
```

**Verify:** `pnpm exec vitest run ui/src/services/__tests__/training.spec.ts --no-file-parallelism`
green.

## Step 4 — Store slice (`ui/src/stores/training.ts`, additive)

**Edit.** Insert next to the plan-browser block (`currentPlan`/`loadPlan`, currently lines 72–87), after
`loadPlan`'s closing brace and before the "Structured-workout payload" section comment:

```ts
  // ── Periodization panel (Task 18-4) — weekly targets + the metadata PUT ──
  const weeklyTargets = ref<WeeklyTargetsResponse | null>(null)
  const loadingTargets = ref(false)
  const targetsError = ref<ApiError | Error | null>(null)

  async function loadWeeklyTargets(id: string) {
    loadingTargets.value = true
    targetsError.value = null
    weeklyTargets.value = null
    try {
      weeklyTargets.value = await getWeeklyTargetsApi(id)
    } catch (e) {
      targetsError.value = e as ApiError | Error
    } finally {
      loadingTargets.value = false
    }
  }

  // Metadata edit. Assigns the PUT response onto currentPlan (it already carries the plan's
  // existing planned workouts — the service re-attaches them for the projection, Task 18-2), then
  // re-loads the target ramp, since a window/cadence/event change reshapes it entirely. Does NOT
  // call loadPlan again. Re-throws so the form can map the 400 (mirrors updateWorkout).
  async function updatePlan(id: string, req: TrainingPlanUpdateRequest): Promise<TrainingPlanResponse> {
    const updated = await updatePlanApi(id, req)
    currentPlan.value = updated
    await loadWeeklyTargets(id)
    return updated
  }
```

Add the two service imports to the existing `import { ... } from '@/services/training'` block:

```ts
  updatePlan as updatePlanApi,
  getWeeklyTargets as getWeeklyTargetsApi,
```

Add `TrainingPlanUpdateRequest` and `WeeklyTargetsResponse` to the existing `import type { ... } from
'@/types/training'` block.

Add all four new members to the `return { ... }` object (lines 242–279), next to the existing
`currentPlan`/`loadingPlan`/`planError`/`loadPlan` group:

```ts
    weeklyTargets,
    loadingTargets,
    targetsError,
    loadWeeklyTargets,
    updatePlan,
```

Do not touch any existing action, ref, or return-object member (including the unrelated `createPlan`,
which already has its own `TrainingPlanRequest` import — do not rename or collide with it).

**Verify:** `pnpm run build` green (type-check on the new imports/actions/return members). No store spec
for this task — the component spec (Step 8) asserts the store contract through `createTestingPinia`
spies; `stores/training.ts` has no dedicated spec file today and this task does not add one (say so in
the commit body, per `Tasks-18-4.md`).

## Step 5 — Schema addition (`ui/src/schemas/training.ts`, additive)

**Edit.** Append after `trainingPlanSchema`/`TrainingPlanFormValues` (after line 44), before the
"Structured-workout builder" section comment. Reuse the file's own local `optionalNumber` helper
(lines 6–8) — do not export or move it, do not re-derive `trainingPlanSchema`:

```ts
// Periodization panel (Task 18-4). Bounds mirror 18-2's TrainingPlanUpdateRequestValidator exactly
// (1–8 build weeks, ≥1 recovery weeks, 30–90 percent recovery volume — ADR-0009 §6). No cross-field
// "all three or none" rule: a partial cadence is legal (ADR-0009 §2).
export const planMetadataSchema = z
  .object({
    name: z.string().min(1, 'Plan name is required').max(200, 'Name must be 200 characters or fewer'),
    methodology: z.enum(['Pyramidal', 'Periodization', 'Polarized', 'Norwegian'], {
      message: 'Please select a methodology',
    }),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    eventId: z.string(),
    buildWeeks: optionalNumber(z.coerce.number().int('Whole number').gte(1, 'Must be at least 1').lte(8, 'Must be 8 or fewer')),
    recoveryWeeks: optionalNumber(z.coerce.number().int('Whole number').gte(1, 'Must be at least 1')),
    recoveryWeekPercentage: optionalNumber(z.coerce.number().gte(30, 'Must be at least 30').lte(90, 'Must be 90 or less')),
  })
  .refine((d) => !d.startDate || !d.endDate || d.endDate >= d.startDate, {
    message: 'End date must be on or after start date',
    path: ['endDate'],
  })

export type PlanMetadataFormValues = z.infer<typeof planMetadataSchema>
```

**Verify:** `pnpm run build` green.

## Step 6 — `PeriodizationPanel.vue`: scaffold + read summary (new file, part 1)

**New file** `ui/src/components/training/PeriodizationPanel.vue`. Start with the script setup, props,
mount effects, and the always-visible read summary; the edit form and target ramp land in Steps 7–8 in
the same file.

```vue
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Button } from '@/components/ui/button'
import { useTrainingStore } from '@/stores/training'
import { useGoalsStore } from '@/stores/goals'
import type { TrainingPlanResponse } from '@/types/training'

const props = defineProps<{ plan: TrainingPlanResponse }>()

const store = useTrainingStore()
const goalsStore = useGoalsStore()

onMounted(() => {
  void store.loadWeeklyTargets(props.plan.id)
  if (!goalsStore.events) void goalsStore.loadAll()
})

// Local copy of PlanDetailView's UTC-safe short-date formatter — that view's own `formatDay`
// stays private/unexported (its planned-workout list still uses it), so this panel keeps its own.
function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
}

const linkedEventName = computed(() => {
  if (!props.plan.eventId) return 'No target event'
  const ev = goalsStore.events?.find((e) => e.id === props.plan.eventId)
  return ev ? ev.name : 'No target event'
})

const cadenceLine = computed(() => {
  const { buildWeeks, recoveryWeeks, recoveryWeekPercentage } = props.plan
  if (buildWeeks == null || recoveryWeeks == null || recoveryWeekPercentage == null) {
    return 'No cadence set'
  }
  return `${buildWeeks} build : ${recoveryWeeks} recovery · ${recoveryWeekPercentage}% recovery volume`
})

const editing = ref(false)
</script>

<template>
  <div class="card-surface p-6">
    <div class="flex items-start justify-between gap-4">
      <div>
        <h2 class="text-lg font-semibold">{{ plan.name }}</h2>
        <p class="mt-1 flex flex-wrap gap-x-3 font-mono text-[12px] text-muted-foreground">
          <span>{{ plan.methodology }}</span>
          <span>{{ formatDay(plan.startDate) }} – {{ formatDay(plan.endDate) }}</span>
          <span>{{ linkedEventName }}</span>
        </p>
        <p class="mt-1 font-mono text-[12px] text-muted-foreground">{{ cadenceLine }}</p>
      </div>
      <Button type="button" variant="outline" size="sm" @click="editing = !editing">
        {{ editing ? 'Cancel' : 'Edit' }}
      </Button>
    </div>

    <!-- Edit form — Step 7 -->

    <!-- Target ramp section — Step 8 -->
  </div>
</template>
```

**Verify:** `pnpm run build` green (component compiles standalone; no consumer yet, so this only checks
types).

## Step 7 — `PeriodizationPanel.vue`: edit form (same file, part 2)

**Edit** the file from Step 6. Add the form imports, `useForm` setup, and submit/mapping logic to the
`<script setup>` block, and the form markup between the summary block and the "Target ramp section"
comment.

Script additions (after the existing imports and before `onMounted`, matching `GoalsEventForm.vue`'s
ordering):

```ts
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { CheckCircle2 } from 'lucide-vue-next'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { planMetadataSchema, type PlanMetadataFormValues } from '@/schemas/training'
import type { TrainingPlanUpdateRequest } from '@/types/training'
```

After `const editing = ref(false)`:

```ts
function fromPlan(plan: TrainingPlanResponse): PlanMetadataFormValues {
  return {
    name: plan.name,
    methodology: plan.methodology,
    startDate: plan.startDate,
    endDate: plan.endDate,
    eventId: plan.eventId ?? '',
    buildWeeks: plan.buildWeeks,
    recoveryWeeks: plan.recoveryWeeks,
    recoveryWeekPercentage: plan.recoveryWeekPercentage,
  }
}

const form = useForm<PlanMetadataFormValues>({
  validationSchema: toTypedSchema(planMetadataSchema),
  initialValues: fromPlan(props.plan),
})

const methodologyOptions = [
  { value: 'Pyramidal', label: 'Pyramidal' },
  { value: 'Periodization', label: 'Periodization' },
  { value: 'Polarized', label: 'Polarized' },
  { value: 'Norwegian', label: 'Norwegian' },
]

const globalError = ref<string | null>(null)
const justSaved = ref(false)

function setError(e: unknown) {
  const messages = extractApiValidationMessages(e)
  if (messages) {
    globalError.value = messages.join(' ')
  } else if (e instanceof ApiError) {
    globalError.value = e.status === 404
      ? 'This plan no longer exists — it may have been removed.'
      : `Couldn't save: ${e.statusText} (${e.status})`
  } else if (e instanceof Error) {
    globalError.value = `Couldn't save: ${e.message}`
  } else {
    globalError.value = "Couldn't save — please try again."
  }
}

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const req: TrainingPlanUpdateRequest = {
    name: values.name,
    methodology: values.methodology,
    startDate: values.startDate,
    endDate: values.endDate,
    eventId: values.eventId || null,
    buildWeeks: values.buildWeeks ?? null,
    recoveryWeeks: values.recoveryWeeks ?? null,
    recoveryWeekPercentage: values.recoveryWeekPercentage ?? null,
  }
  try {
    await store.updatePlan(props.plan.id, req)
    form.resetForm({ values })
    justSaved.value = true
    editing.value = false
  } catch (e) {
    setError(e)
  }
})

const isSubmitting = form.isSubmitting
```

Template — replace the `<!-- Edit form — Step 7 -->` comment with:

```html
<form v-if="editing" class="mt-6 space-y-4 border-t border-border pt-6" @submit="onSubmit">
  <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
    <FormField v-slot="{ componentField }" name="name">
      <FormItem>
        <FormLabel>Name</FormLabel>
        <FormControl>
          <Input v-bind="componentField" />
        </FormControl>
        <FormMessage />
      </FormItem>
    </FormField>

    <FormField v-slot="{ componentField }" name="methodology">
      <FormItem>
        <FormLabel>Methodology</FormLabel>
        <Select v-bind="componentField">
          <FormControl>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
          </FormControl>
          <SelectContent>
            <SelectItem v-for="opt in methodologyOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </SelectItem>
          </SelectContent>
        </Select>
        <FormMessage />
      </FormItem>
    </FormField>
  </div>

  <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
    <FormField v-slot="{ componentField }" name="startDate">
      <FormItem>
        <FormLabel>Start date</FormLabel>
        <FormControl>
          <Input type="date" v-bind="componentField" />
        </FormControl>
        <FormMessage />
      </FormItem>
    </FormField>

    <FormField v-slot="{ componentField }" name="endDate">
      <FormItem>
        <FormLabel>End date</FormLabel>
        <FormControl>
          <Input type="date" v-bind="componentField" />
        </FormControl>
        <FormMessage />
      </FormItem>
    </FormField>
  </div>

  <FormField v-slot="{ componentField }" name="eventId">
    <FormItem>
      <FormLabel>Target event</FormLabel>
      <Select v-bind="componentField">
        <FormControl>
          <SelectTrigger>
            <SelectValue placeholder="No target event" />
          </SelectTrigger>
        </FormControl>
        <SelectContent>
          <SelectItem value="">No target event</SelectItem>
          <SelectItem v-for="ev in goalsStore.events ?? []" :key="ev.id" :value="ev.id">
            {{ ev.name }}
          </SelectItem>
        </SelectContent>
      </Select>
      <FormMessage />
    </FormItem>
  </FormField>

  <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
    <FormField v-slot="{ componentField }" name="buildWeeks">
      <FormItem>
        <FormLabel>Build weeks</FormLabel>
        <FormControl>
          <Input type="number" min="1" max="8" v-bind="componentField" />
        </FormControl>
        <FormMessage />
      </FormItem>
    </FormField>

    <FormField v-slot="{ componentField }" name="recoveryWeeks">
      <FormItem>
        <FormLabel>Recovery weeks</FormLabel>
        <FormControl>
          <Input type="number" min="1" v-bind="componentField" />
        </FormControl>
        <FormMessage />
      </FormItem>
    </FormField>

    <FormField v-slot="{ componentField }" name="recoveryWeekPercentage">
      <FormItem>
        <FormLabel>Recovery volume (% of a build week)</FormLabel>
        <FormControl>
          <Input type="number" min="30" max="90" v-bind="componentField" />
        </FormControl>
        <FormMessage />
      </FormItem>
    </FormField>
  </div>

  <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
    {{ globalError }}
  </div>

  <div class="flex items-center justify-end gap-3">
    <span v-if="justSaved" class="flex items-center gap-1 text-sm text-muted-foreground">
      <CheckCircle2 :size="16" class="text-primary" />
      Saved
    </span>
    <Button type="submit" :disabled="isSubmitting">Save</Button>
  </div>
</form>
```

Note the picker lists **all** `goalsStore.events` (not `upcomingEvents`) so a plan linked to a past race
keeps its selection — matches `Tasks-18-4.md`'s explicit fence. `eventId: values.eventId || null` is the
same mapping `TrainingView.vue:149` uses for plan creation.

**Verify:** `pnpm run build` green.

## Step 8 — `PeriodizationPanel.vue`: target ramp section (same file, part 3)

**Edit** the file again. Add the chart import and the `chartWeeks`/`sourceLabel` computeds to
`<script setup>`, and replace the `<!-- Target ramp section — Step 8 -->` comment with the ramp markup.

Script addition:

```ts
import LoadChart from '@/components/charts/LoadChart.vue'
import type { WeeklyLoadWeek } from '@/types/analytics'

const weeks = computed(() => store.weeklyTargets?.weeks ?? [])

// Adapter onto LoadChart's existing planned/actual/rollingAverage channels — LoadChart.vue and
// lib/charts/load.ts are NOT modified. Targets take the hatched "planned" bar; the dashed trend
// traces the ramp itself (not a 4-week average here); optimalBand is null because the plan's own
// targets replace the ACWR band in this context.
const chartWeeks = computed<WeeklyLoadWeek[]>(() =>
  weeks.value.map((w) => ({
    weekStart: w.weekStart,
    plannedLoad: w.targetLoad,
    actualLoad: w.actualLoad,
    rollingAverage: w.targetLoad,
  })),
)

const sourceLabel = computed(() => {
  const source = store.weeklyTargets?.baselineSource
  if (source === 'TrailingActual') return 'your last 4 weeks'
  if (source === 'FirstWeekPlanned') return "this plan's first week"
  return ''
})
```

Template (replacing the ramp-section comment):

```html
<section class="mt-6 border-t border-border pt-6">
  <header class="flex flex-col">
    <h3 class="text-sm font-semibold">Weekly target ramp</h3>
    <span v-if="store.weeklyTargets?.baseline != null" class="eyebrow text-faint">
      Ramping from {{ store.weeklyTargets.baseline }} TSS/wk · {{ sourceLabel }}
    </span>
  </header>

  <p v-if="store.loadingTargets && !store.weeklyTargets" class="py-10 text-center text-sm text-muted-foreground">
    Loading…
  </p>

  <p v-else-if="weeks.length === 0" class="py-10 text-center text-sm text-muted-foreground">
    No targets yet — log four weeks of training or plan your first week, and the ramp appears.
  </p>

  <template v-else>
    <!-- lib/charts/load.ts:65 always labels the last bar "· NOW" — here that's the plan's final
         week, not necessarily the current week. Known cosmetic artifact in this context; forking
         load.ts/LoadChart.vue is out of scope (Tasks-18-4.md) — documented, not fixed. -->
    <LoadChart :weeks="chartWeeks" :optimal-band="null" />

    <!-- Accessible legend (the SVG is aria-hidden). -->
    <div class="mt-2 flex flex-wrap gap-4 font-mono text-[11px] text-muted-foreground">
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-fg-3)" />
        Target
      </span>
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-accent-hi)" />
        Actual
      </span>
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-warn)" />
        Ramp
      </span>
    </div>

    <!-- Accessible week strip — the assertable rendering of the cadence (the chart above is aria-hidden). -->
    <ul class="mt-4 divide-y divide-border font-mono text-xs">
      <li v-for="w in weeks" :key="w.weekStart" class="flex items-center justify-between gap-3 py-2">
        <span class="text-muted-foreground">{{ formatDay(w.weekStart) }}</span>
        <span>{{ w.targetLoad }} TSS</span>
        <span
          v-if="w.isRecoveryWeek"
          class="inline-flex items-center rounded-full border border-border-strong bg-muted px-2 py-0.5 text-[10px] uppercase tracking-[0.08em] text-subtle"
        >
          Recovery
        </span>
        <span
          v-else-if="w.isTaperWeek"
          class="inline-flex items-center rounded-full border border-primary-lo bg-primary-glow px-2 py-0.5 text-[10px] uppercase tracking-[0.08em] text-primary-hi"
        >
          Taper
        </span>
      </li>
    </ul>
  </template>
</section>
```

**Verify:** `pnpm run build` green. Every SFC touched so far is `<script setup lang="ts">`; no
`fetch`/`axios` outside `src/services/`.

## Step 9 — `PeriodizationPanel.spec.ts` (new)

**New file** `ui/src/components/training/__tests__/PeriodizationPanel.spec.ts`. Mount with
`createTestingPinia({ createSpy: vi.fn, initialState: { training: { currentPlan, weeklyTargets },
goals: { events } } })`, stubbing `RouterLink` where needed. Use the `await vi.waitFor(...)` idiom from
`GoalsEventForm.spec.ts` for valid-submit assertions, **not** a fixed `flushPromises()` count (per the
repo's memory note: refined-array/object zod schemas need more microtask hops than a guessed loop).

```ts
import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import PeriodizationPanel from '@/components/training/PeriodizationPanel.vue'
import LoadChart from '@/components/charts/LoadChart.vue'
import { useTrainingStore } from '@/stores/training'
import { ApiError } from '@/services/api'
import type { TrainingPlanResponse, WeeklyTargetsResponse } from '@/types/training'
import type { EventListItem } from '@/types/goals'

const plan: TrainingPlanResponse = {
  id: 'p1',
  name: 'Spring Base',
  methodology: 'Polarized',
  startDate: '2026-06-08',
  endDate: '2026-08-03',
  eventId: null,
  buildWeeks: 3,
  recoveryWeeks: 1,
  recoveryWeekPercentage: 60,
  plannedWorkouts: [],
}

const events: EventListItem[] = [
  {
    id: 'ev1',
    name: 'Boston Marathon',
    eventDate: '2026-09-01',
    sport: 'Run',
    triathlonDistance: null,
    customDistanceName: null,
    priority: 'A',
    notes: null,
    linkedPlans: [],
  },
]

function targets(overrides: Partial<WeeklyTargetsResponse> = {}): WeeklyTargetsResponse {
  return {
    planId: 'p1',
    startDate: '2026-06-08',
    endDate: '2026-08-03',
    baseline: 200,
    baselineSource: 'TrailingActual',
    weeks: [
      { weekStart: '2026-06-08', targetLoad: 200, isRecoveryWeek: false, isTaperWeek: false, plannedLoad: 190, actualLoad: 180 },
      { weekStart: '2026-06-15', targetLoad: 214, isRecoveryWeek: false, isTaperWeek: false, plannedLoad: 210, actualLoad: 200 },
      { weekStart: '2026-06-22', targetLoad: 229, isRecoveryWeek: false, isTaperWeek: false, plannedLoad: 220, actualLoad: 210 },
      { weekStart: '2026-06-29', targetLoad: 137, isRecoveryWeek: true, isTaperWeek: false, plannedLoad: 130, actualLoad: 120 },
      { weekStart: '2026-07-06', targetLoad: 245, isRecoveryWeek: false, isTaperWeek: false, plannedLoad: 240, actualLoad: 230 },
      { weekStart: '2026-07-27', targetLoad: 172, isRecoveryWeek: false, isTaperWeek: true, plannedLoad: 160, actualLoad: 150 },
      { weekStart: '2026-08-03', targetLoad: 172, isRecoveryWeek: false, isTaperWeek: true, plannedLoad: 160, actualLoad: 150 },
    ],
    ...overrides,
  }
}

function mountPanel(planFixture: TrainingPlanResponse, weeklyTargets: WeeklyTargetsResponse | null) {
  const wrapper = mount(PeriodizationPanel, {
    props: { plan: planFixture },
    global: {
      plugins: [
        createTestingPinia({
          createSpy: vi.fn,
          initialState: { training: { currentPlan: planFixture, weeklyTargets }, goals: { events } },
        }),
      ],
      stubs: { RouterLink: true },
    },
    attachTo: document.body,
  })
  return { wrapper, store: useTrainingStore() }
}

describe('PeriodizationPanel', () => {
  it('renders the plan metadata summary and the cadence line', () => {
    const { wrapper } = mountPanel(plan, targets())
    expect(wrapper.text()).toContain('Spring Base')
    expect(wrapper.text()).toContain('Polarized')
    expect(wrapper.text()).toContain('3 build : 1 recovery')
    expect(wrapper.text()).toContain('60%')
  })

  it('renders "No cadence set" when the periodization fields are null', () => {
    const noCadence = { ...plan, buildWeeks: null, recoveryWeeks: null, recoveryWeekPercentage: null }
    const { wrapper } = mountPanel(noCadence, targets())
    expect(wrapper.text()).toContain('No cadence set')
  })

  it('renders the linked event name when eventId matches a loaded event, else "No target event"', () => {
    const { wrapper: unlinked } = mountPanel(plan, targets())
    expect(unlinked.text()).toContain('No target event')

    const linked = { ...plan, eventId: 'ev1' }
    const { wrapper: linkedWrapper } = mountPanel(linked, targets())
    expect(linkedWrapper.text()).toContain('Boston Marathon')
  })

  it('passes targets to LoadChart on the planned channel with a null band', () => {
    const { wrapper } = mountPanel(plan, targets())
    const chart = wrapper.findComponent(LoadChart)
    expect(chart.exists()).toBe(true)
    const passedWeeks = chart.props('weeks') as { plannedLoad: number; rollingAverage: number; actualLoad: number }[]
    expect(passedWeeks[0].plannedLoad).toBe(200)
    expect(passedWeeks[0].rollingAverage).toBe(200)
    expect(passedWeeks[0].actualLoad).toBe(180)
    expect(chart.props('optimalBand')).toBeNull()
  })

  it('renders the honest empty state and no chart when baselineSource is None', () => {
    const { wrapper } = mountPanel(plan, targets({ baseline: null, baselineSource: 'None', weeks: [] }))
    expect(wrapper.text()).toContain('No targets yet')
    expect(wrapper.findComponent(LoadChart).exists()).toBe(false)
  })

  it('badges the recovery and taper weeks in the week strip', () => {
    const { wrapper } = mountPanel(plan, targets())
    const recoveryBadges = wrapper.findAll('*').filter((n) => n.text() === 'Recovery')
    const taperBadges = wrapper.findAll('*').filter((n) => n.text() === 'Taper')
    expect(recoveryBadges.length).toBeGreaterThanOrEqual(1)
    expect(taperBadges.length).toBe(2)
  })

  it('submits mapped metadata through the store', async () => {
    const { wrapper, store } = mountPanel(plan, targets())
    await wrapper.find('button').trigger('click') // Edit
    await wrapper.find('input[name="name"]').setValue('Renamed')
    await wrapper.find('input[name="recoveryWeekPercentage"]').setValue('60')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(store.updatePlan).toHaveBeenCalledWith(
        'p1',
        expect.objectContaining({ name: 'Renamed', recoveryWeekPercentage: 60, eventId: null }),
      ),
    )
  })

  it("surfaces the server's plan-window rejection", async () => {
    const { wrapper, store } = mountPanel(plan, targets())
    vi.mocked(store.updatePlan).mockRejectedValue(
      new ApiError(400, 'Bad Request', { errors: ['PlanWindow: 2 planned workout(s) fall outside …'] }),
    )
    await wrapper.find('button').trigger('click') // Edit
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain('PlanWindow: 2 planned workout(s) fall outside …'),
    )
    expect(wrapper.find('input[name="name"]').exists()).toBe(true) // form stays open
  })

  it('rejects a recovery percentage below 30 without calling the store', async () => {
    const { wrapper, store } = mountPanel(plan, targets())
    await wrapper.find('button').trigger('click') // Edit
    await wrapper.find('input[name="recoveryWeekPercentage"]').setValue('20')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Must be at least 30'))
    expect(store.updatePlan).not.toHaveBeenCalled()
  })
})
```

Adjust selectors (e.g. the "Edit" button lookup) if `wrapper.find('button')` is ambiguous once the ramp
section renders its own buttons — none are expected here, but confirm empirically against the actual
render rather than assuming.

**Verify:**
`pnpm exec vitest run ui/src/components/training/__tests__/PeriodizationPanel.spec.ts
--no-file-parallelism` — all 8 cases green.

## Step 10 — `PlanDetailView.vue` wiring

**Edit** `ui/src/views/PlanDetailView.vue`. Add the import at the top, next to the other component
imports:

```ts
import PeriodizationPanel from '@/components/training/PeriodizationPanel.vue'
```

Replace lines 66–74 — the `<!-- Plan header (read-only; metadata editing is Phase 18) -->` comment and
its `<div class="card-surface p-6">…</div>` block — with:

```html
<PeriodizationPanel :plan="plan" />
```

Nothing else in the file changes: the back-link, `store.loadPlan(id.value)` in `onMounted`, the planned
workouts `<div class="card-surface">` list, `openBuilder`, `WorkoutStructureBuilder`, and the local
`formatDay` (still used by the planned-workout list at line ~95) all stay exactly as they are.

**Verify:** `pnpm run build` green.

## Step 11 — `PlanDetailView.spec.ts` extension

**Edit** `ui/src/views/__tests__/PlanDetailView.spec.ts`. Add `weeklyTargets: null` to the seeded
`training` initial state (the panel's `onMounted` read needs the key present) and one new test:

```ts
import PeriodizationPanel from '@/components/training/PeriodizationPanel.vue'
```

```ts
createTestingPinia({
  createSpy: vi.fn,
  initialState: { training: { currentPlan: plan, weeklyTargets: null } },
}),
```

```ts
it('renders the periodization panel instead of the read-only header', async () => {
  const { wrapper } = await mountView()

  expect(wrapper.findComponent(PeriodizationPanel).exists()).toBe(true)
  expect(wrapper.text()).toContain('Spring Base')

  wrapper.unmount()
})
```

Keep both existing tests (`renders the plan header and its planned workouts`,
`reopens the structure builder for a planned workout`) passing unchanged — they assert on rendered text
(`'Spring Base'`, `'Threshold 4x8'`, `'Bike'`), which `PeriodizationPanel`/the planned-workout list still
produce.

**Verify:** `pnpm exec vitest run ui/src/views/__tests__/PlanDetailView.spec.ts --no-file-parallelism`
— all 3 cases green.

## Step 12 — Full verification + manual smoke + commit

- `pnpm run build` (runs `vue-tsc -b && vite build`) green — a type error in the new interfaces fails
  the build, which is the point.
- `pnpm exec vitest run --no-file-parallelism` — full suite green. Vitest must **rise** from the
  **229 / 53 files** baseline (Step 0) with zero failures. If the known transient worker-fork crash
  appears with every test reporting passed, re-run once before treating it as real (per
  `Tasks-18-4.md` / the repo's vitest-worker-crash-transient note).
- `dotnet build api/Bryk.sln` and `dotnet test api/Bryk.sln` — green, and the count is **unchanged**
  from the post-18-3 baseline (no backend file in this task's diff).
- **Runtime browser check** (not just the build): start the dev stack —
  `dotnet run` from `api/Bryk.API` (`https://localhost:60129`) and, in a second shell, `pnpm dev` from
  `ui/` (proxies `/api` to that port per `ui/vite.config.ts`). Open a seeded plan at `/plans/:id` and
  confirm, with the browser console open:
  - The summary renders (name, methodology, window, linked-event line, cadence line).
  - Clicking **Edit** opens the form pre-filled from the plan, including the event `<Select>` listing
    every event (not just upcoming ones).
  - A valid save round-trips: the summary updates, "Saved" appears, and the target ramp redraws
    (network tab shows the PUT followed by a GET to `weekly-targets`).
  - Shrinking the plan window to strand an existing planned workout surfaces the server's `PlanWindow:`
    message **verbatim** in the form's error banner, and the form stays open (no redirect, no silent
    discard).
  - The console is clean (no Vue warnings, no unhandled rejections) throughout.
- `git diff --stat` — confirm only the expected files changed/added:
  - `ui/src/types/training.ts` (additive)
  - `ui/src/services/training.ts` (additive)
  - `ui/src/services/__tests__/training.spec.ts` (new)
  - `ui/src/stores/training.ts` (additive)
  - `ui/src/schemas/training.ts` (additive)
  - `ui/src/components/training/PeriodizationPanel.vue` (new)
  - `ui/src/components/training/__tests__/PeriodizationPanel.spec.ts` (new)
  - `ui/src/views/PlanDetailView.vue` (edit — lines 66–74 replaced, one import added)
  - `ui/src/views/__tests__/PlanDetailView.spec.ts` (edit)
  - No `package.json` change, no change to `ui/src/components/charts/LoadChart.vue`,
    `ui/src/lib/charts/load.ts`, `ui/src/components/charts/LoadChartSection.vue`,
    `ui/src/views/ProgressView.vue`, `ui/src/stores/analytics.ts`, `ui/src/schemas/training.ts`'s
    `trainingPlanSchema`/`plannedWorkoutItemSchema`, `ui/src/views/TrainingView.vue`, or anything under
    `api/`. If the diff shows any of these, **STOP** — that is scope creep beyond `Tasks-18-4.md`.
- Commit with the message from `Tasks-18-4.md`:

```
feat(ui): periodization panel on plan detail (edit + target ramp)

Replace PlanDetailView's read-only header - and its "metadata editing is
Phase 18" comment - with a Periodization panel: a vee-validate + zod form
over the new plan PUT (name, methodology, window, target event, and the
three ADR-0003 periodization fields that no UI has ever written), plus the
computed weekly target ramp from the weekly-targets endpoint.

The ramp reuses the Phase-15 LoadChart unchanged: targets are adapted onto
its planned channel, per-week actuals onto its actual channel, the trend
line traces the ramp, and the optimal band is null because the plan's own
targets replace it. LoadChart.vue and lib/charts/load.ts are untouched;
the chart's last-bar "NOW" label is a known cosmetic artifact in this
context and is documented, not forked. An athlete with no usable baseline
sees an honest empty state rather than a zeroed chart, and a week strip
badges recovery and taper weeks (the SVG is aria-hidden).

The event picker lists every event, so a plan linked to a past race keeps
its selection, and the empty option maps to null - this is the write path
Phase 17 shipped display-only. Server rejections (PlanWindow:, EventId:)
surface verbatim. Vitest covers the chart mapping, the empty state, the
badges, the submit mapping, the 400 surfacing and the client bounds; the
service spec pins both new URLs.
```
