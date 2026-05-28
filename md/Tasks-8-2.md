# Task 8-2 — Profile Vue surface

## Goal
Add a `/profile` route that lets an athlete view and edit the three sections of their onboarding data — Required identity, Recommended thresholds + HR, Goals events + goals — backed by the read endpoints from Task 8-1 and the existing onboarding write endpoints.

Depends on Task 8-1 (profile read endpoints must exist).

## Current code/status

- Task 8-1 ships `GET /api/v1/profile/{required,recommended,goals}` returning `ProfileRequiredResponse`, `ProfileRecommendedResponse`, `ProfileGoalsResponse`.
- Write endpoints already exist from Phase 4 — `POST /onboarding/required` (upsert), `/onboarding/recommended` (upsert by sport), `/onboarding/goals` (append). The Profile surface reuses these for saves.
- `ui/src/components/onboarding/{Required,Recommended,Goals}Step.vue` contain the form templates we'd otherwise re-implement.
- `ui/src/components/dashboard/DashboardSidebar.vue` already lists Profile as a sidebar nav item with `active: false`. Task 8-2 flips it to navigable; Task 8-5 finishes the activation polish.
- Pinia: `useOnboardingStore` exists; Profile gets its own store (`useProfileStore`) — different domain concern, different lifecycle (no wizard state, no `nextIncompleteStep` semantics).

## Component-reuse decision required in this task

Two paths for the three editable sections on the Profile page:

- **(a) Reuse the existing step components** by passing initial values + an "edit mode" flag that hides the Continue button. Saves duplication but couples onboarding wizard internals to the profile editor — any UX divergence later becomes painful.
- **(b) Build new Profile section components** (`ProfileRequiredSection.vue`, etc.) that look similar but are independently maintained. Some duplication; clean separation.

**Recommendation: (b)** — onboarding's UX intent (one-way wizard, "Continue" verb, locked steps) differs enough from profile editing (independent sections, save-in-place, no progression) that forcing one component to serve both surfaces accumulates conditionals fast. If/when drift confirms the duplication is painful, extract shared `<XxxFields>` components in a separate refactor.

## Acceptance criteria

