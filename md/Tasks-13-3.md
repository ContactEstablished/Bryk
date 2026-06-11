# Task 13-3 — Workouts history view + live nav

## Surface
Frontend only. Light up the **Workouts** destination: a new `WorkoutsView.vue` at `/workouts`
(filterable, paginated history list in the Recent-Activity visual language), the router entry, and
flipping the inert "Workouts" sidebar item + mobile tab live in `AppSidebar.vue`. Service + store
wiring for the 13-2 filtered/paged endpoint. Vitest spec.

## Why
The nav item has read "Workouts · soon" since the redesign; Phase 11 seeded 9+ completed workouts
with nowhere to browse them. This is the landing surface 13-4's detail view links into.

## Depends on
- **Task 13-2** — `GET /workouts?from&to&sport&skip&take` (filtered, paged, newest-first).
- **Task 13-1** — not required for the list, but the detail link target (`/workouts/:id`) lands in 13-4.

## Required reading
- `ui/src/components/dashboard/RecentActivityCard.vue` — **the row visual language to match**
  (sport icon tile, mono stat line: duration / distance / HR / `… TSS` / RPE, right-aligned date,
  `formatDay`/`formatDuration`/`formatDistance`, `sportIcons`).
- `ui/src/components/common/TypePill.vue` + `pills.ts` (`sportToPillKind`) — sport pill the ROADMAP
  asks the rows to carry; `ui/src/components/common/DeltaChip.vue` (vs-planned delta where linked).
- `ui/src/components/layout/AppSidebar.vue` — `trainItems` (flip the `{ icon: Activity, label:
  'Workouts' }` entry to a navigable one), `mobileItems` filter, `isActive`/`itemClass`.
- `ui/src/components/layout/AppShell.vue` + `ui/src/views/ZonesView.vue` — page shell + `title`/
  `subtitle` usage for a list view.
- `ui/src/services/training.ts`, `ui/src/stores/training.ts`, `ui/src/types/training.ts` — where the
  workouts service/store/types live; extend, don't fork.
- `ui/src/router/index.ts` — route registration (lazy-loaded route components).
- `ui/src/views/__tests__/TrainingView.spec.ts` + `ui/src/components/layout/__tests__/AppSidebar.spec.ts`
  — Vitest conventions (assert **visible text**, never CSS; `createTestingPinia`, `RouterLinkStub`).

## Acceptance criteria
- **Service** (`ui/src/services/training.ts`): add
  `getWorkouts(params: { from?: string; to?: string; sport?: PlannedSport; skip?: number; take?: number })`
  building the query string (omit absent params) and returning `WorkoutResponse[]` (reuse the
  `apiFetch<WorkoutResponse[]>(...) ?? []` pattern from `getRecentWorkouts`).
- **Store** (`ui/src/stores/training.ts`): a `workouts` list slice with the active filters, a
  `loadingWorkouts` flag, and actions to (a) load the first page for a filter set and (b) append the
  next page ("load more"). Track whether the last page was short (no more to fetch). Keep the existing
  `recentWorkouts` slice untouched (dashboard still uses it).
- **View** (`ui/src/views/WorkoutsView.vue`, in `AppShell`, `title="Workouts"`):
  - **Filter bar**: sport selector (All + Swim/Bike/Run/Triathlon/Strength) and a from/to date range
    (native `type="date"` inputs, consistent with the plan form). Changing a filter reloads page 1.
    A "Clear" affordance resets to no filters.
  - **Rows**: one per workout in the Recent-Activity style — `TypePill` (`sportToPillKind(w.sport)`)
    or sport-icon tile, sport/title line, the mono stat line (duration, distance, avg HR, `EffectiveLoad`
    TSS, RPE — each shown only when present), right-aligned `completedDate`. Each row links to
    `/workouts/{id}`.
  - **vs-planned**: when `w.plannedWorkoutId != null`, the row may show a `DeltaChip` comparing
    `effectiveLoad` to planned where that signal is available without extra fetches; if planned load
    isn't on the list payload, omit the chip rather than N+1 per row (the detail view, 13-4, owns the
    full comparison). Keep it honest — don't invent a delta.
  - **Pagination**: a "Load more" button that advances `skip` by the page size; hidden once a short
    page returns. Empty state: "No workouts match these filters." Loading state: "Loading…".
- **Nav** (`AppSidebar.vue`): the Workouts item becomes
  `{ icon: Activity, label: 'Workouts', to: '/workouts', routeName: 'workouts' }` — it renders as a
  `RouterLink`, drops the "soon" badge, joins `mobileItems`, and highlights via `isActive` on
  `/workouts`. Update `AppSidebar.spec.ts` in the same commit if it asserts the inert/"soon" state.
- **Router** (`ui/src/router/index.ts`): `{ path: '/workouts', name: 'workouts', component: () =>
  import('@/views/WorkoutsView.vue') }`.
- **Vitest** (`ui/src/views/__tests__/WorkoutsView.spec.ts`): with a seeded store, the list renders a
  row per workout (assert visible sport/stat text); changing the sport filter triggers the reload
  action; "Load more" triggers the append action; empty state renders its message. Assert text, not classes.
- `pnpm run build` (vue-tsc) green; `pnpm test` green (re-run once if the transient worker crash
  appears with all tests passing).

## What NOT to modify
- Don't touch the dashboard `RecentActivityCard` or its `recentWorkouts` store slice (read-only
  reference for styling).
- Don't build the detail page or edit/delete here (13-4).
- Don't add a charting/aggregate surface, calendar, or plan filter (Phases 14–16, 18).
- Don't change user-facing strings elsewhere; keep new copy stable for the spec.

## Suggested commit
```
feat(ui): workouts history view with filters, paging, live nav

WorkoutsView at /workouts lists completed workouts in the Recent Activity
row style with a sport + date-range filter bar and load-more paging against
the 13-2 endpoint. Flips the Workouts sidebar item and mobile tab live.
```
