# Task 16-5 — Day detail popover, compliance legend, week strip, nav live, assembly

## Surface
Frontend only. `DayDetailPopover.vue` (the day detail popover); `ComplianceLegend.vue`;
`WeekStrip.vue` (the mobile-default week view, replacing 16-3's placeholder); sidebar + mobile-tab
nav wiring in `AppSidebar.vue`; the `/calendar` route's final assembly; final manual smoke test
against the dev seed. **No new package.**

## Why
Closes Phase 16: the popover is the planned-vs-actual + link-out surface (a user clicks a day to see
what's there and navigate to detail); the legend makes the compliance dots legible; the week strip is
the mobile primary view (a month grid is unreadable on a phone); the sidebar entry is the
discoverability gate. After 16-5, the Calendar nav item is live and the daily-use loop is complete.

## Depends on
- **Task 16-1** — the feed (popover content).
- **Task 16-3** — `CalendarView`, `CalendarGrid`, `CalendarDayCell`, `CalendarItemChip`,
  `MonthWeekToggle`, `lib/calendar.ts` transforms (the week strip reuses `groupItemsByDay` and the
  chip/cell primitives).
- **Task 16-4** — `useDragReschedule` (the popover coexists with drag; clicking a chip opens the
  popover, dragging it doesn't — the composable distinguishes a tap from a drag).
- **ADR-0008** §3 (new `Calendar` sidebar item at `/calendar`).

## Required reading
- `ui/src/components/layout/AppSidebar.vue` — the `trainItems` array (16-5 adds the Calendar entry
  between `Training` and `Workouts`); the mobile-tab-bar filter picks it up automatically.
- `ui/src/views/ProgressView.vue` — the page-assembly pattern (sections, eyebrow headers, card surfaces).
- `ui/src/components/calendar/*` (from 16-3, 16-4) — the primitives this task assembles.
- `ui/src/router/index.ts` — confirm the `/calendar` route from 16-3; no change needed here unless
  the route metadata wants a `title`.
- The existing `WorkoutsView` / `WorkoutDetailView` for the chip → detail link-out pattern (the
  popover's "View workout →" / "Edit structure →" links reuse the `/workouts/:id` and
  `/plans/:id?pwId=...` routes — verify the latter exists; if not, link to `/plans/:id` and let the
  plan browser handle the pw selection).

## Acceptance criteria

### `DayDetailPopover.vue`
- Triggered by clicking a day cell's header (the date number) or an "+N more" affordance — not by
  clicking a chip (chips are drag handles in 16-4; clicking a chip with no drag is a tap, which the
  composable treats as a no-op drag — so wire the popover open to a separate click target on the cell,
  e.g. the date number or a dedicated "···" button).
- Renders as a floating card (absolute-positioned near the cell, with a backdrop on mobile):
  - Header: "Thursday, June 18" (full date).
  - Body: one row per `CalendarItemDto` in that day, mirroring the chip layout (sport pill, title,
    load, compliance dot) plus a secondary line:
    - Planned: "Planned · {load} load · {duration or '—'}" + a "View structure →" link to the plan
      browser (carries `planId` + `pwId` if the route supports it, else `/plans/{planId}`).
    - Completed (linked): "Completed · {load} load · {duration}" + a "View workout →" link to
      `/workouts/{id}`. If the planned pair is in the same day, show both rows with a visual connector.
    - Completed (unplanned): "Completed · unplanned · {load} load" + "View workout →".
    - Event: "{Priority} priority · {sport or '—'}" + `Event.Notes` (finally rendered — Phase 17 will
      build the full Goals/Events surface; here we just show the notes inline).
  - Footer: a tiny compliance legend (or "View legend" link to the page-level legend).
- Close: click backdrop, Esc, or click-outside (use `@vueuse/core`'s `onClickOutside` if it's already
  a dependency — check `package.json`; if not, hand-roll a `document` click listener).
- Doesn't interfere with drag: opening the popover pauses drag on that cell (or the popover simply
  doesn't open during an active drag — `isDragging` guard).

### `ComplianceLegend.vue`
- A small horizontal row of dot + label pairs: "On target" (green), "Under/over" (yellow), "Missed"
  (red), "Scheduled" (gray), plus an "Unplanned" tag (no dot — uses the `unplanned` chip styling).
- Pinned to the page header area (right side, beside the `MonthWeekToggle`) or as a slim bar under
  the header. Match the Progress page's legend placement style.
- Pure presentational; no props beyond optional `compact?: boolean` for mobile.

### `WeekStrip.vue` (mobile-default week view)
- Replaces the 16-3 placeholder when the `MonthWeekToggle` is on "week".
- A single horizontal row of 7 `CalendarDayCell`s (Mon–Sun) for the selected week, taller and
  narrower than the month cells, stacking 3-4 chips before the "+N more" affordance.
- Week navigation: the chevron arrows move by 7 days (vs by month in month-view). The period label
  shows "Jun 15–21, 2026".
- On mobile (`md:hidden`), this is the default view; the month grid is `hidden md:grid` (or the
  toggle defaults to "week" below the `md` breakpoint). Pin the responsive default in `CalendarView`'s
  mounted logic: `if (window.matchMedia('(max-width: 768px)').matches) view = 'week'`.
- Reuses `CalendarDayCell` + `CalendarItemChip` + the drag composable (drag works in week view too —
  same grid, fewer cells).

### `AppSidebar.vue` wiring (ADR-0008 §3)
- In `trainItems`, insert between `Training` and `Workouts`:
  `{ icon: CalendarDays, label: 'Calendar', to: '/calendar', routeName: 'calendar' }`.
- Import `CalendarDays` from `lucide-vue-next` (the existing `CalendarRange` stays on `Training`).
- The mobile tab bar picks it up automatically via the existing `mobileItems` filter — **do not**
  touch the mobile-tab logic beyond verifying the new item renders. If the tab bar gets crowded
  (6 items + profile), flag it — but don't prune items in 16-5; that's an IA call for the user.

### Assembly (`CalendarView.vue` final form)
- Header: "Calendar" title + chevrons + period label + `MonthWeekToggle` + `ComplianceLegend`.
- Content: `CalendarGrid` (month) or `WeekStrip` (week), sharing the `useDragReschedule` composable
  instance (provided by `CalendarView`, injected by the grid/strip).
- `DayDetailPopover` rendered at the view level (one instance, positioned via the active cell's rect).
- Empty state (fresh athlete): "No training scheduled yet. Create a plan to get started." with a link
  to `/plans` (the Phase-9/13 plan browser).
- Error state: the feed fetch error as a banner; the reschedule error as a toast (from 16-4).
- Loading state: a skeleton grid (6×7 of muted rectangles) — match `WorkoutsView`'s skeleton style.

### Tests
- `ComplianceLegend.spec.ts`: renders 5 entries with the right labels + dot classes (smoke).
- `DayDetailPopover.spec.ts`: given a day with a planned+completed pair, renders both rows with the
  link hrefs; given an event, renders the notes; given an unplanned completion, renders the tag.
- `WeekStrip.spec.ts`: renders 7 cells for the selected week; chevron advances by 7 days.
- No new tests for `AppSidebar.vue` (the existing pattern doesn't unit-test the nav array; a smoke
  check that the item renders is enough — verify in the manual smoke).
- `pnpm run build` green; `pnpm test` green.

### Manual smoke test (documented in the impl spec, run against the dev seed)
- `/calendar` renders the seeded month with planned + completed + event chips in the right cells
  across a month boundary.
- Drag (desktop) and tap-move (mobile) persist and survive reload (re-verifies 16-4 with 16-5's
  popover present).
- Past days color correctly against the locked thresholds, including a seeded missed (red) and
  overcooked (yellow) workout.
- Out-of-window reschedule blocked with a visible message (re-verifies 16-2/16-4).
- Popover opens on cell-header click, shows planned-vs-actual, link-outs navigate correctly.
- Legend renders all 5 entries; mobile tab bar shows Calendar; sidebar highlights Calendar as active.
- Fresh athlete: empty state renders, no console errors.

## What NOT to modify
- No new npm package.
- Don't change the 16-1 feed shape, the 16-2 endpoint, or the 16-3/16-4 component APIs beyond the
  popover-specific wiring.
- Don't prune the mobile tab bar — flag crowding for the user, don't decide IA unilaterally.
- Don't add Goal/Event CRUD — that's Phase 17. The popover shows event notes read-only.
- Don't implement calendar export (iCal) or weather tags — out of scope per the ROADMAP.
- Don't add a "create planned workout from the calendar" affordance — ROADMAP defers it post-18.
- Don't touch the analytics endpoints or the dashboard — the calendar is additive.

## Suggested commit
```
feat(ui): calendar popover, legend, week strip, nav live (Phase 16 close)

DayDetailPopover shows per-day planned-vs-actual with link-outs to
/workouts/:id and the plan browser; event notes finally render inline
(Phase 17 owns the full Goals/Events surface). ComplianceLegend pins
the 5-bucket dot meanings; WeekStrip is the mobile-default view
(month grid hidden below md). Calendar sidebar item + mobile tab live
(ADR-0008 §3). Phase 16 complete: /calendar renders the seed, drag +
tap-move persist, compliance coloring matches the locked bands.
```
