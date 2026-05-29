# Task 8-2 — Profile Vue surface

> **Updated 2026-05-28.** This task was reshaped after scoping revealed the onboarding
> `/goals` POST is append-only with no update/delete path — re-submitting a loaded list
> would duplicate every event and goal. Rather than ship a fake "edit" or a read-only
> Goals section, we built **Event/Goal CRUD endpoints first** (Part A, landed in commit
> `f36e374`). This task is now the frontend half (Part B): the `/profile` surface, with the
> Goals section doing real per-row add / save / delete against those endpoints.

## Goal
Add a `/profile` route that lets an athlete view and edit the three sections of their
onboarding data — Required identity, Recommended thresholds + HR, Goals events + goals.

- **Required** and **Recommended** save through the existing onboarding POSTs (upsert /
  upsert-by-sport — edit-in-place already works).
- **Goals** uses the Part A per-item endpoints (`/api/v1/events`, `/api/v1/goals`) for
  real create / update / delete, with reads coming from `GET /api/v1/profile/goals`.

Depends on Task 8-1 (profile read endpoints) and Part A (event/goal CRUD) — both merged.

## Current code/status

- **Task 8-1 (merged, `92154f4`)** ships `GET /api/v1/profile/{required,recommended,goals}`.
  `GET /profile/goals` returns `ProfileGoalsResponse` whose `Events` / `Goals` are now
  **Id-bearing** `EventResponse` / `GoalResponse` (changed in Part A so the editor can
  target items by id).
- **Part A (merged, `f36e374`)** ships per-item CRUD:
  - `POST /api/v1/events` → 201 + `EventResponse`; `PUT /api/v1/events/{id}` → 200 + updated;
    `DELETE /api/v1/events/{id}` → 204.
  - `POST /api/v1/goals`, `PUT /api/v1/goals/{id}`, `DELETE /api/v1/goals/{id}` — same shapes.
  - Update/Delete return **404** when the item is missing or belongs to another athlete.
  - Create/Update bodies are the Id-less `EventDto` / `GoalDto`.
- **Required / Recommended writes** still go through Phase 4's `POST /onboarding/required`
  (upsert) and `/onboarding/recommended` (upsert by sport). The Profile surface reuses these.
- `ui/src/components/onboarding/{Required,Recommended,Goals}Step.vue` are the wizard form
  templates. We are **not** reusing them (see Decisions).
- `ui/src/components/dashboard/DashboardSidebar.vue` lists Profile as a static nav item with
  `active: false` and no routing. This task makes Dashboard + Profile navigable.
- Pinia: `useOnboardingStore` exists; Profile gets its own `useProfileStore`.

## Decisions (resolved 2026-05-28)

1. **Goals editing → real per-row CRUD.** Existing events/goals are editable rows (Save +
   Delete each); "Add Event" / "Add Goal" create new rows. Backed by the Part A endpoints.
   No re-submission of whole lists, no duplication.
2. **New Profile section components**, not reuse of the wizard `*Step.vue` components. The
   wizard's one-way "Continue" semantics differ from save-in-place editing, and reuse would
   require modifying the Step components (reserved for Task 8-3). Accept the field-markup
   duplication for now; no shared `<XxxFields>` extraction yet.
3. **Duplicate the dashboard shell wrapper in `ProfileView`.** The sidebar+main shell lives
   inline in `HomeView.vue`; `ProfileView` renders its own `<div class="flex min-h-screen">
   <DashboardSidebar /><main>…</main></div>`. No `HomeView` change, no extracted layout
   component yet — revisit when a third dashboard page needs the shell.
4. **Sidebar navigation via `<RouterLink>`.** Dashboard and Profile become `RouterLink`s with
   `active` computed from `useRoute().name` (`'home'` / `'profile'`). Workouts / Progress /
   Goals stay inert. Active-highlight polish is Task 8-5.
5. **Goal `type` stays `General`.** Matching onboarding, the Goals editor does not expose
   `GoalType`; payloads set `type: 'General'`. Event-driven goals (linking a goal to an event)
   remain out of scope — a future task.

## Goals-section approach (the reshaped part)

`ProfileGoalsSection` renders two lists from `GET /profile/goals`:

- Each existing **event** is a `ProfileEventCard` — its own `useForm` bound to that one item,
  with **Save** (`PUT /events/{id}`) and **Delete** (`DELETE /events/{id}`).
- Each existing **goal** is a `ProfileGoalCard` — same pattern against `/goals/{id}`.
- **Add Event / Add Goal** append a draft card whose **Save** calls `POST /events` / `POST
  /goals` (create), and whose **Remove** just discards the unsaved draft locally.
