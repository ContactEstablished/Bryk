# Task 16-3 — Calendar grid rendering (month grid, day cells, item chips, toggle)

## Surface
Frontend only. `CalendarView.vue` at `/calendar` (lazy-loaded); `components/calendar/` building blocks
(`CalendarGrid`, `CalendarDayCell`, `CalendarItemChip`, `MonthWeekToggle`); a `types/calendar.ts`; a
`services/calendar.ts` mirror of the `GET /calendar` feed; a calendar-store slice; Vitest for the pure
transforms (day-grouping, item ordering, month-matrix building). **No drag-and-drop library, no chart
library, no new npm package.**

## Why
The grid is the calendar's primary surface — a deterministic, testable renderer for the 16-1 feed.
Drag/tap interactions (16-4) and the day-detail popover + legend + nav (16-5) layer on top of these
primitives, so they must be stable and well-typed first.

## Depends on
- **Task 16-1** — the `GET /calendar` feed shape (`CalendarFeedResponse`, `CalendarDayDto`,
  `CalendarItemDto`, `ComplianceBucket`, `CalendarItemKind`).
- **ADR-0008** §1 (chip + dot rendering), §3 (new `Calendar` sidebar item — the route + nav wiring is
  16-5, but the view must exist at `/calendar` for it to land).

## Required reading
- `ui/src/services/analytics.ts` + `ui/src/services/training.ts` — the `apiFetch` + typed-service
  pattern to mirror for `services/calendar.ts`.
- `ui/src/stores/analytics.ts` (or whichever Pinia slice the analytics views use) — the store-action
  pattern (load + cached state).
- `ui/src/views/ProgressView.vue` + `ui/src/views/WorkoutsView.vue` — the established view layout
  (page header + content area, loading/empty/error states).
- `ui/src/components/charts/PMCChart.vue` / `LoadChart.vue` — the established pattern for views that
  consume a store + render pure-transform-driven output (the calendar's day-matrix builder is the
  analogue of `buildPmcGeometry`/`buildLoadGeometry`).
- `ui/src/components/layout/AppSidebar.vue` — the `trainItems` array (16-5 adds the Calendar item, but
  the view must be reachable at `/calendar` for that to land).
- `ui/src/router/index.ts` — the lazy-load route pattern.
- `ui/src/types/analytics.ts` — the type-mirror style.
- The design-export reference (the `Bryk UI.zip` `charts.jsx` + calendar sketches) for chip styling,
  if available — match the established Tailwind token palette (`--chart-*`, `text-faint`, `bg-muted`,
  `TypePill` patterns already in `WorkoutsView`).

## Acceptance criteria

