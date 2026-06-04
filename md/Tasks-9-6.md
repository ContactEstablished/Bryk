# Task 9-6 — Minimal plan-authoring UI (Vue)

## Goal
Add a first `/training` route with a deliberately minimal "create a training plan + add planned workouts" form, so a plan can be authored end-to-end through the UI within Phase 9 (rather than only via the API/seed). This gives the This Week card (Task 9-5) a way to be populated by hand.

Frontend-only. No backend changes.

## Scope discipline — read this first
This is intentionally a **bare-bones** authoring surface, NOT the structured-workout builder. The rich interval/zone builder (target power/HR/pace per step, strength sets/reps/load UI) is **Phase 10**. Phase 9's form captures only what Task 9-3's DTOs accept: plan name + methodology + date range (+ optional target event), and planned workouts with sport + scheduled date + title + planned duration/load. If you find yourself building an interval editor, stop — that's Phase 10 scope creep.

## Depends on
- **Task 9-3** — `POST /api/v1/trainingplans`, `POST .../{id}/plannedworkouts`, `GET /api/v1/trainingplans`, and their DTO shapes.
- Pairs with **Task 9-5** (shares the `training` store/service/types). 9-5 and 9-6 can land in either order; whichever lands second reuses the other's `training` store rather than creating a second one.

## Required reading
- `ui/src/views/ProfileView.vue` + `ui/src/components/profile/ProfileGoalsSection.vue` — **the closest reference**: a route-level view composing sections, with add/remove "draft" rows for a repeating list (events/goals). The planned-workout list mirrors this draft-row pattern.
- `ui/src/components/profile/ProfileEventCard.vue` — per-item form card with `useForm` + zod + the shadcn `FormField`/`FormItem`/`FormControl`/`FormMessage` stack, `defineEmits` for `remove`/`created`.
- `ui/src/schemas/onboarding.ts` — the zod schema + `toTypedSchema` conventions (e.g. `eventItemSchema`); write the plan/planned-workout schemas in the same style, including the UTC "today or later" date refinement helper.
- `ui/src/router/index.ts` — route registration; add `/training` as a lazy-loaded route named `training`, mirroring the `/profile` entry.
- `ui/src/components/dashboard/DashboardSidebar.vue` — the sidebar already lists "Workouts/Progress/Goals" as inert items. **Do NOT add a new nav route here** unless the task explicitly says to — see What NOT to modify. (If a nav entry is wanted, it's a one-line `accountItems`/`trainItems` addition mirroring Profile, but default to leaving the sidebar alone for this task.)
- `ui/src/stores/training.ts` + `services/training.ts` + `types/training.ts` from Task 9-5 — reuse/extend; add `createPlan` / `addPlannedWorkout` actions + the write-side request types here if 9-5 hasn't.
- `ui/src/services/events.ts` (if present) / `ui/src/services/profile.ts` — the POST service style with `apiFetch`.

## Acceptance criteria

**Types / service / store (extend Task 9-5's `training.*`):**
- `types/training.ts` — add write-side request interfaces (`TrainingPlanRequest`, `PlannedWorkoutDto`) mirroring Task 9-3's request DTOs.
- `services/training.ts` — add `createPlan(req): Promise<TrainingPlanResponse>` and `addPlannedWorkout(planId, dto): Promise<PlannedWorkoutResponse>` (and `getPlans()` if the view lists existing plans).
- `stores/training.ts` — add `createPlan` / `addPlannedWorkout` actions that POST then re-fetch (mirror the `saveRequired` → re-load pattern in `stores/profile.ts`), re-throwing so the form can map validation errors (reuse `services/apiErrors.ts` `mapApiValidationToFields` if it generalizes; otherwise a simple global error banner like `RecommendedStep.vue`).

**Route + view:**
- `ui/src/router/index.ts` — add `{ path: '/training', name: 'training', component: () => import('@/views/TrainingView.vue') }`.
- `ui/src/views/TrainingView.vue` (new) — `<script setup lang="ts">`. Renders a "Create Training Plan" form (name, methodology select reusing the methodology options, start/end date, optional target-event select) and, once a plan exists (or inline), a repeating "Planned Workouts" list with add/remove draft rows (sport select, scheduled date, title, planned duration/load). Submit creates the plan and its planned workouts via the store. Keep the layout consistent with `ProfileView.vue` (section cards, `rounded-lg border bg-card p-6`).
- Composition API only; `<script setup lang="ts">`; one component per file; PascalCase filenames; per-item card extracted to a child component if the row markup is non-trivial (mirror `ProfileEventCard`).

**Validation:**
- zod schemas in `ui/src/schemas/` (e.g. `training.ts`) via `toTypedSchema`, matching Task 9-3's server validators (name required/max length; end ≥ start; title required; non-negative duration/load; scheduled date today-or-later if 9-3 requires it). Surface field errors through `FormMessage` and server validation errors through the apiErrors mapper / a global banner.

**Test (`ui/src/views/__tests__/TrainingView.spec.ts` or component-level, ≥2 cases):**
- Renders the create-plan form (heading + name input + submit present), mounted with `createTestingPinia` + `RouterLinkStub` as needed.
- "Add Planned Workout" appends a draft row (mirror the `ProfileGoalsSection` "Add Event appends a draft" test).
- (Optional) submitting an invalid plan (empty name / end < start) shows a validation message.

**Build / test:**
- `pnpm run build` green from `ui/`.
- `pnpm test` green; count up by ≥2.

## Files likely to change/add
- `ui/src/views/TrainingView.vue` (new)
- `ui/src/components/training/PlannedWorkoutCard.vue` (new, if the row is extracted)
- `ui/src/schemas/training.ts` (new)
- `ui/src/types/training.ts` — add request types (extends 9-5's file)
- `ui/src/services/training.ts` — add write actions (extends 9-5's file)
- `ui/src/stores/training.ts` — add write actions (extends 9-5's file)
- `ui/src/router/index.ts` — one route
- `ui/src/views/__tests__/TrainingView.spec.ts` (new)

## What NOT to modify
- Do not build the structured-workout / interval / zone builder — Phase 10. Phase 9 captures only the fields Task 9-3 accepts.
- Do not add a new sidebar nav route in `DashboardSidebar.vue` unless explicitly desired — default to leaving the sidebar's inert items as-is (they activate in their own phases). If you do add `/training` to the sidebar, it's a single `trainItems` entry mirroring the Profile pattern — flag it in the PR.
- Do not change the dashboard cards (8-4 / 8-5 / 9-5) or HomeView.
- Do not change Task 9-3's backend DTOs to fit the form — the form conforms to the API, not vice versa.
- Do not call `fetch`/`axios` directly from components — go through `services/training.ts`.
- Do not add execution/logging-a-completed-workout UI — Phase 11.

## Test plan
1. `pnpm run build` green.
2. `pnpm test` green; new tests in the count.
3. Manual smoke (Tasks 9-2…9-4 landed): visit `/training`, create a plan, add two planned workouts dated in the current week, submit → success; navigate to `/` → the This Week card (Task 9-5) now shows those sessions. Submit an invalid plan → validation message; no console errors.
4. `git diff --stat` — only the new training view/schema/(card) + the extended training type/service/store + one router line + the spec.

## Suggested commit
```
feat: add minimal /training plan-authoring UI

A bare-bones /training route to create a training plan and add planned
workouts (name, methodology, date range, + sport/date/title/duration per
session), POSTing to the Task 9-3 endpoints via the training store. Just
enough to populate the This Week card by hand; the structured interval/
zone workout builder is Phase 10. Sidebar nav unchanged.
```
