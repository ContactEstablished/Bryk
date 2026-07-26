# Task 18-4 — Periodization panel on plan detail (edit form + target ramp)

## Surface
Frontend (Vue) only. Types for 18-2's PUT body and 18-3's targets response, two service functions, a
store slice (targets + a plan-update action that re-hydrates `currentPlan`), one zod schema, **one** new
component `ui/src/components/training/PeriodizationPanel.vue`, and the swap of the read-only header
block in `PlanDetailView.vue`. Plus Vitest. **No backend change, no migration, no new npm package, no
new shadcn primitive.**

## Why
`PlanDetailView.vue:66` still carries the marker comment `<!-- Plan header (read-only; metadata editing
is Phase 18) -->`. This is Phase 18. The panel is the only surface where an athlete can set
`buildWeeks` / `recoveryWeeks` / `recoveryWeekPercentage` — fields that have existed since ADR-0003 and
that **no UI has ever written** (`TrainingPlanRequest` in `types/training.ts:110-120` deliberately omits
them) — and the only place the plan↔event link becomes editable, closing Phase 17's display-only
deferral. Rendering the ramp through the **existing** `LoadChart` is a deliberate reuse: the targets map
onto the chart's `plannedLoad` channel and actuals onto `actualLoad`, so the ramp and the athlete's real
weeks are read against the same axis with zero new chart code, exactly as the ROADMAP prescribes
("reusing 15's LoadChart — targets in place of planned hatch").

## Depends on
- **Task 18-2** — `PUT /api/v1/trainingplans/{id}`; the 400 shapes this form must surface
  (`PlanWindow:` on a stranding window, `EventId:` on a foreign event) and the 30–90 percent bounds.
- **Task 18-3** — `GET /api/v1/trainingplans/{id}/weekly-targets` and the `TargetBaselineSource`
  contract (`None` ⇒ empty `weeks`).
- **ADR-0009 §6** — percent scale: the form's field is 30–90, labelled "% of a build week".
- **Phase 13 / Task 13-5** — `PlanDetailView` + `stores/training.ts` `currentPlan`/`loadPlan`.
- **Phase 15 / Task 15-x** — `LoadChart.vue` + `lib/charts/load.ts` (consumed unchanged).
- **Phase 17** — `stores/goals.ts` (`loadAll`, `events`) supplies the event picker's options.
- **Shares `ui/src/types/training.ts` with Task 18-5.** Land 18-4 first; do not edit that file from two
  parallel sessions.

## Required reading
- `ui/src/views/PlanDetailView.vue` — lines 66–74 are the block being replaced; keep everything else
  (back-link, planned-workout list, `WorkoutStructureBuilder` wiring, `formatDay`) untouched.
- `ui/src/components/charts/LoadChart.vue` — props are exactly `{ weeks: WeeklyLoadWeek[]; optimalBand:
  OptimalBand | null }`; it renders the hatched bar from `plannedLoad`, the filled bar from
  `actualLoad`, the dashed trend from `rollingAverage`, and an empty state when `weeks.length === 0`.
  **Consumed as-is; not modified.**
- `ui/src/lib/charts/load.ts` — `buildLoadGeometry` reads only `weekStart`, `plannedLoad`, `actualLoad`,
  `rollingAverage`. Note line 65: the **last** bar is always labelled `· NOW`. **Not modified.**
- `ui/src/components/charts/LoadChartSection.vue` — the section-wrapper pattern (card-surface header +
  chart + accessible legend) to imitate. **Do not reuse this component**: it is bound to
  `useAnalyticsStore` and its own range toggle. The panel gets its own inline `<section>`.
- `ui/src/components/goals/GoalsEventForm.vue` — **the form template**: `useForm` +
  `toTypedSchema(schema)`, `FormField`/`FormItem`/`FormLabel`/`FormControl`/`FormMessage`, `Select` +
  `SelectTrigger`/`SelectValue`/`SelectContent`/`SelectItem`, the `globalError` +
  `extractApiValidationMessages` + `ApiError` error mapping, the `justSaved` flag re-baselined via
  `form.resetForm({ values })`, and the `dirty` watcher.
- `ui/src/views/TrainingView.vue:47–53, 99, 145–150, 218–230` — the existing plan form's event
  `<Select>`: `eventId: ''` initial value, `eventId: values.eventId || null` on submit, `SelectValue
  placeholder="No target event"`. Mirror this mapping exactly.
- `ui/src/schemas/training.ts` — `trainingPlanSchema` (the field set and the `endDate >= startDate`
  `.refine`) and the **local** `optionalNumber` helper at lines 6–8. Reuse the helper in-file; do not
  export it, do not move it.
