# Task 8-4 — Wire the Primary Goal dashboard card

## Goal
Replace the "Primary Goal" placeholder card on the dashboard with real content: the athlete's highest-priority upcoming event, shown with name, sport, date, and weeks-to-go countdown.

Depends on Task 8-1 (`GET /api/v1/profile/goals` exists). Could also depend on Task 8-2 if we want to reuse its store, but Task 8-4 can stand alone with a thin frontend layer.

## Current code/status

- `ui/src/views/HomeView.vue` renders the dashboard shell when all three onboarding flags are true. The Primary Goal card is a `PlaceholderCard` instance with `title="Primary Goal"` and a subtitle pointing at this task.
- `Event` entity (Phase 2) carries `EventPriority` (`A` / `B` / `C`) and `EventDate`.
- Task 8-1 ships `GET /api/v1/profile/goals` returning `ProfileGoalsResponse` with `Events` and `Goals` arrays.
- The dashboard mockup the user shared shows: event name + date + weeks-out countdown + "ON TRACK" status badge + circular progress + current/target pace. This task covers the **event identity + countdown only**; "ON TRACK" status, progress ring, and pace targets are out of v1 scope (they require fitness/plan data from Phases 9–13).

## "Highest-priority event" selection rule

Sort the athlete's events by:

1. `Priority` ascending — A before B before C (treat the enum's natural order; A is most important).
2. Then `EventDate` ascending — the soonest A-event wins over a later A-event.
3. Exclude events whose `EventDate` is in the past (already happened — not "upcoming").

If the resulting list is empty (no upcoming events), render an empty state with a "Set a goal" affordance linking to `/profile` rather than rendering nothing or a stale placeholder. Empty state copy: "No upcoming events. Set one in your profile."

## Selection-on-server-vs-client decision required in this task

- **(a) Client-side selection.** Frontend pulls all events via `getGoals()` (already shipping in Task 8-2's store) and picks the highest-priority upcoming event locally. No backend change.
- **(b) Server-side selection.** Add a dedicated `GET /api/v1/profile/primary-event` returning a single `PrimaryEventResponse` or 404. Cleaner separation; one network request returns less data.

**Recommendation: (a)** — for v1 with the dashboard rendering only a handful of cards, the total event payload is small, and avoiding a new endpoint keeps the surface tighter. Promote to (b) if/when the events list grows or other surfaces want the same query.

## Acceptance criteria

**Types + store:**

- Reuse the `EventDto` type from `ui/src/types/onboarding.ts` (shared shape between onboarding and profile).
- `ui/src/stores/profile.ts` (from Task 8-2) exposes `goals` state with the `Events` array. Add a derived computed `primaryEvent` that applies the selection rule above and returns either an `EventDto` or `null`.

**Component:**

- New file `ui/src/components/dashboard/PrimaryGoalCard.vue`:
  - `<script setup lang="ts">`.
  - Calls `useProfileStore()` on mount; if `goals` not yet loaded, calls `loadGoals()`.
  - Renders one of three states:
    - **Loading:** dim "Loading…" or the existing `PlaceholderCard` shape with no value.
    - **Empty (no upcoming events):** card title "Primary Goal" with "No upcoming events." subtitle and a "Set a goal" `<router-link to="/profile">` styled as a link.
    - **Populated:** card title "Primary Goal", event `name` as a large heading, sport + formatted date below, and a weeks-to-go countdown ("14 weeks out", or "X weeks out" / "Tomorrow" / "Today" depending on proximity). Styling matches the existing `PlaceholderCard` card chrome (`rounded-lg border bg-card p-5`) for visual consistency.

**HomeView integration:**

- `ui/src/views/HomeView.vue` — in the middle row, replace the `PlaceholderCard` instance for "Primary Goal" with `<PrimaryGoalCard />`. The grid cell sizing (`lg:col-span-1`-equivalent) stays the same.

**Test:**

- `ui/src/components/dashboard/__tests__/PrimaryGoalCard.spec.ts` — at least two cases:
  - Renders the highest-priority upcoming event when multiple events exist with mixed priorities and dates.
  - Renders empty state when the athlete has no upcoming events.

**Build / test:**

- `pnpm run build` green.
- `pnpm test` green; test count grows by ≥2 tests.

## Files likely to change/add

- `ui/src/stores/profile.ts` — add `primaryEvent` computed.
- `ui/src/components/dashboard/PrimaryGoalCard.vue` (new)
- `ui/src/components/dashboard/__tests__/PrimaryGoalCard.spec.ts` (new)
- `ui/src/views/HomeView.vue` — swap PlaceholderCard for PrimaryGoalCard.

## What NOT to modify

- Do not add the "ON TRACK" status badge, circular progress ring, or current/target pace fields to the card — they need data from Phases 9–13.
- Do not introduce a new backend endpoint — use the existing `GET /profile/goals` from Task 8-1.
- Do not touch the other three top-row stat cards (Weekly Load, Resting HR, Sleep Avg, Form/TSB) — Resting HR is Task 8-5; the others are later phases.
- Do not touch the bottom-row Recent Activity card — Phase 11.
- Do not extend `EventPriority` or `Event` entity — out of scope.
- Do not modify `OnboardingView.vue` or any wizard component.

## Test plan

1. `pnpm run build` green.
2. `pnpm test` green; new tests in count.
3. Manual smoke (assumes Tasks 8-1 and 8-2 landed):
   - Athlete with no events → dashboard Primary Goal card shows empty state with link to `/profile`.
   - Athlete with one B-event 4 weeks out → card shows that event with "4 weeks out".
   - Athlete with one A-event 10 weeks out and one B-event 2 weeks out → card shows the A-event ("10 weeks out") because A trumps proximity.
   - Athlete with a past A-event and a future C-event → card shows the C-event (past events excluded).
   - Refresh dashboard → primary goal still correct (loadGoals fires on mount).
4. `git diff --stat` — only the named files touched.

## Suggested commit

Single commit:

```
feat: wire Primary Goal dashboard card to real event data

Replace the dashboard's Primary Goal placeholder with a card that
surfaces the athlete's highest-priority upcoming event. Selection
rule: priority A before B before C, then earliest date; past events
excluded. Empty state links to /profile to set a goal.

Reads events via the profile store's getGoals (Task 8-1 endpoint).
No new backend endpoint — selection is client-side. The "ON TRACK"
badge / progress ring / pace targets from the design mockup are
deferred to Phases 9-13 when the underlying data exists.
```
