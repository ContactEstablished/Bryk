# Task 17-3 — `GoalsView` page + goals store/service + nav live

## Surface
Frontend only. A new `/goals` route + `GoalsView.vue` (Goals section + Events section) reading from
17-1's new GET endpoints via a new `services/goals-events.ts` read layer and a new Pinia `goals` store;
the read-only Events cards render `Event.Notes`, A/B/C priority styling, the ported `ProgressRing`
countdown, and a linked-plan chip that navigates to the plan browser; the sidebar `Goals` item goes
**live** (currently inert "soon"). **CRUD forms are 17-4** — this task ships the read/display surface +
nav; leave a slot for the forms. **No new package.**

## Why
The ROADMAP's headline Phase-17 deliverable: the Goals nav item goes live with a real page. Splitting
read/display (17-3) from CRUD (17-4) keeps each task surgical — 17-3 proves the new GET endpoints and the
shared `ProgressRing` render correctly against seeded data before the forms wrap the existing
POST/PUT/DELETE. The linked-plan chip is the first UI surfacing of the dormant plan↔event link
(display-only per the ROADMAP).

## Depends on
- **Task 17-1** — `GET /events` (`upcoming` filter, `Notes`, `LinkedPlans`), `GET /goals`
  (`daysRemaining`, `status`) — the endpoints this page reads.
- **Task 17-2** — `ProgressRing.vue` (the event countdown ring; GoalsView passes the true linked-plan
  `[start, target]` elapsed fraction when a plan is linked, else the creation-window fallback).
- **Phase 16 precedent** — `CalendarView.vue` + `stores/calendar.ts` + `services/calendar.ts` +
  `types/calendar.ts` as the view/store/service/types shape and the loading/empty/error state pattern.
- **Phase 13** — the plan browser route `/plans/:id` the linked-plan chip navigates to.

## Required reading
- `ui/src/views/CalendarView.vue` — the **view template to mirror**: header + loading skeleton + empty
  state + error banner + `onMounted` store load. Same altitude for `GoalsView`.
- `ui/src/stores/calendar.ts` — the store shape (state refs, `loading`/`error`, a `load*` action) to
  mirror for the new `goals` store.
- `ui/src/services/calendar.ts` — the `apiFetch` + null-guard service pattern to mirror for
  `services/goals-events.ts`.
- `ui/src/services/events.ts`, `ui/src/services/goals.ts` — the **existing write** services (POST/PUT/
  DELETE) that already exist; **do not duplicate** them. Add only the **GET** functions here (or in the
  new `goals-events.ts` read module).
- `ui/src/types/onboarding.ts` (`EventDto`, `GoalDto`, `Sport`, `EventPriority`, `GoalType`) and
  `ui/src/types/profile.ts` (`EventResponse`, `GoalResponse`) — the existing frontend types to extend
  with the new list-response shapes.
- `ui/src/components/layout/AppSidebar.vue` — the `trainItems` array; the `Goals` entry is currently
  inert (`{ icon: Target, label: 'Goals' }`, no `to`/`routeName`, renders a "soon" badge). Make it live.
- `ui/src/router/index.ts` — add the lazy `/goals` route next to `/calendar`.
- `ui/src/components/profile/ProfileEventCard.vue` / `ProfileGoalCard.vue` — the existing per-item
  cards; **reference for styling/labels**, but the Goals page cards are new read-display components (the
  Profile cards are edit forms, reused/wrapped in 17-4).
- `ui/src/components/common/TypePill.vue` + `pills.ts` — the sport/type pill for the goal/event cards.
- `ui/src/components/dashboard/PrimaryGoalCard.vue` — the `daysUntil`/`formattedDate` UTC date math to
  reuse for the event countdown (lift into a small helper if shared).

## Acceptance criteria

### Types (`ui/src/types/goals.ts`, new)
- `LinkedPlan { id: string; name: string }`.
- `EventListItem extends EventResponse { linkedPlans: LinkedPlan[] }` (mirrors 17-1's
  `EventListItemResponse`).
- `GoalStatus = 'NoDate' | 'Upcoming' | 'DueSoon' | 'Overdue'`.
- `GoalListItem extends GoalResponse { daysRemaining: number | null; status: GoalStatus }`.

### Read service (`ui/src/services/goals-events.ts`, new)
- `getEvents(upcoming?: boolean): Promise<EventListItem[]>` → `GET /events` (append `?upcoming=true` when
  set), null-guarded like `getCalendarFeed`.
- `getEvent(id: string): Promise<EventListItem>` → `GET /events/{id}`.
- `getGoalsList(): Promise<GoalListItem[]>` → `GET /goals`.
- Keep the existing write services (`services/events.ts`, `services/goals.ts`) untouched.

### Store (`ui/src/stores/goals.ts`, new)
- State: `events = ref<EventListItem[] | null>(null)`, `goals = ref<GoalListItem[] | null>(null)`,
  `loading = ref(false)`, `error = ref<ApiError | Error | null>(null)`.