- `ui/src/services/training.ts` — `getPlan`/`updateWorkout` show the `apiFetch` + null-guard + method
  conventions to copy.
- `ui/src/stores/training.ts` — setup-store style: `ref`s, plain async actions, everything returned in
  one flat object at the end (lines 242–279); `loadPlan` (76–87) and `updateWorkout` (228–233, the
  "re-throw for the form, refresh the surfaces" pattern).
- `ui/src/stores/goals.ts` — `loadAll()` + `events`; `ui/src/types/goals.ts` — `EventListItem`
  (`id`, `name`, `eventDate`, `priority`, …).
- `ui/src/services/__tests__/events.spec.ts` — the service-spec shape (spy on `globalThis.fetch`,
  assert URL + method + parsed body).
- `ui/src/views/__tests__/PlanDetailView.spec.ts` — the mount harness (memory router,
  `createTestingPinia` with `initialState.training.currentPlan`) to extend.
- `ui/src/components/goals/__tests__/GoalsEventForm.spec.ts` — the form-spec harness; note the
  `await vi.waitFor(() => expect(store.x).toHaveBeenCalled...)` idiom used instead of counting
  `flushPromises()`.

## Acceptance criteria

### `ui/src/types/training.ts` (additive only)
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
Leave `TrainingPlanRequest` and `TrainingPlanResponse` untouched.

### `ui/src/services/training.ts` (two additions)
```ts
export async function updatePlan(id: string, req: TrainingPlanUpdateRequest): Promise<TrainingPlanResponse>
export async function getWeeklyTargets(id: string): Promise<WeeklyTargetsResponse>
```
- `updatePlan` → `apiFetch<TrainingPlanResponse>(`/trainingplans/${id}`, { method: 'PUT', body: JSON.stringify(req) })`,
  throwing `new Error('Unexpected empty response from PUT /trainingplans/{id}')` on `null` (the
  `updateWorkout` pattern).
- `getWeeklyTargets` → `apiFetch<WeeklyTargetsResponse>(`/trainingplans/${id}/weekly-targets`)` with the
  same null guard. No query params.

### `ui/src/stores/training.ts` (additive slice, placed next to the plan-browser block)
- `const weeklyTargets = ref<WeeklyTargetsResponse | null>(null)`, `const loadingTargets = ref(false)`,
  `const targetsError = ref<ApiError | Error | null>(null)`.
- `async function loadWeeklyTargets(id: string)` — the `loadPlan` shape: clear `weeklyTargets`, set
  loading, catch into `targetsError`, always clear loading. **Never re-throws** (it is a read).
- `async function updatePlan(id: string, req: TrainingPlanUpdateRequest): Promise<TrainingPlanResponse>`
  — calls the service, assigns the response to `currentPlan.value`, then `await loadWeeklyTargets(id)`
  (a window/cadence/event change reshapes the whole ramp), then returns the updated plan.
  **Re-throws** so the form can map the 400 (mirrors `updateWorkout`). Do **not** swallow the error and
  do **not** call `loadPlan` again — 18-2's response already carries the plan's planned workouts.
- Export all four new members from the returned object.
- Do not touch any existing action.

