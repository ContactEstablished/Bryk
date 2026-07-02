# Impl 17-3 — Build order: `GoalsView` page + goals store/service + nav live

**Executor:** GLM 5.2. **Acceptance contract:** `md/Tasks-17-3.md`. **Decision lock:** ADR-0003
(TrainingPlan/Event field shapes — the linked-plan chip target), Phase 17 ROADMAP decision
("plan↔event link is display-only").
**Scope:** Frontend only. No new npm package. Depends on Task 17-1 (backend GET endpoints — must be
merged/available) and Task 17-2 (`ProgressRing.vue` + `buildRingGeometry` — must be merged/available).
CRUD forms are out of scope (Task 17-4); this task stubs the add affordance only.

## Step 0 — Pre-flight

- `git status` clean. `pnpm run build` (from `ui/`) green; `pnpm test` green.
- **Confirm 17-1 and 17-2 are actually merged** before starting — this task cannot proceed without
  them:
  - Backend: `GET /api/v1/events` (with `upcoming` filter, `Notes`, `LinkedPlans`), `GET /api/v1/events/{id}`,
    `GET /api/v1/goals` (with `daysRemaining`/`status`) must exist and respond. Smoke-check with the dev
    API running (`EventListItemResponse`/`GoalListItemResponse` shapes per `Tasks-17-1.md`).
  - Frontend: `ui/src/components/common/ProgressRing.vue` and `ui/src/lib/progressRing.ts`
    (`buildRingGeometry`) must exist. If either dependency is missing, **STOP** — do not reimplement
    17-1/17-2 inline; flag the gap and wait.
- Re-read `md/Tasks-17-3.md` in full. Open: `ui/src/views/CalendarView.vue`, `ui/src/stores/calendar.ts`,
  `ui/src/services/calendar.ts`, `ui/src/services/events.ts`, `ui/src/services/goals.ts`,
  `ui/src/types/onboarding.ts`, `ui/src/types/profile.ts`, `ui/src/components/layout/AppSidebar.vue`,
  `ui/src/router/index.ts`, `ui/src/components/profile/ProfileEventCard.vue` /
  `ProfileGoalCard.vue`, `ui/src/components/common/TypePill.vue` + `pills.ts`,
  `ui/src/components/dashboard/PrimaryGoalCard.vue`, `ui/src/stores/profile.ts` (`primaryEvent`'s
  priority/date sort), `ui/src/composables/useCountUp.ts`, `ui/src/components/layout/AppShell.vue`.
- **Verify the 17-1 response shapes live** by hitting `GET /api/v1/events` and `GET /api/v1/goals`
  against the dev API (seeded athlete) — confirm exact field names/casing (`linkedPlans` vs
  `LinkedPlans`, `daysRemaining`, `status` as a string union) before writing types. .NET's default JSON
  casing is camelCase; pin it before Step 1.

## Step 1 — Types (`ui/src/types/goals.ts`, new)

Mirror 17-1's DTOs, extending the existing `EventResponse`/`GoalResponse` from `types/profile.ts`:

```ts
import type { EventResponse, GoalResponse } from '@/types/profile'

export interface LinkedPlan {
  id: string
  name: string
}

export interface EventListItem extends EventResponse {
  linkedPlans: LinkedPlan[]
}

export type GoalStatus = 'NoDate' | 'Upcoming' | 'DueSoon' | 'Overdue'

export interface GoalListItem extends GoalResponse {
  daysRemaining: number | null
  status: GoalStatus
}
```

**Verify:** `pnpm run build` green (type-checks; no consumers yet).

## Step 2 — Read service (`ui/src/services/goals-events.ts`, new)

Mirror `services/calendar.ts`'s `apiFetch` + null-guard pattern. **Do not touch**
`services/events.ts` / `services/goals.ts` (the existing write services).