### Types (`ui/src/types/calendar.ts`)
- Mirror the 16-1 DTOs verbatim: `ComplianceBucket` (`'Grey' | 'Green' | 'Yellow' | 'Red'`),
  `CalendarItemKind` (`'Planned' | 'Completed' | 'Event'`), `CalendarItemDto`, `CalendarDayDto`,
  `CalendarFeedResponse`. Use the string-enum convention the API serializes (verify against a live
  response during the smoke test — the .NET enums serialize as their string names by default per
  `Program.cs`'s `JsonStringEnumConverter`).

### Service (`ui/src/services/calendar.ts`)
- `getCalendarFeed(from?: string, to?: string): Promise<CalendarFeedResponse>` — mirrors
  `getWorkouts`'s URLSearchParams build (omit absent params; the server applies defaults).
- `reschedulePlannedWorkout(planId: string, plannedWorkoutId: string, scheduledDate: string): Promise<void>`
  — declared here (used by 16-4) so the service module is complete in one place; 16-4 wires the
  interaction to call it. Returns void (204).

### Store (`ui/src/stores/calendar.ts`)
- Pinia slice: `feed: CalendarFeedResponse | null`, `loading: boolean`, `error: string | null`.
- Action `loadFeed(from?: string, to?: string)` — calls `getCalendarFeed`, sets state. Mirror the
  analytics-store pattern (no global error toast — the view renders the error state).
- Getter `daysByMonth` (or a pure transform in `lib/calendar.ts` — see below) — see the transform
  section; prefer a pure function the store calls so it's unit-testable.

### Pure transforms (`ui/src/lib/calendar.ts`)
**These are the Vitest-covered units** (mirroring `lib/charts/pmc.ts` / `load.ts`):
- `buildMonthMatrix(days: CalendarDayDto[], anchorMonth: { year: number; month: number }): CalendarDayCell[][]`
  — a 6×7 (or 5×7, whichever fits the month) Monday-anchored matrix of `CalendarDayCell` objects
  (`{ date: DateOnly; items: CalendarItemDto[]; isInMonth: boolean; isToday: boolean }`). Cells outside
  the anchor month carry `isInMonth: false` and an empty `items` array (the feed only covers the
  requested range; out-of-range cells are blank, not erroring).
- `groupItemsByDay(feed: CalendarFeedResponse): Map<string, CalendarDayDto>` — a string-keyed
  (`YYYY-MM-DD`) lookup for O(1) cell population. (Trivial but pinned by tests so the key format is
  stable.)
- `complianceColor(bucket: ComplianceBucket | null): { dot: string; chip?: string }` — the Tailwind
  class string(s) for each bucket; `null` (events) returns no dot. Pinned so 16-4/16-5 reuse the same
  mapping.
- `sportColor(sport: Sport): string` — reuse the existing `TypePill` / sport-color utility if one
  exists (grep for it); if not, define one here and refactor `TypePill` to use it in a follow-up (out
  of scope for 16-3 — just define it locally and note the tech-debt candidate).

### Components
- `CalendarView.vue` (`/calendar`):
  - Page header: "Calendar" title + a `MonthWeekToggle` (two pill buttons; "Month" default) + chevron
    arrows + the period label ("June 2026" for month, "Jun 15–21, 2026" for week).
  - Content: loading skeleton, empty state (fresh athlete — "No training scheduled yet"), error state,
    or the `CalendarGrid` (month) / `WeekStrip` (week — 16-5 owns `WeekStrip`; 16-3 renders the grid
    only, with the toggle wired but week-view rendering deferred to 16-5 — show a "Week view coming
    next" placeholder for the week toggle in 16-3, replaced by `WeekStrip` in 16-5).
  - Reads `route.query` for `from`/`to` (optional; written via `router.replace` on period navigation,
    matching the ADR-0007 §5 convention).
- `CalendarGrid.vue`:
  - Props: `days: CalendarDayDto[]`, `anchorMonth: { year: number; month: number }`.
  - Renders a 7-column CSS grid; Mon–Sun header row (muted uppercase). Calls `buildMonthMatrix` and
    renders one `CalendarDayCell` per matrix cell.
- `CalendarDayCell.vue`:
  - Props: `cell: CalendarDayCell` (the matrix entry), `today: string` (YYYY-MM-DD).
  - Date number top-left; today's date with a filled accent circle. Out-of-month cells: muted text,
    no items rendered. In-month cells: stacked `CalendarItemChip`s (cap at 3 visible + an "+N more"
    affordance that 16-5's popover will expand; for 16-3 just show the cap and a count).
- `CalendarItemChip.vue`:
  - Props: `item: CalendarItemDto`.
  - Compact horizontal chip: sport-color square (or a small `TypePill`-style tag) + title (truncate)
    + load number + compliance dot (right-aligned). Events render as a distinct dark-slate pill with
    the A/B/C priority badge (use `EventPriority`); no compliance dot. Unplanned completions show a
    small "unplanned" tag. Hover state lifts the chip (subtle shadow) — 16-4's drag uses the same
    hover styling.
- `MonthWeekToggle.vue`:
  - Props: `modelValue: 'month' | 'week'`. Emits `update:modelValue`. Two pill buttons; active state
    filled with `bg-primary-hi`/`text-primary-foreground`.

### Route + nav
- Add `/calendar` to `ui/src/router/index.ts` as a lazy-loaded route, name `calendar`, component
  `() => import('@/views/CalendarView.vue')`.
- **Do NOT** wire the sidebar item in 16-3 — that's 16-5 (it touches `AppSidebar.vue` and the mobile
  tab bar). 16-3 just makes the route reachable by URL so the smoke test works.

### Tests (`ui/src/lib/calendar.spec.ts` or co-located)
- `buildMonthMatrix`: a known month (e.g. June 2026, which starts on a Monday) yields the correct
  6×7 matrix; cells outside June carry `isInMonth: false`; today's cell carries `isToday: true`.
- `groupItemsByDay`: a feed with items on three days produces three map entries with the right keys.
- `complianceColor`: each bucket returns the expected Tailwind classes; `null` returns no dot.
- `sportColor`: bike/run/swim/strength each return a distinct class (smoke-level — exact strings
  pinned so 16-4/16-5 don't drift).
- `pnpm run build` (vue-tsc) green; `pnpm test` green.

## What NOT to modify
- No new npm package (no drag-and-drop library, no chart library, no date library — use native
  `Date` and the existing `lib/format.ts` patterns; if a date utility is missing, add it to
  `lib/calendar.ts` as a pure function).
- Don't wire the sidebar item or mobile tab — that's 16-5.
- Don't implement drag/tap interactions — that's 16-4. The chips are presentational only in 16-3.
- Don't implement the day-detail popover — that's 16-5. The "+N more" affordance is a static label
  for now.
- Don't change `AppSidebar.vue`, `AppRouter`, or any other view — only add the `/calendar` route.
- Don't fetch the feed in the components — always through the store action.
- Don't invent a new color system — reuse the existing Tailwind tokens / `TypePill` patterns.

## Suggested commit
```
feat(ui): calendar grid rendering (month view, chips, toggle)

CalendarView at /calendar renders the 16-1 feed as a Monday-anchored
month grid: CalendarGrid → CalendarDayCell → CalendarItemChip, with
compliance dots (ADR-0008 §1) and event priority pills. Pure transforms
(buildMonthMatrix, groupItemsByDay, complianceColor) in lib/calendar.ts
pinned by Vitest. MonthWeekToggle wired; week-view rendering deferred to
16-5. Route live by URL; sidebar nav lands in 16-5. No new package.
```
