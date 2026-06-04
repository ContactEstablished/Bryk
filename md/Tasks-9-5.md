# Task 9-5 — Wire the "This Week" dashboard card (Vue)

## Goal
Replace the "This Week" `PlaceholderCard` on the dashboard with a real card that lists the athlete's planned workouts for the current week, fetched from Task 9-4's endpoint. Mirrors the Task 8-4 (`PrimaryGoalCard`) / 8-5 (`RestingHrCard`) pattern.

Frontend-only. No backend changes.

## Depends on
- **Task 9-4** — `GET /api/v1/training/this-week` returning `ThisWeekResponse` (week range + ordered planned workouts).

## Required reading
- `ui/src/components/dashboard/PrimaryGoalCard.vue` and `RestingHrCard.vue` — **the reference cards.** Match: card chrome `rounded-lg border bg-card p-5`; title `text-[11px] font-semibold uppercase tracking-wider text-muted-foreground`; **loading gated on data presence, not the loading flag** (`v-if="store.<data>"` → … → `v-else "Loading…"`), the lesson from the /profile flash fix; empty-state styling.
- `ui/src/components/dashboard/__tests__/PrimaryGoalCard.spec.ts` — the spec pattern: `createTestingPinia({ createSpy: () => () => {}, initialState: { … } })`, `stubs: { RouterLink: RouterLinkStub }`, `attachTo: document.body`.
- `ui/src/views/HomeView.vue` — the middle-row grid; the "This Week" card is the `lg:col-span-2` cell (lines ~101–106). Swap ONLY that instance.
- `ui/src/services/profile.ts` — the `apiFetch<T>('/path')` service style + the null-guard.
- `ui/src/stores/profile.ts` — the Pinia setup-store pattern (`ref` state, `loadX` actions with loading/error flags, returned in the object). Decide: extend the profile store, or add a dedicated `training` store. **Recommendation: a dedicated `ui/src/stores/training.ts`** — This Week is its own domain concept; per CLAUDE.md "one store per domain concept."
- `ui/src/types/profile.ts` — the response-type mirroring convention for the new `training.ts` types.

## Acceptance criteria

**Types (`ui/src/types/training.ts`** — new):**
- `PlannedWorkoutResponse` and `ThisWeekResponse` TS interfaces mirroring Task 9-4's JSON exactly (sport union reuses `Sport` from `@/types/onboarding`; dates are `'YYYY-MM-DD'` strings). Verify field names against the actual 9-4 response, don't assume.

**Service (`ui/src/services/training.ts`** — new):**
- `getThisWeek(): Promise<ThisWeekResponse>` via `apiFetch<ThisWeekResponse>('/training/this-week')` with the same null-guard pattern as `services/profile.ts`.

**Store (`ui/src/stores/training.ts`** — new, recommended):**
- `thisWeek` ref (`ThisWeekResponse | null`), `loadingThisWeek`, `thisWeekError`, and a `loadThisWeek()` action mirroring `loadGoals()` in `stores/profile.ts` (set loading, clear error, try/catch, finally clear loading).

**Component (`ui/src/components/dashboard/ThisWeekCard.vue`** — new):**
- `<script setup lang="ts">`; calls the store on mount and `loadThisWeek()` if `thisWeek` not loaded.
- Three render states, chrome matching the sibling cards:
  - **Loading:** gated on `!store.thisWeek` → "Loading…".
  - **Empty (loaded, no sessions this week):** static copy **"No sessions planned this week."** — **no CTA link** (decided: the plan-authoring UI is Task 9-6; this card stays static. A future polish may add a link to /training once 9-6 ships — leave a one-line `<!-- TODO(9-6+) -->` comment, do not wire it).
  - **Populated:** title "This Week" + the week range, and the list of planned workouts grouped/ordered by day — each row showing day, sport, and title (and planned duration/load if present). Keep it a clean list; this is the `lg:col-span-2` wide cell, so a simple day-by-day list reads well.

**HomeView integration (`ui/src/views/HomeView.vue`):**
- Import `ThisWeekCard`; replace the `<PlaceholderCard title="This Week" … />` inside the `<div class="lg:col-span-2">` with `<ThisWeekCard />`. Keep the wrapping `lg:col-span-2` div and its sizing exactly. Leave the Primary Goal card (8-4) and all other cards untouched.

**Test (`ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts`** — new, ≥2 cases):**
- Seed `training` store via `initialState` with a `thisWeek` containing ≥2 planned workouts → asserts the sessions render (titles/sports/days present).
- Seed `thisWeek` with an empty workout list → asserts the static "No sessions planned this week." empty copy renders and **no** `RouterLink` is present.
- (Optional 3rd) no seed → asserts "Loading…".

**Build / test:**
- `pnpm run build` green from `ui/`.
- `pnpm test` green; count up by ≥2.

## Files likely to change/add
- `ui/src/types/training.ts` (new)
- `ui/src/services/training.ts` (new)
- `ui/src/stores/training.ts` (new)
- `ui/src/components/dashboard/ThisWeekCard.vue` (new)
- `ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts` (new)
- `ui/src/views/HomeView.vue` — swap one PlaceholderCard + add an import

## What NOT to modify
- Do not touch the Primary Goal card (Task 8-4) or the Resting HR card (8-5).
- Do not touch the other top-row placeholders (Weekly Load, Sleep Avg, Form/TSB) or the Recent Activity card — later phases.
- Do not add a CTA link in the empty state — decided static for Phase 9.
- Do not build the plan-authoring form — that's Task 9-6.
- Do not call `fetch`/`axios` directly from the component — go through `services/training.ts` (CLAUDE.md).
- Do not put the card's data fetching in `HomeView` — the card owns its own load-on-mount, like the sibling cards.

## Test plan
1. `pnpm run build` green.
2. `pnpm test` green; new tests in the count.
3. Manual smoke (Tasks 9-2…9-4 landed, a plan with sessions this week seeded via the 9-3 API or 9-6 UI): dashboard "This Week" card lists this week's sessions by day; an athlete with no plan sees "No sessions planned this week."; refresh re-fetches on mount; no flash of empty content before "Loading…".
4. `git diff --stat` — only the new training type/service/store/card/spec + HomeView.

## Suggested commit
```
feat: wire This Week dashboard card to planned-workout data

Replace the This Week placeholder with a card listing the athlete's
planned workouts for the current week, fetched from
GET /api/v1/training/this-week via a new training store/service. Loading
is gated on data presence (no pre-load flash); the empty state is static
"No sessions planned this week." pending the plan-authoring UI (Task 9-6).
```
