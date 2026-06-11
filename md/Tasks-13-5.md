# Task 13-5 — Plan browser (list → detail → reopen structure builder)

## Surface
Frontend only — the backend already exposes everything needed (`GET /trainingplans`,
`GET /trainingplans/{id}`, `GET`/`PUT .../structure`). Add a plan **browser**: a plan list, a plan
detail with its planned-workout rows, and an "Edit structure" action that reopens the existing
`WorkoutStructureBuilder` against a chosen planned workout. Closes the Phase-10 carry-forward gap (the
builder could only be opened from the just-created plan).

## Why
After creating a plan you could never reopen it: no list, no detail, no way to edit a planned
workout's structure later. The seed has an active 8-week plan with 17 planned workouts (4 structured)
that is currently unreachable in the UI. Browse + structure-edit only — **plan-metadata editing
(name/dates/methodology/event) is Phase 18** (`PUT /trainingplans/{id}` doesn't exist yet).

## Depends on
- **Task 9-3** — `GET /trainingplans` (athlete's plans) + `GET /trainingplans/{id}` (plan with planned
  workouts), both already shipped on `TrainingPlansController`.
- **Task 10-5** — `WorkoutStructureBuilder.vue` (self-contained: loads `GET structure`, saves
  `PUT structure` via the training store), reused unchanged.

## Required reading
- `api/Bryk.API/Controllers/TrainingPlansController.cs` — confirm `GetByAthleteAsync` (list) +
  `GetByIdAsync` (detail with `plannedWorkouts`) shapes; no backend change needed.
- `ui/src/components/training/WorkoutStructureBuilder.vue` — the component to reopen (props:
  `planId`, `plannedWorkoutId`, `sport`, `title`; emits `close`).
- `ui/src/views/TrainingView.vue` — how the builder is launched today (`buildTarget` pattern,
  `openBuilder`), and the create-plan form that stays the "New plan" destination.
- `ui/src/components/layout/AppSidebar.vue` — the **Training** nav item (`routeName: 'training'`,
  `to: '/training'`) and `mobileItems`.
- `ui/src/services/training.ts`, `ui/src/stores/training.ts`, `ui/src/types/training.ts`
  (`TrainingPlanResponse`, `PlannedWorkoutResponse`) — extend.
- `ui/src/router/index.ts`, `ui/src/views/ZonesView.vue` (list/detail layout reference).

## Decision — information architecture (locked here)
Repoint the existing **Training** sidebar item to the new plan **browser** (`/plans`,
`routeName: 'plans'`) — "Training" as a concept is *your plans*, and today it lands on a blank create
form with no way back to existing plans. The create-plan form stays at `/training` (unchanged) and is
reached via a prominent **"New plan"** link on the browser. Nav item count is unchanged; the mobile tab
follows. (No new top-level nav item; plan-metadata editing stays out per Phase 18.)

## Acceptance criteria
- **Service** (`ui/src/services/training.ts`): `getPlans()` → `GET /trainingplans`
  (`TrainingPlanResponse[]`); `getPlan(id)` → `GET /trainingplans/{id}` (`TrainingPlanResponse`).
- **Store** (`ui/src/stores/training.ts`): a `plans` list slice (load + loading/error) and a
  `currentPlan` slice (load by id + loading/error). Reuse the existing `structure`/`loadStructure`/
  `saveStructure` slice for the builder.
- **List view** (`ui/src/views/PlansView.vue`, `AppShell`, `title="Training Plans"`): loads `getPlans`
  on mount; renders a row per plan (name, methodology, start–end window, planned-workout count), each
  linking to `/plans/{id}`; a "New plan" link/button → `/training`. Empty state: "No plans yet."
  with the New-plan affordance.
- **Detail view** (`ui/src/views/PlanDetailView.vue`, `AppShell`): loads `getPlan(route.params.id)`;
  header shows plan name + window + methodology + linked event id (read-only — **no edit controls**);
  lists planned-workout rows (`TypePill` for sport, title, scheduled date, `effectiveLoad`/planned
  load when present). Each row with a structurable sport has an **"Edit structure"** button that opens
  `WorkoutStructureBuilder` (`:plan-id="plan.id"`, `:planned-workout-id="pw.id"`, `:sport="pw.sport"`,
  `:title="pw.title"`, `@close`), keyed by `pw.id` so switching targets remounts. On the builder's
  save the structure persists via the existing store path and survives a reload (verify in smoke).
  404 (missing/foreign plan) shows a "Plan not found" message.
- **Nav** (`AppSidebar.vue`): the Training item becomes `{ to: '/plans', routeName: 'plans', … }`
  (keep the `CalendarRange` icon + "Training" label). `isActive` highlights on `/plans` and `/plans/:id`
  (use `routeName`). Update `AppSidebar.spec.ts` in the same commit if it asserts the old target.
- **Router**: `{ path: '/plans', name: 'plans', component: () => import('@/views/PlansView.vue') }`
  and `{ path: '/plans/:id', name: 'plan-detail', component: () => import('@/views/PlanDetailView.vue') }`.
  Leave `/training` (create form) in place.
- **Vitest**: `PlansView.spec.ts` — seeded plans render a row each (assert names) + "New plan" present;
  `PlanDetailView.spec.ts` — seeded plan renders its planned-workout titles and clicking "Edit
  structure" reveals the builder (assert visible text). Assert text, not classes.
- `pnpm run build` green; `pnpm test` green (re-run once on the transient worker crash).

## What NOT to modify
- No plan-metadata editing, no plan create/delete here (create stays at `/training`; metadata edit is
  Phase 18). No `PUT /trainingplans/{id}` — it doesn't exist; don't add it.
- Don't change `WorkoutStructureBuilder` behavior (reuse as-is; only its mount site is new).
- Don't add calendar/scheduling or aggregates (Phases 14–16).
- Don't change backend code — this task is UI-only.

## Suggested commit
```
feat(ui): plan browser — list, detail, reopen structure builder

PlansView (/plans) lists the athlete's plans; PlanDetailView (/plans/:id)
shows planned-workout rows and reopens WorkoutStructureBuilder against an
existing planned workout via the structure GET/PUT. Repoints the Training
nav to the browser; the create form stays at /training as "New plan".
```