```ts
import { apiFetch } from '@/services/api'
import type { EventListItem, GoalListItem } from '@/types/goals'

export async function getEvents(upcoming?: boolean): Promise<EventListItem[]> {
  const qs = upcoming ? '?upcoming=true' : ''
  const result = await apiFetch<EventListItem[]>(`/events${qs}`)
  if (result === null) throw new Error('Unexpected empty response from /events')
  return result
}

export async function getEvent(id: string): Promise<EventListItem> {
  const result = await apiFetch<EventListItem>(`/events/${id}`)
  if (result === null) throw new Error(`Unexpected empty response from /events/${id}`)
  return result
}

export async function getGoalsList(): Promise<GoalListItem[]> {
  const result = await apiFetch<GoalListItem[]>('/goals')
  if (result === null) throw new Error('Unexpected empty response from /goals')
  return result
}
```

**Verify:** `pnpm run build` green.

## Step 3 — Store (`ui/src/stores/goals.ts`, new)

Mirror `stores/calendar.ts`'s setup-store shape; the `upcomingEvents` sort mirrors
`stores/profile.ts`'s `primaryEvent` (priority A<B<C, then date), but **filters rather than picks one**.

```ts
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError } from '@/services/api'
import { getEvents, getGoalsList } from '@/services/goals-events'
import type { EventListItem, GoalListItem } from '@/types/goals'

function utcTodayIso(): string {
  const now = new Date()
  const yyyy = now.getUTCFullYear()
  const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(now.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

export const useGoalsStore = defineStore('goals', () => {
  const events = ref<EventListItem[] | null>(null)
  const goals = ref<GoalListItem[] | null>(null)
  const loading = ref(false)
  const error = ref<ApiError | Error | null>(null)

  async function loadAll() {
    loading.value = true
    error.value = null
    try {
      const [ev, gl] = await Promise.all([getEvents(), getGoalsList()])
      events.value = ev
      goals.value = gl
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loading.value = false
    }
  }

  // Upcoming events (today inclusive), priority A<B<C then soonest date —
  // same sort as stores/profile.ts's primaryEvent, but the full filtered list.
  const upcomingEvents = computed<EventListItem[]>(() => {
    const all = events.value
    if (!all) return []
    const today = utcTodayIso()
    return [...all.filter((e) => e.eventDate >= today)].sort((a, b) =>
      a.priority !== b.priority
        ? a.priority.localeCompare(b.priority)
        : a.eventDate.localeCompare(b.eventDate),
    )
  })

  return { events, goals, loading, error, loadAll, upcomingEvents }
})
```

**Note in the commit body** (per the task): this store is intentionally separate from
`stores/profile.ts` — do not refactor `profile.ts`'s `/profile/goals` composition in this task.

**Verify:** `pnpm run build` green.

## Step 4 — Store tests (`ui/src/stores/__tests__/goals.spec.ts`)

Mock the service module (`vi.mock('@/services/goals-events', ...)`) since, unlike
`stores/analytics.ts`, `loadAll` calls the network layer directly.

Tests:
- `loadAll` populates `events`/`goals` from mocked `getEvents`/`getGoalsList` resolving concurrently
  (assert `Promise.all` semantics via both arrays populated after one `await store.loadAll()`).
- `upcomingEvents`: given a past event, a today-dated event, and two future events with priorities
  B and A (A later date, B sooner date) — asserts: past event excluded; A-event sorts before B-event
  despite the later date (priority trumps proximity, mirroring `PrimaryGoalCard.spec.ts`'s existing
  assertion); today-dated event included.
- Error path: mocked `getEvents` (or `getGoalsList`) rejects → `error.value` is set, `loading.value`
  returns to `false`.

**Verify:** `pnpm test` — `goals.spec.ts` passes.

## Step 5 — `GoalsEventCard.vue` (`ui/src/components/goals/GoalsEventCard.vue`, new)

Props: `defineProps<{ event: EventListItem }>()`. Read-display only (no form). Mirror
`ProfileEventCard.vue`'s field set for labels/copy, but render, not edit:

- Event name (heading), `TypePill` for `event.sport` (via `sportToPillKind`), formatted date (lift
  the UTC-safe date formatting from `PrimaryGoalCard.vue`'s `formattedDate`/`daysUntil` into a small
  shared helper — e.g. add `formatEventDate(dateStr: string)` and `daysUntil(dateStr: string)` to a new
  `ui/src/lib/dateFormat.ts`, since both `PrimaryGoalCard` and this card need identical UTC-stable math;
  `PrimaryGoalCard.vue`'s inline copies stay as-is per 17-2's "do not change `PrimaryGoalCard`" fence —
  **do not refactor `PrimaryGoalCard.vue` in this task**, only add the new shared helper and use it here).
- Priority badge: A/B/C styling — A emphasized (e.g. `bg-destructive/10 text-destructive` or the
  existing accent-strong token), B/C progressively muted (`text-subtle`/`text-faint` borders). Pin one
  concrete class set; keep it visually distinct from the compliance dots (different domain).
- `ProgressRing` countdown: center = `daysUntil`/weeks via `useCountUp` (mirror `PrimaryGoalCard`'s
  `weeks`/`animatedWeeks` pattern locally in this card — do not import from `PrimaryGoalCard.vue`).
  Fill fraction: when `event.linkedPlans.length > 0` **and** a linked plan's `startDate` is available,
  use `[startDate, event.eventDate]` elapsed fraction; else fall back to the rolling-horizon fraction
  (`clamp(1 - daysUntil / 168, 0, 1)`, same constant as 17-2's `PrimaryGoalCard` fallback). **Note:**
  `EventListItem.linkedPlans` (`LinkedPlan { id; name }`) carries no `startDate` per 17-1's DTO — so in
  practice this card always uses the rolling-horizon fallback until a future task adds `startDate` to
  `LinkedPlanDto`. Implement the true-window branch as dead-but-ready code guarded by an optional
  `startDate` field so it activates for free later; do not add the field to the backend DTO in this task.
- `event.notes` rendered inline beneath the countdown (a `<p>`, muted, only when `notes` is non-empty).
- Linked-plan chip(s): `<RouterLink v-for="plan in event.linkedPlans" :key="plan.id" :to="`/plans/${plan.id}`">{{ plan.name }}</RouterLink>`
  styled as a small pill/chip. **No chip when `linkedPlans` is empty** (`v-if`/`v-for` naturally renders
  nothing).
- Use `card-surface` (or the existing card utility class seen in `PrimaryGoalCard.vue`) for the
  container.

**Verify:** `pnpm run build` green (component compiles, no consumers yet).

## Step 6 — `GoalsEventCard` tests (`ui/src/components/goals/__tests__/GoalsEventCard.spec.ts`)

Mirror `DayDetailPopover.spec.ts`'s `RouterLinkStub` mount pattern.

Tests:
- Renders event name, `Notes` text, priority badge text (`A`/`B`/`C`), and a `ProgressRing`
  (`findComponent` by name/stub — stub `ProgressRing` if it pulls in animation timers, matching how
  `PrimaryGoalCard.spec.ts` handles `useCountUp`'s reduced-motion snap).
- Given `linkedPlans: [{ id: 'plan-1', name: 'Marathon Build' }]`, renders a `RouterLinkStub` with
  `to === '/plans/plan-1'` and text `'Marathon Build'`.
- Given `linkedPlans: []`, no `RouterLinkStub` is rendered.

**Verify:** `pnpm test` — `GoalsEventCard.spec.ts` passes.

## Step 7 — `GoalsGoalCard.vue` (`ui/src/components/goals/GoalsGoalCard.vue`, new)

Props: `defineProps<{ goal: GoalListItem }>()`. Read-display only. Mirror `ProfileGoalCard.vue`'s
field set for labels/copy, rendered not edited:

- `GoalType` `TypePill` (goal.type is `'General' | 'EventDriven'` — extend `pills.ts`'s
  `sportToPillKind` mapping *only if* a goal-type pill kind is actually needed; simplest: render the
  type as plain text/badge without extending the sport-keyed pill map, since `PillKind` is sport-shaped
  — **do not** force `GoalType` through `sportToPillKind`; use a small local `goalTypeLabel` map or a
  neutral `TypePill kind="neutral"` with the type text as slot content).
- `goal.description` (body text).
- Target-date countdown: reuse the `formatEventDate`/date-math helper from Step 5 where applicable, or
  render `goal.targetDate` directly with `'No target date'` when null.
- Status pill driven by `goal.status`:
  - `'Overdue'` → warn/destructive styling.
  - `'DueSoon'` → accent styling.
  - `'Upcoming'` → neutral styling.
  - `'NoDate'` → muted, label text `'No date'`.
- `daysRemaining` phrasing: `'in N days'` (positive), `'N days ago'` (negative, use `Math.abs`),
  `'today'` (`daysRemaining === 0`), nothing/omit when `daysRemaining === null`.

**Verify:** `pnpm run build` green.

## Step 8 — `GoalsGoalCard` tests (`ui/src/components/goals/__tests__/GoalsGoalCard.spec.ts`)

Tests (one case per `status` value, pin exact phrasing):
- `status: 'Overdue', daysRemaining: -3` → renders `'3 days ago'` + the overdue/destructive pill text.
- `status: 'DueSoon', daysRemaining: 5` → renders `'in 5 days'` + the due-soon pill.
- `status: 'DueSoon', daysRemaining: 0` → renders `'today'`.
- `status: 'Upcoming', daysRemaining: 30` → renders `'in 30 days'` + the upcoming/neutral pill.
- `status: 'NoDate', daysRemaining: null` → renders `'No date'`, no "in N days"/"ago" text.
- Renders `goal.description`.

**Verify:** `pnpm test` — `GoalsGoalCard.spec.ts` passes.

## Step 9 — `GoalsView.vue` (`ui/src/views/GoalsView.vue`, new)

Mirror `CalendarView.vue`'s altitude: `AppShell` wrapper, `onMounted` load, loading skeleton, empty
state, error banner.

```ts
import { computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import AppShell from '@/components/layout/AppShell.vue'
import GoalsEventCard from '@/components/goals/GoalsEventCard.vue'
import GoalsGoalCard from '@/components/goals/GoalsGoalCard.vue'
import { Button } from '@/components/ui/button'
import { useGoalsStore } from '@/stores/goals'

const store = useGoalsStore()
const { events, goals, loading, error, upcomingEvents } = storeToRefs(store)

onMounted(() => {
  void store.loadAll()
})

const pastEvents = computed(() => {
  // date-desc so the most recent past event is nearest the upcoming section.
  const today = /* utcTodayIso() — lift or inline, matching store's helper */
  return (events.value ?? [])
    .filter((e) => e.eventDate < today)
    .sort((a, b) => b.eventDate.localeCompare(a.eventDate))
})

const isEmpty = computed(
  () => !loading.value && !error.value && (events.value?.length ?? 0) === 0 && (goals.value?.length ?? 0) === 0,
)
```

Template shape (per the task's acceptance criteria):
- Header via `AppShell` title="Goals" + a short subtitle (e.g. "Events, goals & countdowns").
- Loading skeleton: a few muted rectangles (mirror `CalendarView`'s `animate-pulse` blocks — count/shape
  is presentational, keep simple: e.g. 3 event-card-height rectangles + 3 goal-card-height rectangles).
- Empty state: "Add your first event/goal" — a message plus the **stubbed** add affordance (below);
  do **not** point at a working form (17-4 wires it).
- Error state: banner with `error.message` + a retry button calling `store.loadAll()` — mirror
  `CalendarView`'s error banner pattern (or the simpler `error && !feed` block).
- **Events section**: heading "Events" + an "Add event" button (**stubbed**: `disabled` or a no-op
  click handler — do not mount a form). Render `upcomingEvents` first (each via `GoalsEventCard`), then
  a visually muted/collapsed "Past events" sub-heading + `pastEvents` list below.
- **Goals section**: heading "Goals" + an "Add goal" button (**stubbed** identically). Render
  `goals.value` (already date-asc per 17-1's repo ordering — no extra client sort needed) via
  `GoalsGoalCard`.
- Leave a clearly-commented mount point (e.g. `<!-- 17-4 mounts the event/goal forms here -->`) near
  each stubbed button so the next task has an obvious anchor.

**Verify:** `pnpm run build` green.

## Step 10 — `GoalsView` tests (`ui/src/views/__tests__/GoalsView.spec.ts`)

Mirror the mocked-store mount pattern from `PrimaryGoalCard.spec.ts` (`createTestingPinia`) or
`CalendarView`-adjacent view specs — mount with a testing Pinia seeded with `events`/`goals`/`loading`/
`error` state (stub `RouterLink`; stub child cards or let them render — prefer letting them render since
they're already unit-tested in isolation, to catch wiring bugs).

Tests:
- Given seeded `events` + `goals`, renders a `GoalsEventCard`-shaped block and a `GoalsGoalCard`-shaped
  block for each item (assert via text content, e.g. event/goal names appear).
- Given empty arrays (`events: [], goals: []`, `loading: false`), renders the empty-state copy.
- Given `error` set, renders the error banner text.

**Verify:** `pnpm test` — `GoalsView.spec.ts` passes.

## Step 11 — Router (`ui/src/router/index.ts`)

**Edit** — add next to `/calendar` (alphabetical-ish with the existing routes):

```ts
{
  path: '/goals',
  name: 'goals',
  component: () => import('@/views/GoalsView.vue'),
},
```

**Verify:** `pnpm run build` green.

## Step 12 — Sidebar nav (`ui/src/components/layout/AppSidebar.vue`)

**Edit** the `trainItems` array — change the inert `Goals` entry:

```ts
// before
{ icon: Target, label: 'Goals' },
// after
{ icon: Target, label: 'Goals', to: '/goals', routeName: 'goals' },
```

The template already branches on `item.to` (the "soon" badge `<div v-else>` disappears automatically
once `to` is set — no template change needed). `mobileItems`'s existing `.filter((i) => i.to != null)`
picks it up automatically — **do not** edit `mobileItems` or prune any item. **Note in the commit body**
that the mobile tab bar gains one item (now 6 + Profile = 7), same as Phase 16 flagged for Calendar —
leave the IA call to the user.

**Verify:** `pnpm run build` green. Manual: `/goals` reachable via the sidebar `Goals` link; the "soon"
badge is gone; the link highlights active on `/goals`.

## Step 13 — Final verification + commit

- `pnpm run build` (vue-tsc) green.
- `pnpm test` green — full suite, including the 4 new spec files
  (`goals.spec.ts`, `GoalsEventCard.spec.ts`, `GoalsGoalCard.spec.ts`, `GoalsView.spec.ts`) plus all
  pre-existing specs unaffected. Use `pnpm exec vitest run --no-file-parallelism` if the known transient
  worker crash appears with all tests passing (per CLAUDE.md/Tasks-17-3 note).
- Manual smoke: navigate to `/goals` — events + goals render against seeded data; `ProgressRing`
  countdown draws in; a seeded event with a linked plan shows a chip that navigates to `/plans/:id`;
  `Notes` render inline; priority A/B/C styling is visually distinct; goal status pills match
  `Overdue`/`DueSoon`/`Upcoming`/`NoDate`; stubbed "Add event"/"Add goal" buttons do nothing (no
  console error); fresh-athlete swap (temporary `DevAuth:CurrentAthleteId`, then restore) shows the
  empty state with no console errors.
- `git diff --stat` — only: `ui/src/types/goals.ts`, `ui/src/services/goals-events.ts`,
  `ui/src/stores/goals.ts` + spec, `ui/src/components/goals/*` + `__tests__/*`,
  `ui/src/views/GoalsView.vue` + spec, `ui/src/router/index.ts` (one route added),
  `ui/src/components/layout/AppSidebar.vue` (one item edited), and (if added per Step 5)
  `ui/src/lib/dateFormat.ts`. No `package.json` changes. No edits to `services/events.ts`,
  `services/goals.ts`, `stores/profile.ts`, `components/profile/*`, or `PrimaryGoalCard.vue`.
- Commit:

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
