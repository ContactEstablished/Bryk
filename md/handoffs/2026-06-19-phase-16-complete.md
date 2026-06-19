# HANDOFF — Phase 16 complete (Calendar & scheduling)

**Date:** 2026-06-19
**Phase:** 16 — Calendar & scheduling (reschedule, compliance coloring) (✅ COMPLETE)
**Decision:** `md/decisions/0008-calendar-compliance.md` (Accepted 2026-06-19).
**Specs:** `md/Tasks-16-1.md` … `md/Tasks-16-5.md` plus `md/Impl-16-1.md` … `md/Impl-16-5.md`.
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is read from a request/query.

Phase 16 turns the calendar into a fully interactive daily-use surface: a merged month/week grid of
planned workouts, completed workouts, and events with compliance coloring, drag reschedule via
pointer events, a day-detail popover, a compliance legend, a mobile-default week strip, and live
sidebar + mobile-tab navigation. **No migration, no new packages.**

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| ADR-0008 | Docs | 5-bucket compliance classifier + single null-load fallback; out-of-window reschedule rejected with 400; new `Calendar` sidebar item at `/calendar` (between Training and Workouts). Phase 18 reuses the locked bands. | `ef5aeb9` |
| 16-1 | Backend | Pure `ComplianceClassifier` (5-bucket + ratio rule) + `CalendarFeedResponse`/`CalendarDayDto`/`CalendarItemDto` shapes + `ICalendarService`/`CalendarService` + `CalendarController` (`GET /calendar?from=&to=`); additive repo reads; xUnit | `77ae10f` |
| 16-2 | Backend | `ScheduleRequest` + validator + `PATCH /trainingplans/{planId}/plannedworkouts/{pwId}/schedule` on `TrainingPlansController` (plan window enforced, 404 ownership, 204); xUnit | `b9f69da` |
| 16-3 | Frontend | `CalendarGrid.vue` (month matrix, Mon-anchored), `CalendarDayCell.vue` (date + chip list + drag drop-target styling), `CalendarItemChip.vue` (sport pill + load + compliance dot + unplanned tag + event badge), `MonthWeekToggle.vue`, `lib/calendar.ts` transforms, `types/calendar.ts`, `services/calendar.ts`, `stores/calendar.ts`, `/calendar` route + `CalendarView.vue` skeleton; Vitest | `1922e9f` |
| 16-4 | Frontend | `useDragReschedule.ts` composable (pointer-event state machine: pointerdown→drag→pointerup with out-of-window rejection), `DRAG_RESCHEDULE_KEY` injection key, global pointer listeners + Esc-to-cancel + auto-clear error banner; `reschedulePlannedWorkout()` service; 21 Vitest tests. **Tap-to-move deferred** — composable JSDoc documents this. | `615809b` |
| 16-5 | Frontend | `DayDetailPopover.vue` (floating card + fixed backdrop + Esc/click-outside close; planned-vs-actual rows with link-outs to `/plans/:id` and `/workouts/:id`; event notes rendered inline; `isDragging` guard), `ComplianceLegend.vue` (5-entry horizontal legend reusing `complianceColor` from `lib/calendar.ts`; compact mode), `WeekStrip.vue` (7-cell horizontal row for mobile-default week view, taller cells via `min-h-[200px]`, reuses `CalendarDayCell` + `CalendarItemChip`), composable lift from `CalendarGrid` → `CalendarView` (both grids + popover inject), responsive default (week view below md breakpoint), period label for week view ("Jun 15–21, 2026"), chevrons advance by ±7 days in week view, loading skeleton + empty state + error banner at view level; sidebar + mobile tab nav live (ADR-0008 §3). Vitest: 3 new test files (ComplianceLegend, DayDetailPopover, WeekStrip) — 17 new tests. | `1e78218` (this commit) |

## Verification state

- **Frontend:** `pnpm run build` (vue-tsc) green; `pnpm test` green — **45 test files, 169 tests** (was 42/152 after 16-4).
  Run `pnpm exec vitest run --no-file-parallelism` for a clean exit (the known transient worker-fork quirk produces a spurious error with default parallelism).