- `loadAll()` — fetches events + goals in parallel (`Promise.all([getEvents(), getGoalsList()])`), sets
  state, maps errors into `error` (mirror the calendar store's try/finally). A computed
  `upcomingEvents` (filter `eventDate >= todayUtc`, priority A<B<C then date — reuse the sort in
  `stores/profile.ts`'s `primaryEvent`).
- This store is **separate** from `stores/profile.ts` — the profile store keeps its `/profile/goals`
  composition for the Profile page; the Goals page uses the new first-class endpoints. (Flag the mild
  duplication in the commit body; do not refactor the profile store in this task.)

### `GoalsView.vue` (`ui/src/views/GoalsView.vue`, new)
- `onMounted` → `store.loadAll()`. Header: "Goals" title + a short subtitle. Loading skeleton, empty
  state (fresh athlete → "Add your first event/goal" pointing at the on-page CRUD from 17-4), and an
  error banner + retry — mirror `CalendarView`.
- **Events section** (date-ordered; render `upcomingEvents` first, then past events in a collapsed/muted
  group or below):
  - A new read-display `GoalsEventCard.vue` per event: event name, sport pill (`TypePill`),
    formatted date, **A/B/C priority styling** (priority badge — A emphasized, B/C progressively muted),
    the `ProgressRing` countdown (center = days/weeks-to-go via `useCountUp`; fill fraction = elapsed of
    `[linkedPlan.startDate ?? <creation-fallback>, eventDate]` — when a plan is linked and 17-4/13 expose
    its `startDate`, use it; else the rolling-horizon fallback from 17-2), **`Notes` rendered inline**
    (finally — the ROADMAP calls this out), and a **linked-plan chip** per `linkedPlans` entry:
    `<RouterLink :to="`/plans/${plan.id}`">` showing the plan name (navigates to the plan browser).
    No plan chip when `linkedPlans` is empty.
- **Goals section** (date-ordered):
  - A new read-display `GoalsGoalCard.vue` per goal: `GoalType` `TypePill`, description, target-date
    countdown, and a **status pill** driven by `status` (`Overdue` = warn/destructive, `DueSoon` =
    accent, `Upcoming` = neutral, `NoDate` = muted "No date"). Show `daysRemaining` ("in N days" /
    "N days ago" / "today").
- **Leave a CRUD mount point**: an "Add event" / "Add goal" affordance + a slot/region where 17-4 mounts
  the vee-validate forms. 17-3 may stub the buttons (disabled or a "coming next" no-op) — 17-4 wires them.
  Do not build the forms here.

### Router + nav
- `ui/src/router/index.ts`: add `{ path: '/goals', name: 'goals', component: () => import('@/views/GoalsView.vue') }`
  (lazy, next to `/calendar`).
- `ui/src/components/layout/AppSidebar.vue`: change the `Goals` `trainItems` entry from the inert
  `{ icon: Target, label: 'Goals' }` to `{ icon: Target, label: 'Goals', to: '/goals', routeName: 'goals' }`.
  The "soon" badge disappears (the template branches on `item.to`); the mobile tab bar picks it up
  automatically via the existing `mobileItems` filter. **Note in the commit** that the mobile tab bar
  gains one item (as Phase 16 flagged for Calendar) — IA pruning is the user's call, do not prune.

### Tests
- `ui/src/stores/__tests__/goals.spec.ts` — `loadAll` populates `events`/`goals` from mocked services;
  `upcomingEvents` filters past + sorts by priority then date; error path sets `error`.
- `ui/src/components/goals/__tests__/GoalsEventCard.spec.ts` — renders event name, `Notes`, priority
  badge, `ProgressRing`, and a linked-plan `RouterLink` to `/plans/{id}` (use `RouterLinkStub`); no chip
  when `linkedPlans` empty.
- `ui/src/components/goals/__tests__/GoalsGoalCard.spec.ts` — renders description + status pill for each
  `status`; `daysRemaining` phrasing for future/past/today.
- `ui/src/views/__tests__/GoalsView.spec.ts` — mounts with a mocked store: renders event + goal cards;
  empty state when arrays empty; error banner on error. (Sidebar nav array isn't unit-tested per the
  Phase-16 precedent — cover in manual smoke.)
- `pnpm run build` (vue-tsc) green; `pnpm test` green (`--no-file-parallelism` for a clean exit if the
  known transient worker crash appears with all tests passing).

## What NOT to modify
- **No new package.** Reuse `ProgressRing` (17-2), `TypePill`, `useCountUp`, existing UI primitives.
- **Do not** build the CRUD forms here — that's 17-4. Leave the mount point/buttons stubbed.
- **Do not** duplicate the existing write services — add only the GET read functions.
- **Do not** refactor `stores/profile.ts` or the Profile `Goals` section — the Goals page uses a separate
  store/endpoints; the mild duplication is intentional and flagged, not fixed here.
- **Do not** add a plan↔event **write** control (no "link plan" button) — the link is display-only in
  Phase 17.
- **Do not** read athlete identity in any service — the API scopes to the current athlete.
- **Do not** prune the mobile tab bar — flag the added item in the commit, leave IA to the user.

## Suggested commit
```
feat(ui): Goals page + goals store; Goals nav goes live

New /goals route + GoalsView (Events + Goals sections) reading 17-1's new
GET endpoints via a goals-events read service and a dedicated goals Pinia
store. Read-display cards: events render Notes inline (finally), A/B/C
priority styling, the shared ProgressRing countdown, and a linked-plan
chip navigating to the plan browser (display-only link); goal cards render
a status pill from the server-computed status + daysRemaining. Sidebar
Goals item goes live (was inert "soon"); mobile tab bar gains it. CRUD
forms land in 17-4 — this task ships the read/display surface + nav and a
stubbed add affordance. Vitest covers the store, both cards, and the view.
```
