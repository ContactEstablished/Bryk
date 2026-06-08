# Task 11-5 — Log-workout UI + planned-vs-actual + Recent Activity (Vue)

## Goal
The executed-workout UI: from a planned workout (e.g. a This-Week "Log" affordance), open a
log-workout form that captures session-level actuals and an optional per-step actuals grid, shows a
**planned-vs-actual** comparison, and submits via Task 11-4's endpoints; and replace the dashboard's
"Recent Activity" placeholder with the athlete's completed workouts. Frontend only. No backend changes.

## Scope discipline — read first
Bounded by ADR-0005: per-step actuals are **optional/nullable** (partial entry is valid — don't force a
full grid), and **no PMC / Form-TSB** chart (later phase). If you find yourself building a TSB trend
chart, stop.

## Depends on
- **Task 11-4** — `POST /workouts`, `GET /workouts/{id}`, `GET /workouts` (week/recent).
- Pairs with **Task 10-5** — reuse its builder patterns; extend the existing `training` store/service/types.

## Required reading
- `md/decisions/0005-training-load-and-execution.md` §4, §5, §6.
- `ui/src/components/training/{WorkoutStructureBuilder,WorkoutBlockCard,WorkoutStepRow}.vue` — the
  nested `useFieldArray` + per-step row pattern to mirror for the per-step actuals grid.
- `ui/src/schemas/training.ts` — extend with the log-workout / step-result zod schemas (`toTypedSchema`,
  sport-aware factory); reuse exported per-row schemas where they exist.
- `ui/src/stores/training.ts` / `services/training.ts` / `types/training.ts` — add `logWorkout` /
  `getWorkout` / `getRecentWorkouts` actions + request/response types; **do not fork a store**.
- `ui/src/components/dashboard/{ThisWeekCard,RestingHrCard}.vue` + `ui/src/views/HomeView.vue` — the
  "Recent Activity" `PlaceholderCard` to replace; where the "Log" affordance lives.
- `ui/src/components/training/__tests__/WorkoutStructureBuilder.spec.ts` — Vue test conventions.

## Acceptance criteria
- **Types/service/store**: add `LogWorkoutRequest` / `WorkoutResponse` (+ step-result shapes) and
  `logWorkout(req)` (POST then reload recent), `getWorkout(id)`, `getRecentWorkouts()` actions,
  mirroring 10-5's request/store shape.
- **Log-workout form**: launched from a planned workout (seeds its planned steps for comparison) **or**
  standalone (unplanned). Session fields: completed date, actual duration/distance, avg/max HR, overall
  RPE, notes, optional manual load override. Optional **per-step actuals grid** (add/skip rows; each row
  nullable). Submit calls `logWorkout`; field errors via `FormMessage`, server errors via the global
  banner / `extractApiValidationMessages`.
- **Planned-vs-actual**: when launched from a planned workout, show planned target vs. captured actual
  per step (and planned `effectiveLoad` vs. actual `effectiveLoad`).
- **Recent Activity**: replace the `PlaceholderCard title="Recent Activity"` in `HomeView.vue` with a
  component listing recent completed workouts (date, sport, effective load) from `getRecentWorkouts`.
- **Components**: Composition API, `<script setup lang="ts">`, one per file, PascalCase; extract row/card
  components if markup is non-trivial. No `fetch`/`axios` in components.
- **Tests** (≥2): adding a step-result row appends to the grid; submitting a valid session logs (mock
  the service) — **test gotcha:** valid vee-validate submits over refined-array schemas need ~6
  `flushPromises`.
- `pnpm run build` + `pnpm test` green; count up by ≥2.

## What NOT to modify
- Do not build a PMC / Form-TSB chart — later phase (ADR-0005 §7); leave that placeholder.
- Do not build device `.fit` import — post-v1; per-step actuals are manual + optional.
- Do not change the load formulas or backend — Tasks 11-1/11-4.
- Do not fork a second training store or call `fetch`/`axios` directly from components.

## Suggested commit
```
feat: add log-workout UI and recent activity

Log a completed workout from /training (or a planned session): session
actuals plus an optional per-step actuals grid with a planned-vs-actual
view, backed by the 11-4 workouts API. Replace the Recent Activity
placeholder with the athlete's completed workouts. No TSB chart (later
phase).
```