- After any create / update / delete, the store re-fetches `/profile/goals` so the lists
  reflect server truth (and new items pick up their server-assigned id).

Per-row independent forms (one `useForm` per card) are why this needs the per-item zod
schemas — see the schema note in "What NOT to modify".

**Known limitation (accepted).** `eventItemSchema` (and the server's `EventDtoValidator`)
require `eventDate >= today`. Editing a past-dated event is therefore rejected unless its
date is moved forward. Events are upcoming-race-oriented, so this is an accepted v1 edge.

## Acceptance criteria

**Types:**
- `ui/src/types/profile.ts` — `ProfileRequiredResponse`, `ProfileRecommendedResponse`,
  `ProfileGoalsResponse`, plus `EventResponse` (`id` + the `EventDto` fields) and
  `GoalResponse` (`id` + the `GoalDto` fields). Reuse `SportThresholdsDto`, `EventDto`,
  `GoalDto`, `Sport`, `EventPriority`, `GoalType`, `TriathlonDistance` from
  `ui/src/types/onboarding.ts`.

**Services (all HTTP through `ui/src/services/api.ts`):**
- `ui/src/services/profile.ts` — `getRequired()`, `getRecommended()`, `getGoals()` over the
  Task 8-1 GET endpoints.
- `ui/src/services/events.ts` — `createEvent(data: EventDto): Promise<EventResponse>`,
  `updateEvent(id, data): Promise<EventResponse>`, `deleteEvent(id): Promise<void>`.
- `ui/src/services/goals.ts` — `createGoal` / `updateGoal` / `deleteGoal`, same shapes.

**Store:**
- `ui/src/stores/profile.ts` — `useProfileStore` (setup-style Pinia, mirroring
  `useOnboardingStore`). Holds `required` / `recommended` / `goals` response data +
  per-section loading/error state. Actions:
  - `loadRequired()` / `loadRecommended()` / `loadGoals()`.
  - `saveRequired(payload)` / `saveRecommended(payload)` → call the existing onboarding
    service `submitRequired` / `submitRecommended`, then re-load that section.
  - `createEvent(dto)` / `updateEvent(id, dto)` / `deleteEvent(id)` and
    `createGoal(dto)` / `updateGoal(id, dto)` / `deleteGoal(id)` → call the events/goals
    services, then `loadGoals()`. Re-throw on error for component-level handling.

**Route + view:**
- `ui/src/router/index.ts` — add `{ path: '/profile', name: 'profile', component: () =>
  import('@/views/ProfileView.vue') }`. Lazy-load.
- `ui/src/views/ProfileView.vue` — duplicated dashboard shell (sidebar + main). Stacks
  `ProfileRequiredSection`, `ProfileRecommendedSection`, `ProfileGoalsSection` with section
  headers. Each section loads its own data on mount.

**Section components (`ui/src/components/profile/`):**
- `ProfileRequiredSection.vue` — `useForm` + `toTypedSchema(onboardingRequiredSchema)`,
  `initialValues` from `store.required`. "Save changes" → `saveRequired`; on success show a
  brief inline "Saved" and re-fetch; on error map field errors via `mapApiValidationToFields`
  (reuse `ui/src/services/apiErrors.ts`).
- `ProfileRecommendedSection.vue` — same pattern with `onboardingRecommendedSchema` and
  `store.recommended`.
- `ProfileGoalsSection.vue` — orchestrates the event/goal lists + Add buttons + draft rows
  (see "Goals-section approach").
- `ProfileEventCard.vue` — one event's `useForm` + `toTypedSchema(eventItemSchema)`, Save +
  Delete.
- `ProfileGoalCard.vue` — one goal's `useForm` + `toTypedSchema(goalItemSchema)`, Save +
  Delete. Payload sets `type: 'General'`.

All section/card components use `<script setup lang="ts">` and the shadcn-vue `Form*` / `Input`
/ `Select` / `Button` primitives, matching the wizard step components. No "Continue" verb, no
`emit('next')`, no stepper chrome.

**Sidebar nav:**
- `ui/src/components/dashboard/DashboardSidebar.vue` — Dashboard + Profile become
  `RouterLink`s; `active` computed from `route.name`. Workouts / Progress / Goals stay inert.

**Tests (Vitest):**
- `ui/src/stores/__tests__/profile.spec.ts` — load + a goals CRUD round-trip (create → list
  updates; delete → list updates).
- `ui/src/services/__tests__/profile.spec.ts` — `getRequired/getRecommended/getGoals` round-trip
  against mocked fetch.
- `ui/src/services/__tests__/events.spec.ts` and `goals.spec.ts` — create/update/delete round-trip
  against mocked fetch (assert method + URL + body; delete handles 204/null).
- `ui/src/components/profile/__tests__/ProfileRequiredSection.spec.ts` — mount +
  invalid-submit-shows-errors.
- `ui/src/components/profile/__tests__/ProfileGoalsSection.spec.ts` — mount renders existing
  events/goals from a seeded store; "Add Event" appends a draft card.

**Build / test:**
- `pnpm run build` from `ui/` green.
- `pnpm test` from `ui/` green; test count grows by ≥6 tests.

## Files likely to change/add

New:
- `ui/src/types/profile.ts`
- `ui/src/services/profile.ts`, `ui/src/services/events.ts`, `ui/src/services/goals.ts`
- `ui/src/services/__tests__/profile.spec.ts`, `events.spec.ts`, `goals.spec.ts`
- `ui/src/stores/profile.ts`, `ui/src/stores/__tests__/profile.spec.ts`
- `ui/src/views/ProfileView.vue`
- `ui/src/components/profile/ProfileRequiredSection.vue`
- `ui/src/components/profile/ProfileRecommendedSection.vue`
- `ui/src/components/profile/ProfileGoalsSection.vue`
- `ui/src/components/profile/ProfileEventCard.vue`
- `ui/src/components/profile/ProfileGoalCard.vue`
- `ui/src/components/profile/__tests__/ProfileRequiredSection.spec.ts`
- `ui/src/components/profile/__tests__/ProfileGoalsSection.spec.ts`

Modified:
- `ui/src/router/index.ts` — one route added.
- `ui/src/components/dashboard/DashboardSidebar.vue` — nav items → RouterLink, active via useRoute.
- `ui/src/schemas/onboarding.ts` — **export only** (see below).

## What NOT to modify

- Do not modify `ui/src/views/OnboardingView.vue` or the three `*Step.vue` components — Task
  8-3 owns the band-aid removal there.
- Do not modify the existing onboarding store (`ui/src/stores/onboarding.ts`) or service
  (`ui/src/services/onboarding.ts`) — Profile reuses their existing methods.
- Do not change any zod **validation rules**. The one allowed change to
  `ui/src/schemas/onboarding.ts` is **adding `export`** to the existing `eventItemSchema` and
  `goalItemSchema` so the per-row cards can reuse them. Visibility only — no rule changes, no
  behavior change for onboarding. (If you'd rather not touch onboarding.ts at all, mirror the
  two item schemas in a new `ui/src/schemas/profile.ts` instead — but prefer the export to
  avoid rule drift.)
- Do not touch dashboard placeholder cards — Tasks 8-4 and 8-5 own those.
- Do not add `GoalType` selection or event-driven goal linking — out of scope (Decision 5).
- Do not introduce a shared `<XxxFields>` extraction — accept the duplication for now.

## Test plan

1. `pnpm run build` green from `ui/`.
2. `pnpm test` from `ui/` green; new tests in count.
3. Manual smoke (fully-onboarded athlete):
   - From the dashboard, click Profile in the sidebar → land on `/profile`.
   - Required section pre-filled from `/profile/required`; edit a value → Save → 204 →
     section re-fetches and shows the new value.
   - Recommended section pre-filled with HR + sport thresholds; edit → Save → re-fetch.
   - Goals section lists existing events and goals:
     - Edit an event field → Save → `PUT /events/{id}` → list re-fetches with the change.
     - Add Event → fill → Save → `POST /events` → new card gets its server id.
     - Delete a goal → `DELETE /goals/{id}` → it disappears from the list.
   - Refresh `/profile` → all three sections re-load from the server.
4. `git diff --stat` — only the named files touched; onboarding Step components untouched.

## Suggested commit

Single commit (includes the one-line `export` enabling change):

```
feat: add /profile route with three editable sections

Profile view at /profile lets athletes view and edit the three sections
of their onboarding data. Required/Recommended save via the existing
onboarding upsert POSTs; Goals does real per-row add/save/delete against
the event/goal CRUD endpoints, reading Id-bearing items from
GET /profile/goals.

New types, services (profile/events/goals), store, view, and section +
card components under ui/src/{types,services,stores,views,components}/profile/.
Sidebar Profile nav is now navigable; active-state polish lands in Task 8-5.
Reuses the onboarding zod schemas (eventItemSchema/goalItemSchema exported
for per-row reuse — no rule changes).

Onboarding wizard surface untouched — Task 8-3 retires the summary-card
band-aid separately.
```