### `ui/src/schemas/training.ts` (additive)
```ts
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
- Bounds mirror 18-2's validator exactly (1–8, ≥ 1, 30–90). Client bounds are a UX convenience; the
  server remains authoritative.
- **No** cross-field "all three or none" rule (a partial cadence is legal — ADR-0009 §2).
- `trainingPlanSchema` and `plannedWorkoutItemSchema` are **not** modified or re-derived from this one.

### `ui/src/components/training/PeriodizationPanel.vue` (new — the only new component)
`<script setup lang="ts">`, Composition API, TypeScript.
- Props: `defineProps<{ plan: TrainingPlanResponse }>()`. No emits.
- `onMounted`: `void store.loadWeeklyTargets(props.plan.id)`; and `if (!goalsStore.events) void goalsStore.loadAll()`.
- **Read summary (always visible):** plan name, methodology, `formatDay(startDate) – formatDay(endDate)`,
  and either the linked event's name (looked up in `goalsStore.events` by `plan.eventId`) or
  "No target event". Plus a cadence line: `"{buildWeeks} build : {recoveryWeeks} recovery · {recoveryWeekPercentage}% recovery volume"`
  when all three are set, else `"No cadence set"`. An `Edit` / `Cancel` `Button` toggles the form.
- **Edit form (hidden until toggled):** `useForm<PlanMetadataFormValues>({ validationSchema:
  toTypedSchema(planMetadataSchema), initialValues: fromPlan(props.plan) })` where `fromPlan` maps
  `eventId: plan.eventId ?? ''` and passes the three periodization numbers through as-is. Fields:
  `name` (Input), `methodology` (Select over the four values), `startDate` / `endDate`
  (`Input type="date"`), `eventId` (Select, first item value `''` labelled "No target event", then one
  `SelectItem` per `goalsStore.events` — **all** events, not `upcomingEvents`, so a plan linked to a
  past race keeps its selection), `buildWeeks` / `recoveryWeeks` / `recoveryWeekPercentage`
  (`Input type="number"`, the percent field labelled "Recovery volume (% of a build week)" with
  `min="30" max="90"`).
- Submit: map to `TrainingPlanUpdateRequest` with `eventId: values.eventId || null` and the three
  numbers as `?? null`; call `store.updatePlan(props.plan.id, req)`; on success `form.resetForm({ values })`,
  set a `justSaved` flag, close the form; on failure set `globalError` via
  `extractApiValidationMessages(e)` (joined with a space) with the `ApiError` 404/status fallbacks
  copied from `GoalsEventForm.setError`. The server's `PlanWindow: …` / `EventId: …` text must reach the
  user verbatim — do **not** rewrite or shorten it.
- **Target ramp `<section>`** (inline in this component, mirroring `LoadChartSection`'s card layout):
  - Header: "Weekly target ramp" + an eyebrow reading `"Ramping from {baseline} TSS/wk · {sourceLabel}"`
    where `sourceLabel` is `'your last 4 weeks'` for `TrailingActual` and `'this plan's first week'` for
    `FirstWeekPlanned`.
  - When `weeklyTargets` is null and `loadingTargets` → "Loading…".
  - When `weeks.length === 0` (i.e. `baselineSource === 'None'`) → an honest empty state:
    "No targets yet — log four weeks of training or plan your first week, and the ramp appears." and
    **no** `LoadChart`.
  - Otherwise `<LoadChart :weeks="chartWeeks" :optimal-band="null" />` where
    ```ts
    const chartWeeks = computed<WeeklyLoadWeek[]>(() =>
      (store.weeklyTargets?.weeks ?? []).map((w) => ({
        weekStart: w.weekStart,
        plannedLoad: w.targetLoad,   // targets take the hatched "planned" channel
        actualLoad: w.actualLoad,
        rollingAverage: w.targetLoad // the dashed trend traces the ramp itself
      })),
    )
    ```
    `optimalBand` is **null** — the plan's own targets replace the ACWR band here.
  - Legend (the SVG is `aria-hidden`): "Target" (hatch swatch), "Actual" (accent swatch), "Ramp"
    (warn swatch) — relabelled from `LoadChartSection`'s Planned/Actual/4-wk avg.
  - A week strip / table under the chart listing each `weekStart` (short date) with its `targetLoad` and
    a badge reading `Recovery` when `isRecoveryWeek`, `Taper` when `isTaperWeek`, nothing otherwise —
    this is the accessible, assertable rendering of the cadence (the chart is `aria-hidden`).
  - **Known cosmetic artifact to document with a comment, not to fix:** `lib/charts/load.ts:65` labels
    the last bar `· NOW`, which here is the plan's final week, not the current week. Forking `load.ts`
    or `LoadChart.vue` is out of scope; note it in the commit body for the handoff's tech-debt list.
- Uses only existing primitives from `ui/src/components/ui/*` — no new shadcn component, no Textarea.

### `ui/src/views/PlanDetailView.vue`
- Replace lines 66–74 (the comment + the read-only header `div`) with
  `<PeriodizationPanel :plan="plan" />`, importing the component at the top. The stale
  "metadata editing is Phase 18" comment goes away with the block.
- Nothing else changes: the back-link, planned-workout list, `openBuilder`, `WorkoutStructureBuilder`,
  and `formatDay` stay exactly as they are (`formatDay` stays even if the panel has its own copy —
  the list still uses it).

## Non-goals
- **No backend change of any kind** in this task (no controller, service, DTO, validator, or
  `Program.cs` edit). If the panel needs a field the API does not return — **STOP and ask**.
- **No migration, no new NuGet or npm package** (no date-picker library, no chart library, no new
  shadcn-vue component).
- **Do not modify** `ui/src/components/charts/LoadChart.vue` or `ui/src/lib/charts/load.ts` — including
  "just to fix the `· NOW` label" or "just to add a target series". The adapter is the whole point.
- **Do not modify** `LoadChartSection.vue`, `ProgressView.vue`, `stores/analytics.ts`, or anything on
  the Progress page.
- **Do not modify** `trainingPlanSchema`, `plannedWorkoutItemSchema`, `TrainingView.vue`, or the plan
  *create* flow. Creating a plan still omits the periodization fields; setting them is an edit.