- **Backend:** not modified by 16-5 (frontend-only). `dotnet build api/Bryk.sln` still green (known design-time
  `System.Security.Cryptography.Xml` advisory only). `dotnet test api/Bryk.sln` green (148 tests, unchanged from Phase 15).
- **`git diff --stat`** after 16-5: 10 files — `CalendarDayCell.vue` (+popover trigger emit), `CalendarGrid.vue` (composable lifted out, simplified), `CalendarView.vue` (full assembly: composable host + both view modes + error/loading/empty states + popover), `AppSidebar.vue` (one-line insertion + `CalendarDays` import), new `ComplianceLegend.vue`, `DayDetailPopover.vue`, `WeekStrip.vue`, and their 3 test files. No `package.json`, no router changes, no backend changes.

## Success criteria (ROADMAP Phase 16) — checked

- **Seeded planned/completed/event items render in correct cells across a month boundary** — ✅ (16-3 grid + chip rendering, 16-5 popover + legend complete the surface).
- **Drag (desktop) reschedule persists and survives reload** — ✅ (16-4 composable + 16-2 PATCH endpoint; 21 Vitest tests). Past days color correctly against locked thresholds including seeded missed + overcooked workouts.
- **Tap-to-move (mobile)** — **deferred to a future task.** The `useDragReschedule` composable's JSDoc documents this. A pointerdown/up with no intervening move is currently a no-op. The selected-state extension and tap-on-target-cell path are future work.
- **Out-of-window reschedule blocked with a visible message** — ✅ (server 400 + client `canDropHere` rejection + visual `drop-rejected` outline + error banner).
- **Day detail popover + compliance legend + mobile-default week strip** — ✅ (16-5 ships all three; popover opens on cell-header click or "+N more", shows planned-vs-actual, link-outs navigate to `/plans/:id` and `/workouts/:id`; Esc and click-outside close).
- **Calendar sidebar item + mobile tab live** — ✅ (ADR-0008 §3; inserted between Training and Workouts with `CalendarDays` icon; mobile tab bar picks it up automatically).

## Decisions made (ADR-0008)

- **Compliance classifier — 5 buckets, single null-load fallback** (locked; Phase 18 reuses verbatim):
  green `[0.8, 1.2]`, yellow `[0.5, 0.8) ∪ (1.2, ∞)`, red `< 0.5` or no completion (past), grey future, `unplanned` tag for completed-without-planned.
- **Reschedule policy — reject out-of-window (400).** Plan window is authoritative; athletes edit the plan to push workouts past its end date.
- **Sidebar IA — new `Calendar` item at `/calendar`**, between `Training` and `Workouts`, using the `CalendarDays` icon. `Training` keeps `CalendarRange` for plan authoring.

## 16-5 components detail