**Types + service + store:**
- `ui/src/types/profile.ts` — TypeScript interfaces mirroring the three Profile response DTOs from Task 8-1. Reuse `SportThresholdsDto`, `EventDto`, `GoalDto` from `ui/src/types/onboarding.ts` (they're shared shapes).
- `ui/src/services/profile.ts` — three typed methods (`getRequired`, `getRecommended`, `getGoals`) over the new GET endpoints. All HTTP through the existing `ui/src/services/api.ts` wrapper.
- `ui/src/stores/profile.ts` — `useProfileStore` (Pinia) holding the three sections' loaded data + loading/error state. Exposes `loadRequired()` / `loadRecommended()` / `loadGoals()` actions and a generic `saveRequired(payload)` / `saveRecommended(payload)` / `saveGoals(payload)` that call the existing onboarding service methods (reuse `submitRequired` / `submitRecommended` / `submitGoals` from `ui/src/services/onboarding.ts`). After a save, re-fetch that section to refresh local cache.

**Route + view:**
- `ui/src/router/index.ts` — add `{ path: '/profile', name: 'profile', component: () => import('@/views/ProfileView.vue') }`. Lazy-load.
- `ui/src/views/ProfileView.vue` — top-level shell rendering inside the dashboard layout (sidebar + main). Three `ProfileRequiredSection`, `ProfileRecommendedSection`, `ProfileGoalsSection` components stacked vertically with section headers. Each section loads its own data on mount; each has its own save button + per-section save error state.

**Section components:**
- `ui/src/components/profile/ProfileRequiredSection.vue`
- `ui/src/components/profile/ProfileRecommendedSection.vue`
- `ui/src/components/profile/ProfileGoalsSection.vue`

Each uses `<script setup lang="ts">` + `useForm` + `toTypedSchema` per the wizard convention. Each reuses the existing zod schemas from `ui/src/schemas/onboarding.ts` (those validators apply equally to profile edits — same server validation rules). Each section renders the same fields as the corresponding wizard step component (same labels, same controls, same per-field validation). Differences from the wizard:
- No "Continue" button; "Save changes" button instead.
- No emit('next') — on success, show a brief inline "Saved" indicator and re-fetch the section's data.
- No stepper / wizard chrome.

**Sidebar nav navigability:**
- `ui/src/components/dashboard/DashboardSidebar.vue` — flip Profile's `active: false` to navigable. Use `useRoute()` to compute `active` dynamically (`route.name === 'profile'` for Profile, `route.name === 'home'` for Dashboard). Disabled nav items (Workouts / Progress / Goals) stay inert. Polish for the active highlight comes from Task 8-5.

**Tests:**
- `ui/src/stores/__tests__/profile.spec.ts` — store load + save round-trip.
- `ui/src/services/__tests__/profile.spec.ts` — service methods round-trip against mocked fetch.
- `ui/src/components/profile/__tests__/ProfileRequiredSection.spec.ts` — at least one mount + invalid-submit-shows-errors test.

**Build / test:**
- `pnpm run build` from `ui/` green.
- `pnpm test` from `ui/` green; test count grows by ≥3 tests.

## Files likely to change/add

- `ui/src/types/profile.ts` (new)
- `ui/src/services/profile.ts` (new)
- `ui/src/services/__tests__/profile.spec.ts` (new)
- `ui/src/stores/profile.ts` (new)
- `ui/src/stores/__tests__/profile.spec.ts` (new)
- `ui/src/views/ProfileView.vue` (new)
- `ui/src/components/profile/ProfileRequiredSection.vue` (new)
- `ui/src/components/profile/ProfileRecommendedSection.vue` (new)
- `ui/src/components/profile/ProfileGoalsSection.vue` (new)
- `ui/src/components/profile/__tests__/ProfileRequiredSection.spec.ts` (new)
- `ui/src/router/index.ts` (one route added)
- `ui/src/components/dashboard/DashboardSidebar.vue` (active state wired via useRoute)

## What NOT to modify

- Do not modify `ui/src/views/OnboardingView.vue` or the three `*Step.vue` components — Task 8-3 owns the band-aid removal there.
- Do not modify the existing onboarding store (`ui/src/stores/onboarding.ts`).
- Do not modify the existing onboarding service (`ui/src/services/onboarding.ts`) — Profile reuses its existing methods for saves.
- Do not modify the existing zod schemas — Profile reuses them as-is.
- Do not touch dashboard placeholder cards — Tasks 8-4 and 8-5 own those.
- Do not introduce a shared `<XxxFields>` extraction — accept the duplication for now.

## Test plan

1. `pnpm run build` green from `ui/`.
2. `pnpm test` from `ui/` green; new tests in count.
3. Manual smoke (assumes a fully-onboarded athlete via Task 8-1's profile reads working):
   - From the dashboard, click Profile in the sidebar → land on `/profile`.
   - Required section shows pre-filled values from `/api/v1/profile/required`.
   - Recommended section shows pre-filled HR + sport thresholds.
   - Goals section shows the existing events and goals.
   - Edit a value in Required → click "Save changes" → 204 from API → section re-fetches and shows the new value.
   - Repeat for Recommended and Goals.
   - Refresh `/profile` → all three sections re-load from the server with the latest values.
4. `git diff --stat` — only the named files touched.

## Suggested commit

Single commit:

```
feat: add /profile route with three editable sections

Profile view at /profile lets athletes view and edit the three
sections of their onboarding data. Backed by the GET endpoints from
Task 8-1 for reads; uses the existing onboarding POST endpoints for
writes (upsert / append semantics already match the edit-in-place UX).

New types, service, store, view, and three section components under
ui/src/{types,services,stores,views,components}/profile/. Sidebar
Profile nav item is now navigable; active state polish lands in Task
8-5. Schemas reused unchanged from onboarding.

Onboarding wizard surface untouched — Task 8-3 retires the
summary-card band-aid separately.
```
