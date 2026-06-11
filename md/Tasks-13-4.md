# Task 13-4 — Workout detail view (metrics, planned-vs-actual, edit, delete)

## Surface
Frontend only (consumes 13-1's PUT/DELETE + `TrainingPlanId`). A new `WorkoutDetailView.vue` at
`/workouts/:id`: a `MetricTile` summary strip, a per-step **planned-vs-actual** table that finally
renders the long-captured `AvgPower`/`AvgPace` and `Workout.Notes`, in-place edit via `LogWorkoutForm`
in a new **edit mode**, and delete behind a confirm. Service + store wiring for update/delete + the
structure compose.

## Why
13-3 lists workouts but there's no way to inspect one, fix a mis-logged actual, or remove a bad
entry — and `AvgPower`/`AvgPace`/`Notes` have been captured since Phase 11 yet never shown. The
planned-vs-actual table is the first place an athlete sees prescription against execution.

## Depends on
- **Task 13-1** — `PUT /workouts/{id}`, `DELETE /workouts/{id}`, additive `WorkoutResponse.trainingPlanId`.
- **Task 13-3** — the list rows that link here; shared service/store/types.
- **Task 10-4/10-5** — `GET /trainingplans/{planId}/plannedworkouts/{pwId}/structure` (`getStructure`),
  reused to source the planned side.

## Required reading
- `ui/src/components/common/MetricTile.vue` (+ `useCountUp`) — the summary tiles; `DeltaChip` for
  vs-planned deltas; `TypePill`/`pills.ts` for the sport pill.
- `ui/src/components/training/LogWorkoutForm.vue` — extend for edit mode (currently log-only, takes a
  `plannedWorkout` prop, submits `store.logWorkout`). Note `plannedSteps`/`plannedLabel` already do a
  planned-vs-actual seed for the **log** flow — reuse that thinking for the table.
- `ui/src/schemas/workouts.ts` — `logWorkoutSchema` (reused unchanged for edit).
- `ui/src/services/training.ts` (`getWorkout`, `getStructure`) + `ui/src/stores/training.ts`
  (`logWorkout`, `loadStructure`) + `ui/src/types/training.ts` (`WorkoutResponse`,
  `WorkoutStepResultResponse`, `WorkoutStepResponse`, `LogWorkoutRequest`).
- `ui/src/views/ZonesView.vue` / `ProfileView.vue` — `AppShell` detail-page layout + section cards.

## Acceptance criteria
- **Types** (`ui/src/types/training.ts`): add `trainingPlanId: string | null` to `WorkoutResponse`;
  add `UpdateWorkoutRequest` (alias of/identical to `LogWorkoutRequest`'s body for the PUT).
- **Service** (`ui/src/services/training.ts`): `updateWorkout(id, req)` → `PUT /workouts/{id}`;
  `deleteWorkout(id)` → `DELETE /workouts/{id}` (204, no body).
- **Store** (`ui/src/stores/training.ts`): a `currentWorkout` slice (load via `getWorkout`),
  `updateWorkout(id, req)` and `deleteWorkout(id)` actions that re-throw for the form/confirm to map
  errors, and on success refresh the affected slices (`recentWorkouts`, and the 13-3 `workouts` list)
  so the dashboard feed and history list reflect the change/removal.
- **Edit mode in `LogWorkoutForm.vue`**: add an optional `workout?: WorkoutResponse | null` prop.
  When present: pre-fill every field from the workout (sport from `workout.sport`, the session actuals,
  `loadOverride`, `rpe`, `notes`, and a step-result row per `workout.stepResults` carrying its
  `workoutStepId`); the header reads "Edit workout", the submit button reads "Save changes", and submit
  calls `store.updateWorkout(workout.id, req)` instead of `logWorkout`. Emits `saved` (alongside the
  existing `logged`/`close`). **Log mode (no `workout` prop) is unchanged** — keep "Log workout" copy so
  `LogWorkoutForm.spec.ts` stays green (update it only if you add assertions).
- **View** (`ui/src/views/WorkoutDetailView.vue`, in `AppShell`):
  - On mount, load the workout by `route.params.id`; if `plannedWorkoutId` && `trainingPlanId`, also
    load the structure via `getStructure(trainingPlanId, plannedWorkoutId)`.
  - **`MetricTile` strip**: EffectiveLoad (TSS), duration (formatted), distance, avg HR / max HR — each
    tile shows "—" when the value is absent; numeric tiles animate via `MetricTile`'s built-in
    `useCountUp`. Sport pill + completed date in the header.
  - **Planned-vs-actual table**: flatten the planned steps (blocks→steps, ordered) and zip to
    `stepResults` by `workoutStepId`. Each row shows the planned target (zone/power/pace/duration, or
    sets×reps×load for strength) beside the actual (`AvgPower`, `AvgPace`, avg HR, duration, RPE) —
    **`AvgPower` and `AvgPace` displayed for the first time**. Rows with no planned match (ad-hoc
    actuals) show actual-only; planned steps with no actual show planned-only. When the workout has no
    linked plan/structure, render just the actual step-result rows (or a "no per-step detail" note).
  - **Notes**: render `workout.notes` when present (first surfacing of the captured field).
  - **Edit**: an "Edit" control reveals `LogWorkoutForm` in edit mode bound to the loaded workout; on
    `saved`, reload the workout (and structure) and collapse the form.
  - **Delete**: a "Delete" control behind an explicit confirm (inline confirm or `window.confirm`);
    on confirm, call the store delete then `router.push('/workouts')`.
- **Router** (`ui/src/router/index.ts`): `{ path: '/workouts/:id', name: 'workout-detail',
  component: () => import('@/views/WorkoutDetailView.vue') }`.
- **Vitest** (`ui/src/views/__tests__/WorkoutDetailView.spec.ts`): with a seeded `currentWorkout`,
  the MetricTile values and a step row's `AvgPower`/`AvgPace` render (assert visible text); Notes render
  when present; clicking Edit reveals the form ("Save changes" visible); clicking Delete then confirming
  invokes the delete action. Assert text, not classes.
- `pnpm run build` green; `pnpm test` green (re-run once on the transient worker crash).

## What NOT to modify
- Don't fatten `WorkoutResponse` with planned structure — compose via the existing `getStructure`
  endpoint using `trainingPlanId` (13-1).
- Don't change the structure builder or plan endpoints (13-5 reuses the builder read-only-ish).
- Don't add charts/aggregates; the comparison is per-step display only.
- Don't regress `LogWorkoutForm`'s log mode copy/behavior.

## Suggested commit
```
feat(ui): workout detail with planned-vs-actual, edit and delete

WorkoutDetailView at /workouts/:id shows a MetricTile strip, a per-step
planned-vs-actual table (now surfacing AvgPower/AvgPace and Notes), inline
edit via LogWorkoutForm's new edit mode (PUT), and delete-with-confirm
(DELETE -> back to the list). Planned side composed from the existing
structure endpoint via the workout's trainingPlanId.
```