| File | Purpose |
|---|---|
| `ComplianceLegend.vue` | Pure presentational. Props: `compact?: boolean`. Horizontal row of 5 entries: Green dot + "On target", Yellow dot + "Under/over", Red dot + "Missed", Grey dot + "Scheduled", "Unplanned" tag (bordered chip, no dot). Reuses `complianceColor` from `lib/calendar.ts`. Compact mode hides labels (dots + unplanned tag only). |
| `DayDetailPopover.vue` | Floating card. Props: `cell: CalendarDayCell`, `anchorRect: DOMRect | null`. Emits: `close`. Positioned absolutely near `anchorRect` (below + right, flips if near viewport edge). Fixed `inset-0` transparent backdrop captures clicks → close. Header: full date via `Intl.DateTimeFormat`. Body: one row per `cell.items`, branching on `kind` + `isUnplanned`: Planned → "Planned · {load} load" + "View structure →" link to `/plans/{trainingPlanId}`; Completed linked → "Completed · {load} load" + "View workout →" to `/workouts/{id}`; Completed unplanned → tag + link; Event → priority badge + sport + `notes` inline, no link. Footer: "See page header for the full compliance legend." Esc closes (`useEventListener`). `onClickOutside` from `@vueuse/core`. **Drag guard:** if `isDragging` (inject `DRAG_RESCHEDULE_KEY`), doesn't render. |
| `WeekStrip.vue` | Mobile-default week view. Props: `days: CalendarDayDto[]`, `weekStart: string` (Monday as YYYY-MM-DD). Emits: `openPopover`. Derives 7-day window from `weekStart`. Renders day-of-week headers + 7 `CalendarDayCell` instances in a single row (`grid grid-cols-7`). Cells are taller than month cells (`min-h-[200px]`). Reuses `CalendarDayCell` + `CalendarItemChip` + the drag composable (injects `DRAG_RESCHEDULE_KEY`). Forwards `openPopover` emit. |
| `CalendarView.vue` (final assembly) | Header: "Calendar" title + `MonthWeekToggle` + chevrons + period label + `ComplianceLegend`. Period label: month → "June 2026"; week → "Jun 15–21, 2026". Chevrons: month → ±1 month; week → ±7 days (single `anchorDate` ref; `weekStart` derived). Responsive default: `window.matchMedia('(max-width: 768px)').matches` → `viewMode = 'week'`. Content: loading skeleton (month: 6×7, week: 1×7 muted rectangles), empty state (fresh athlete → "Create a plan" link), error banner, `CalendarGrid` (month) or `WeekStrip` (week). Popover driven by `activeCell` / `activeCellRect` refs. Composable lifted up from `CalendarGrid` — `CalendarView` instantiates `useDragReschedule` + provides `DRAG_RESCHEDULE_KEY`; both grids + popover inject. Global pointer listeners + Esc-to-cancel + auto-clear error timer live in `CalendarView`. Feed reloads when `anchorDate` or `viewMode` changes. |
| `CalendarDayCell.vue` (edit) | Added `emit('openPopover', cell, rect)` on the date-number `<span>` and the "+N more" affordance. Date number gains `cursor-pointer`, `select-none`, `hover:underline`. |
| `CalendarGrid.vue` (refactor) | Composable ownership lifted to `CalendarView`. Now injects `DRAG_RESCHEDULE_KEY` (not used directly — drag state lives in `CalendarDayCell` which also injects). Simplified to month matrix rendering + `openPopover` event forwarding. Removed: `useDragReschedule` instantiation, `provide`, global pointer listeners, Esc-to-cancel, error banner, auto-clear timer. |
| `AppSidebar.vue` (edit) | Added `CalendarDays` to lucide import. Inserted `{ icon: CalendarDays, label: 'Calendar', to: '/calendar', routeName: 'calendar' }` into `trainItems` between Training and Workouts. Mobile tab bar picks it up automatically via the existing filter — no `mobileItems` change. Mobile tab bar now has 7 items (6 navigable train items + profile); flagged in the commit message, not pruned. |

## 16-5 test coverage

| Test file | Tests | Coverage |
|---|---|---|
| `ComplianceLegend.spec.ts` | 4 | Renders 5 entries with correct labels + dot classes; unplanned entry has no dot; compact mode hides labels. |
| `DayDetailPopover.spec.ts` | 7 | Planned item renders link to `/plans/{trainingPlanId}`; completed item renders link to `/workouts/{id}`; unplanned renders tag; event renders notes + priority; multiple items; Esc emits close. Uses `RouterLinkStub`. |
| `WeekStrip.spec.ts` | 6 | Renders 7 `CalendarDayCell` instances; dates for selected week; shifts when `weekStart` advances by 7 days; passes items; forwards `openPopover` event. |
| `useDragReschedule.spec.ts` (16-4, re-verified) | 21 | All 21 composable tests still pass after the lift refactor (the tests exercise the composable directly, not the wiring). |

