# Task 8-5 — Wire the Resting HR card + finalize sidebar Profile activation

## Goal
Replace the "Resting HR" placeholder card on the dashboard with the athlete's saved `RestingHr` from the Athlete entity. Polish the sidebar Profile nav item's active-state highlight. Confirm the other three top-row stat cards (Weekly Load, Sleep Avg, Form/TSB) keep their placeholders unchanged — they're owned by Phases 11 / post-v1 / 13.

Depends on Task 8-1 (`GET /api/v1/profile/recommended` exposes `RestingHr` and `MaxHr`) and Task 8-2 (profile store has the data loaded).

## Current code/status

- `ui/src/views/HomeView.vue` renders four top-row stat cards as `PlaceholderCard` instances: Weekly Load, Resting HR, Sleep Avg, Form (TSB).
- `Athlete.RestingHr` is nullable on the entity. The onboarding Recommended step submits it (zod schema allows null). For Phase 5, an athlete may have completed onboarding without setting a resting HR.
- Task 8-1's `ProfileRecommendedResponse` includes `RestingHr` and `MaxHr`.
- Task 8-2's `useProfileStore` exposes `recommended` state with both fields. If not yet loaded, `loadRecommended()` fetches.
- Task 8-2 already made the sidebar Profile nav item navigable (using `useRoute()` for the active class). Task 8-5 finishes the polish — confirms the active highlight visually matches Dashboard's, confirms the route guard logic is correct.

## Acceptance criteria

**Resting HR card:**

- New file `ui/src/components/dashboard/RestingHrCard.vue`:
  - `<script setup lang="ts">`.
  - Calls `useProfileStore()` on mount; if `recommended` not loaded, calls `loadRecommended()`.
  - Renders one of three states:
    - **Loading:** dim "—" or the existing `PlaceholderCard` shape with no value (match the loading aesthetic of `PrimaryGoalCard` from Task 8-4 for consistency).
    - **Populated:** card title "RESTING HR" (small caps muted, matching `PlaceholderCard`), value as `<span class="font-mono text-3xl font-semibold">{{ restingHr }}</span>` with a `<span class="text-sm text-muted-foreground ml-1">bpm</span>` unit. Styling matches `PlaceholderCard` chrome.
    - **Empty (no value set):** card title "RESTING HR", value rendered as a dimmed "—", subtitle "Set in profile" as a `<router-link to="/profile">` styled as a small link.

**HomeView integration:**

- `ui/src/views/HomeView.vue` — in the top stat row, replace the `PlaceholderCard` instance for "Resting HR" with `<RestingHrCard />`. Other three top-row placeholders unchanged.

**Sidebar Profile active highlight (polish):**

- `ui/src/components/dashboard/DashboardSidebar.vue` — confirm the active class set in Task 8-2 (`useRoute().name === 'profile'`) renders the same visual treatment as the Dashboard active state (background `bg-sidebar-accent`, foreground `text-sidebar-foreground`, font-medium). Disabled items (Workouts / Progress / Goals) stay inert. No new behavior — just verifying the carry-over from Task 8-2 looks right.

**Decision:** **do not touch** the other three top-row placeholders (Weekly Load, Sleep Avg, Form/TSB). Their copy already explains which phase owns the wire-up. Modifying them would be scope creep.

**Test:**

- `ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts` — at least two cases:
  - Renders the bpm value when `recommended.restingHr` is set.
  - Renders empty state with "/profile" link when `restingHr` is null.

**Build / test:**

- `pnpm run build` green.
- `pnpm test` green; test count grows by ≥2 tests.

## Files likely to change/add

- `ui/src/components/dashboard/RestingHrCard.vue` (new)
- `ui/src/components/dashboard/__tests__/RestingHrCard.spec.ts` (new)
- `ui/src/views/HomeView.vue` — swap one PlaceholderCard for RestingHrCard.
- `ui/src/components/dashboard/DashboardSidebar.vue` — likely no functional change; just confirm/polish the active highlight from Task 8-2.

## What NOT to modify

- Do not touch the Weekly Load, Sleep Avg, or Form (TSB) placeholder cards — they're owned by Phases 11 / post-v1 / 13 respectively.
- Do not touch the Recent Activity card — Phase 11.
- Do not touch the Primary Goal card — Task 8-4 owns it.
- Do not introduce a "max HR" card — out of scope unless promoted.
- Do not add additional dashboard nav routes — the other sidebar items stay inert until their phases land.
- Do not modify any backend code — Task 8-5 is pure frontend.
- Do not modify the wizard or profile editor UI.

## Test plan

1. `pnpm run build` green.
2. `pnpm test` green; new tests in count.
3. Manual smoke (assumes Tasks 8-1, 8-2, 8-3, 8-4 landed):
   - Athlete who set RestingHr during onboarding → dashboard Resting HR card shows the value with "bpm" unit.
   - Athlete who left RestingHr null → card shows empty state with "Set in profile" link.
   - Click the link → land on `/profile` Recommended section, where the value can be set.
   - After saving in `/profile`, return to dashboard → card shows the new value (loadRecommended re-fetches on mount).
   - Sidebar: Dashboard and Profile both navigate; active highlight follows the current route. Other items stay inert.
4. `git diff --stat` — only the named files touched.

## Suggested commit

Single commit:

```
feat: wire Resting HR dashboard card + polish sidebar Profile state

Replace the Resting HR placeholder with a card that surfaces the
athlete's saved Athlete.RestingHr value (read via the Task 8-1 profile
endpoint + Task 8-2 store). Empty state links to /profile for an
athlete who hasn't set it yet.

Sidebar Profile nav active highlight polished — visual parity with
the Dashboard active state. Other three top-row stat cards (Weekly
Load, Sleep Avg, Form/TSB) keep their placeholders pending Phases 11
/ post-v1 / 13 respectively.

Closes Phase 8 dashboard warmup scope.
```
