# Impl 16-5 — Build order: popover, legend, week strip, nav live, assembly

**Executor:** GLM 5.2. **Acceptance contract:** `md/Tasks-16-5.md`. **Decision lock:** ADR-0008 §3.
**Scope:** Frontend only. No new package. Closes Phase 16.

## Step 0 — Pre-flight

- `git status` clean (16-1/2/3/4 committed). `pnpm run build` + `pnpm test` green.
- Re-read `md/Tasks-16-5.md` + ADR-0008 §3. Open: `ui/src/components/layout/AppSidebar.vue`
  (the `trainItems` array + `mobileItems` filter), `ui/src/views/ProgressView.vue` (page-assembly
  pattern), all `ui/src/components/calendar/*` (from 16-3/16-4), `ui/src/router/index.ts` (route
  exists from 16-3), `ui/src/services/training.ts` (for plan/workout link routes — verify
  `/plans/:id` and `/workouts/:id` exist; they do).
- Check `package.json` for `@vueuse/core` — if present, use `onClickOutside`; if not, hand-roll a
  document click listener (don't add the package without approval).

## Step 1 — `ComplianceLegend.vue`

**New file** `ui/src/components/calendar/ComplianceLegend.vue`.

Pure presentational. Props: `compact?: boolean`. Renders a horizontal row (flex):
- Green dot + "On target"
- Yellow dot + "Under/over"
- Red dot + "Missed"
- Grey dot + "Scheduled"
- "Unplanned" tag (no dot — uses a small bordered chip matching the unplanned chip styling)

Dot classes from `lib/calendar.ts`'s `complianceColor`. Labels: `text-xs text-faint`. Wrap in a
`<div class="flex items-center gap-4">`. Compact mode: drop labels to icons-only (or just dots) on
narrow viewports — pin one behavior.

## Step 2 — `DayDetailPopover.vue`

**New file** `ui/src/components/calendar/DayDetailPopover.vue`.

Props:
- `cell: CalendarDayCell` (the day whose items to show).
- `anchorRect: DOMRect` (the cell's rect, for positioning).

Emits: `close`.

Behavior:
- Positioned absolutely near `anchorRect` (below + right of the cell, or flipped if near viewport
  edge — keep it simple: `position: absolute; top: anchorRect.bottom + 4px; left: anchorRect.left`).
- Backdrop: a fixed `inset-0` transparent div that captures clicks → `emit('close')`.
- Header: full date ("Thursday, June 18" via `Intl.DateTimeFormat`).
- Body: one row per `cell.items`:
  - Planned: sport pill + title + "Planned · {load} load · {duration or '—'}" + "View structure →"
    link. Link target: `/plans/${item.trainingPlanId}` (the plan browser; the pw selection happens
    there — verify `PlanDetailView` accepts a `?pwId=` query or just lands on the plan). If
    `item.workoutId` is set (matched completion), show a small "✓ completed" checkmark.
  - Completed (linked): sport pill + title + "Completed · {load} load · {duration}" + "View workout →"
    link to `/workouts/${item.id}`.
  - Completed (unplanned): sport pill + title + "Completed · unplanned · {load} load" + "View workout →".
  - Event: priority badge (A/B/C) + name + "{sport or '—'}" + `item.notes` (finally rendered) — no link.
- Footer: a tiny inline legend or "View legend" link (optional — the page legend is right there).
- Esc closes (window keydown listener while mounted).
- `onClickOutside` (if `@vueuse/core` present) or hand-rolled: click outside the popover card (not the
  backdrop) closes.

**Drag interaction guard:** if `isDragging` (inject from the grid's composable), don't render the
popover (the popover is opened by a cell-header click, not a chip click, so this is a belt-and-braces
guard).

## Step 3 — `WeekStrip.vue`

**New file** `ui/src/components/calendar/WeekStrip.vue`.

Props: `days: CalendarDayDto[]`, `weekStart: string` (YYYY-MM-DD, the Monday of the selected week),
`planWindows` (passed through for the drag composable).

- Renders a single horizontal row of 7 `CalendarDayCell`s (Mon–Sun) for `[weekStart, weekStart+6]`.
- Cells are taller and narrower than month cells (`min-h-[200px]` or similar), stacking 3-4 chips
  before the "+N more" affordance.
- Reuses `CalendarDayCell` + `CalendarItemChip` + the drag composable (the grid provides it; WeekStrip
  injects the same instance — so `CalendarView` hosts the composable, and both `CalendarGrid` and
  `WeekStrip` inject it).
- The cell-header click (popover trigger) works the same as in month view.

## Step 4 — `CalendarView.vue` final assembly

- Header: "Calendar" h1 + chevrons + period label + `MonthWeekToggle` + `ComplianceLegend`.
- Period label: month view → "June 2026"; week view → "Jun 15–21, 2026" (format the week range).
- Chevrons: month view → ±1 month; week view → ±7 days. Update `anchorDate` (a single `Date` ref
  representing the anchor; derive `anchorMonth` or `weekStart` from it).
- Responsive default: `onMounted`, if `window.matchMedia('(max-width: 768px)').matches`, set
  `view = 'week'`. (`view: Ref<'month' | 'week'>`, default `'month'`.)
- Content area:
  - Loading skeleton: 6×7 (month) or 1×7 (week) muted rectangles.
  - Empty state (fresh athlete): "No training scheduled yet. Create a plan to get started." with a
    `<RouterLink to="/plans">` — only when `feed.days.every(d => d.items.length === 0)`.
  - Error state: banner with `error.message`.
  - `CalendarGrid` (month) or `WeekStrip` (week), sharing the composable instance.
- `DayDetailPopover` rendered at the view level (one instance), driven by
  `activeCell: Ref<CalendarDayCell | null>` + `activeCellRect: Ref<DOMRect | null>`. Cell-header click
  sets both; popover `close` clears them.
- Composable: `CalendarView` instantiates `useDragReschedule` (with `planWindows` loaded from
  `getPlans()` in `onMounted`) and `provide`s it. Both grids + the popover inject.
- Feed reload: when `anchorDate` or `view` changes, recompute the feed window and call
  `calendarStore.loadFeed(from, to)`. For month view: `from = isoDate(new Date(year, month-1, 1))`,
  `to = isoDate(new Date(year, month, 0))`. For week view: `from = weekStart`, `to = weekStart + 6 days`.
  **Cap check:** month spans ≤ 31 days (well under 62); week spans 7 days — both fine.

## Step 5 — Sidebar nav (`AppSidebar.vue`)

**Edit** `ui/src/components/layout/AppSidebar.vue`:

In the `trainItems` array, insert between `Training` and `Workouts`:
```ts
{ icon: CalendarDays, label: 'Calendar', to: '/calendar', routeName: 'calendar' },
```
Add `CalendarDays` to the `lucide-vue-next` import (the existing `CalendarRange` stays on `Training`).

**Do not** touch `mobileItems` (the existing filter picks up the new item automatically). **Do not**
prune any items — if the mobile tab bar gets crowded (6 items + profile = 7), flag it for the user in
the commit message but don't decide IA unilaterally.

## Step 6 — Tests

- `ComplianceLegend.spec.ts`: renders 5 entries; dot classes match `complianceColor`. Smoke.
- `DayDetailPopover.spec.ts`: given a cell with a planned+completed pair, renders both rows with the
  correct link hrefs (`/plans/{trainingPlanId}`, `/workouts/{id}`); given an event, renders
  `notes`; given an unplanned completion, renders the "unplanned" tag. Use `@vue/test-utils` mount.
- `WeekStrip.spec.ts`: renders 7 cells for the selected week; `props.weekStart` advancing by 7 days
  shifts the rendered dates.
- No new `AppSidebar` tests (the existing pattern doesn't unit-test the nav array; a smoke check that
  the Calendar item renders is enough — cover in the manual smoke).

**Verify:** `pnpm run build` + `pnpm test` green.

## Step 7 — Manual smoke (run against the dev seed)

- `/calendar` renders the seeded month with planned + completed + event chips in the right cells
  across a month boundary (navigate to a month spanning June/July).
- Sidebar `Calendar` item is live, highlights as active on `/calendar`; mobile tab bar shows Calendar.
- Drag (desktop) a planned chip to another in-window day → chip moves, survives reload, feed re-fetches.
- Tap-to-move (mobile): **if you shipped it** (the composable supports it via the selected-state
  extension — if you deferred it to a future task, document that here and skip this bullet).
- Out-of-window drag → "rejected" outline, drop no-op, no error toast.
- Past days color correctly: seeded missed (red), overcooked (yellow), on-target (green), future (grey).
- Click a day-cell header → `DayDetailPopover` opens with planned-vs-actual + link-outs; link-outs
  navigate to `/plans/:id` and `/workouts/:id` correctly; Esc / click-outside closes.
- `ComplianceLegend` renders all 5 entries; matches the dots on the chips.
- Toggle to week view → `WeekStrip` renders 7 cells; chevrons advance by 7 days; drag works in week view.
- Fresh athlete (DevAuth swapped to a fresh GUID, restored after): empty state renders, no console
  errors, no chips.
- Zero console errors/warnings throughout.

## Step 8 — Phase 16 closeout

- `pnpm run build` + `pnpm test` green.
- `git diff --stat` — only the new components (`DayDetailPopover`, `ComplianceLegend`, `WeekStrip`),
  edits to `CalendarView.vue` (assembly), `AppSidebar.vue` (one item), the new test files. No
  package.json, no router changes (route from 16-3), no backend changes.
- Commit with the message in `Tasks-16-5.md`.
- **Handoff doc:** write `md/handoffs/YYYY-MM-DD-phase-16-complete.md` mirroring the Phase-15 handoff
  structure (what shipped, verification state, decisions made, known gaps, next phase pointer). Commit
  as `docs: close out Phase 16 — ledger, ADR-0008 referenced, handoff`.
- **Update `ROADMAP.md`:** flip Phase 16 row to ✅ Complete; update the "Status as of" date.
  Commit as `docs: roadmap — mark Phase 16 complete`.
- Phase 16 done. Next per ROADMAP: Phase 17 (Goals & events surface) or Phase 12 (Auth, approval-gated)
  — surface the choice to the user; don't assume.