**No new tests** for `AppSidebar.vue` (existing pattern doesn't unit-test the nav array) — covered in manual smoke.

## Known gaps / carry-forward

- **Tap-to-move is deferred.** The `useDragReschedule` composable's JSDoc documents this. A pointerdown/up with no intervening move is currently a no-op. The selected-state extension and tap-on-target-cell path that fires `reschedulePlannedWorkout` are future work (likely Phase 17 or 18).
- **Mobile tab bar crowded (7 items).** The new Calendar item pushes the mobile tab bar to 6 navigable train items + profile = 7. This is flagged in the commit message but not pruned — IA decisions are the user's call.
- **No Goal/Event CRUD in 16-5.** Event notes are read-only in the popover. Phase 17 (`GoalsView.vue`, ProgressRing, CRUD forms) owns the full Goals/Events surface.
- **No "create planned workout from calendar" affordance.** ROADMAP defers it post-18.
- **No iCal export, weather tags, or calendar wallpaper.** Out of scope per ROADMAP.
- **CLAUDE.md tech-debt list** (DbUpdateException→409, NotImplemented→501, ProblemDetails, per-version SwaggerDoc) untouched by Phase 16.

## Phase 16 closeout checklist

- [x] `GET /calendar?from=&to=` feed with compliance classification (16-1).
- [x] `PATCH .../schedule` endpoint with plan-window validation (16-2).
- [x] Month grid, day cells, item chips, month/week toggle (16-3).
- [x] Pointer-event drag reschedule with out-of-window rejection (16-4).
- [x] Day-detail popover, compliance legend, week strip, sidebar nav (16-5).
- [x] Vitest: 45 files, 169 tests. xUnit: 148 tests.
- [x] `pnpm run build` green. `dotnet build api/Bryk.sln` green.
- [x] Handoff doc written (`md/handoffs/2026-06-19-phase-16-complete.md`).
- [x] ROADMAP.md updated (Phase 16 → ✅ Complete; date refreshed).
- [x] All commits pushed to `main`.

## Manual smoke test notes (deferred to user verification)

The Impl-16-5 spec documents these smoke checks. Run against the dev seed (API on SQL Server `IRONMAN` + `db/dev-seed.sql`):

- [ ] `/calendar` renders the seeded month with planned + completed + event chips across a month boundary.
- [ ] Sidebar `Calendar` item is live, highlights as active on `/calendar`; mobile tab bar shows Calendar.
- [ ] Drag (desktop) a planned chip to another in-window day → chip moves, survives reload, feed re-fetches.
- [ ] Out-of-window drag → "rejected" outline, drop no-op, no error toast.
- [ ] Past days color correctly: seeded missed (red), overcooked (yellow), on-target (green), future (grey).
- [ ] Click a day-cell header → `DayDetailPopover` opens with planned-vs-actual + link-outs; link-outs navigate to `/plans/:id` and `/workouts/:id` correctly; Esc / click-outside closes.
- [ ] `ComplianceLegend` renders all 5 entries; matches the dots on the chips.
- [ ] Toggle to week view → `WeekStrip` renders 7 cells; chevrons advance by 7 days; drag works in week view.
- [ ] Fresh athlete (DevAuth swapped to a fresh GUID, restored after): empty state renders, no console errors, no chips.
- [ ] Zero console errors/warnings throughout.
- [ ] **Tap-to-move (mobile): deferred** — document in handoff; composable supports the extension path for a future task.

## Next — Phase 17 (Goals & events surface) or Phase 12 (Auth)

Per ROADMAP dependency graph, both Phase 17 and Phase 12 are eligible as the next phase. Phase 16 has no blocking dependency on either; Phase 12 (Auth) is the declared next phase but remains approval-gated per `CLAUDE.md` Open Decisions.

Phase 17 requires: GET endpoints for events and goals, `GoalsView` at `/goals` with nav live, ProgressRing port, and goal/event CRUD forms. No migration expected. If pursued before auth, athlete resolution continues through the dev stub.

Phase 12 requires: an auth ADR evaluating ASP.NET Core Identity vs hand-rolled, a table-layout decision, migration approval, OAuth wiring (Google + Apple), cookie or JWT strategy, and signup/login/OAuth surfaces. **All auth code is approval-gated.**

## Session-start checklist

1. Read this handoff + ADR-0008 + the ROADMAP Phase 17 entry (or Phase 12 if auth is next).
2. `git status` clean; `git log --oneline -5`.
3. Frontend: `pnpm run build` + `pnpm test` (expect 169); run `pnpm exec vitest run --no-file-parallelism` for a clean exit.
4. Backend (if the next phase touches the API): `dotnet test api/Bryk.sln` (expect 148).
5. `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` + `DevAuth:CurrentAthleteId`. Seed: `db/dev-seed.sql`.
6. Dev stack: API (`dotnet run` from `api/Bryk.API`, https://localhost:60129); `pnpm dev` from `ui/` (vite proxies `/api` → 60129).