- **Do not** export or relocate the local `optionalNumber` helper in `schemas/training.ts`.
- **Do not** add planned-workout editing, deletion, drag, or a "generate workouts from targets" action
  to the panel — targets are numbers; authoring stays manual (ROADMAP *Out of scope*).
- **Do not** edit `ThisWeekCard.vue` or `ThisWeekResponse` — that is 18-5.
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.
- **No auth code / no token handling** — Phase 12 stays deferred and approval-gated.
- No per-sport target split, no multi-event ATP, no coach view.

## Test expectations

**`ui/src/components/training/__tests__/PeriodizationPanel.spec.ts` (new)** — mount with
`createTestingPinia({ createSpy: vi.fn, initialState: { training: { currentPlan, weeklyTargets },
goals: { events } } })`, stubbing `RouterLink` where needed.
- `renders the plan metadata summary and the cadence line` — name, methodology, formatted window,
  `3 build : 1 recovery`, `60%`.
- `renders "No cadence set" when the periodization fields are null`.
- `renders the linked event name when eventId matches a loaded event, else "No target event"`.
- `passes targets to LoadChart on the planned channel with a null band` — `findComponent(LoadChart)`;
  assert `props('weeks')` equals the mapped array (`plannedLoad === targetLoad`,
  `rollingAverage === targetLoad`, `actualLoad` passed through) and `props('optimalBand') === null`.
- `renders the honest empty state and no chart when baselineSource is None` — `weeks: []` →
  text contains "No targets yet", `findComponent(LoadChart).exists()` is `false`.
- `badges the recovery and taper weeks in the week strip` — a fixture with
  `isRecoveryWeek` on index 3 and `isTaperWeek` on the last two → the strip shows one `Recovery` and two
  `Taper` badges.
- `submits mapped metadata through the store` — open the form, change `name` and set
  `recoveryWeekPercentage` to `60`, submit, then
  `await vi.waitFor(() => expect(store.updatePlan).toHaveBeenCalledWith('p1', expect.objectContaining({
  name: 'Renamed', recoveryWeekPercentage: 60, eventId: null })))` — pinning that an empty `eventId`
  string becomes `null`.
- `surfaces the server's plan-window rejection` — make `store.updatePlan` reject with an `ApiError`
  carrying `errors: ['PlanWindow: 2 planned workout(s) fall outside …']`; assert the message text
  appears in the DOM verbatim and the form stays open.
- `rejects a recovery percentage below 30 without calling the store` — set `20`, submit, assert
  `store.updatePlan` was not called and a validation message renders.

**`ui/src/services/__tests__/training.spec.ts` (new, minimal)** — the `events.spec.ts` shape:
- `updatePlan PUTs the metadata body to /trainingplans/{id}` — URL `/api/v1/trainingplans/p1`, method
  `PUT`, parsed body deep-equals the request.
- `getWeeklyTargets GETs /trainingplans/{id}/weekly-targets` — URL assertion + returned payload.

**`ui/src/views/__tests__/PlanDetailView.spec.ts` (extend)** — add
`renders the periodization panel instead of the read-only header`
(`findComponent(PeriodizationPanel).exists()` is true and the plan name still renders); keep both
existing tests passing unchanged. The `training` initial state gains `weeklyTargets: null` if the
panel's `onMounted` read needs it.

No new store spec: `stores/training.ts` has none today, and the component spec asserts the store
contract through `createTestingPinia` spies. Say so in the commit body rather than adding one silently.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
`pnpm run build` runs `vue-tsc -b` — a type error in the new interfaces fails the build, which is the
point. Vitest must rise from **229 / 53 files** with zero failures; xUnit stays at its post-18-3 count
(no backend change). Re-run once before debugging a worker crash with all tests passing (known
transient fork quirk).

## Review checklist
- [ ] `LoadChart.vue` and `lib/charts/load.ts` are absent from `git diff`.
- [ ] The targets→chart adapter lives in `PeriodizationPanel.vue`, maps `targetLoad` onto `plannedLoad`,
      and passes `:optimal-band="null"`.
- [ ] `baselineSource === 'None'` renders an explicit empty state, never a zeroed chart.
- [ ] The percent field is labelled and bounded **30–90** (percent, not a fraction).
- [ ] `eventId: ''` maps to `null` on submit; the picker lists **all** events so an existing link is
      never silently dropped.
- [ ] `store.updatePlan` re-throws, assigns the PUT response to `currentPlan`, and re-loads targets.
- [ ] Server 400 text (`PlanWindow:` / `EventId:`) reaches the user verbatim.
- [ ] `PlanDetailView.vue`'s planned-workout list and structure-builder wiring are untouched, and the
      stale "metadata editing is Phase 18" comment is gone.
- [ ] Every SFC is `<script setup lang="ts">`; no `fetch`/`axios` outside `src/services/`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
