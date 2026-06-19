# Task 16-4 — Reschedule interactions (pointer-event drag desktop, tap-to-move mobile)

## Surface
Frontend only. Pointer-event drag on `CalendarItemChip` (desktop) and tap-select/tap-target on day
cells (mobile + a11y fallback) calling the 16-2 `reschedulePlannedWorkout` service; a
`useDragReschedule` composable in `src/composables/`; Vitest for the composable's state machine;
Cypress/Playwright **not** required (the smoke test is manual per the Phase-15 precedent). **No
drag-and-drop library.**

## Why
The calendar's reschedule UX is the headline interaction of Phase 16. Pointer events (not the older
mouse/DnD APIs) cover desktop + touch in one model and keep the dependency list at zero. The
interaction is constrained — same-grid, day-cell snap, single source chip — so a library is
overhead. A composable keeps the state machine testable in isolation.

## Depends on
- **Task 16-2** — `PATCH .../schedule` endpoint + `reschedulePlannedWorkout` service (declared in
  16-3's `services/calendar.ts`).
- **Task 16-3** — `CalendarItemChip`, `CalendarDayCell`, `CalendarGrid` (the chip becomes draggable;
  the cell becomes a drop target).
- **ADR-0008** §2 (out-of-window → visible message; 204 on success; the feed re-fetches after).

## Required reading
- `ui/src/components/calendar/CalendarItemChip.vue` (from 16-3) — the chip to make draggable.
- `ui/src/components/calendar/CalendarDayCell.vue` + `CalendarGrid.vue` (from 16-3) — the drop targets.
- `ui/src/composables/` — pick an existing composable (e.g. `useCountUp` if it exists, or any
  `use*.ts`) and match the file naming + export style.
- `ui/src/services/apiErrors.ts` — the error-shape parser; the out-of-window 400 surfaces a
  `ScheduledDate` field error that the composable must turn into a visible message.
- The Vue 3 pointer-events docs / a known-good reference implementation — pointer events have edge
  cases (pointer capture, `pointercancel`, touch-action). The composable must set
  `touch-action: none` on draggable chips via a CSS class to prevent the browser from scrolling on
  touch drag.

## Acceptance criteria

### `useDragReschedule` composable (`ui/src/composables/useDragReschedule.ts`)
A single composable owning the entire interaction state machine. Pure state + callbacks; no direct
DOM manipulation beyond what Vue's `v-on` bindings give you. Returns:
- `draggingItem: Ref<CalendarItemDto | null>` — the chip being dragged (null at rest).
- `draggingOverDate: Ref<string | null>` — the YYYY-MM-DD the pointer is currently over (null at rest
  or when over an invalid target).
- `isDragging: ComputedRef<boolean>` — `draggingItem.value !== null`.
- `canDropHere: ComputedRef<boolean>` — true when `draggingOverDate` is within the plan window of the
  dragging item's plan (the composable needs the plan window; see the props section below).
- `error: Ref<string | null>` — the last reschedule error (out-of-window 400, network, 404). Cleared
  on the next drag start.
- `onPointerDown(item: CalendarItemDto, event: PointerEvent): void` — starts a drag (only for
  `Kind === Planned` items — completed/event chips are not draggable).
- `onPointerMove(event: PointerEvent): void` — updates `draggingOverDate` based on
  `document.elementFromPoint(event.clientX, event.clientY)` (find the closest `[data-date]` ancestor).
- `onPointerUp(event: PointerEvent): Promise<void>` — if `canDropHere` and the target differs from
  the item's current date, calls `reschedulePlannedWorkout(planId, plannedWorkoutId, newDate)`; on
  success, the store re-fetches the feed; on failure, sets `error` and leaves the chip in place.
- `onPointerCancel(): void` — resets state (covers `pointercancel`).
- `cancelDrag(): void` — programmatic cancel (Esc key — wire a global listener while `isDragging`).

**Plan window:** the composable takes `planWindows: ComputedRef<Record<string, { start: string; end: string }>>`
(keyed by planId) as a setup arg, OR the `CalendarItemDto` carries its plan window inline. **Preferred:**
extend `CalendarItemDto` on the frontend only (a derived `planWindow?: { start: string; end: string }`
field populated by the store from the loaded plans) — this keeps the composable stateless re: plan
lookup. If the feed doesn't carry the plan id on planned items, **stop and ask** — the 16-1 spec has
`PlannedWorkoutId` on completed items but the planned item's `TrainingPlanId` isn't explicitly listed.
Add `TrainingPlanId: Guid?` to the `CalendarItemDto` for planned items in 16-1 if it's missing (this
is a 16-1 amendment — flag it during 16-4 planning, not silently).

**Out-of-window UX:** when `canDropHere` is false (pointer over a cell outside the plan window), the
target cell renders a "rejected" visual (dashed red border or a ban icon) and `onPointerUp` is a
no-op that resets state. When the server returns a 400 (the network path can still hit this if the
client's plan-window view is stale), `error` carries the field error message.

### Component wiring
- `CalendarItemChip.vue`:
  - Add `@pointerdown="onPointerDown(item, $event)"` on the root element for `Kind === Planned` items.
  - Add `:class="{ 'cursor-grab': item.Kind === 'Planned', 'is-dragging': isDragging && draggingItem?.Id === item.Id }"`.
  - Add `style="touch-action: none"` (or a Tailwind class) on draggable chips.
  - Completed and event chips: no pointer handlers, `cursor: default`.
- `CalendarDayCell.vue`:
  - Add `data-date="<YYYY-MM-DD>"` attribute on the cell root (the composable's `elementFromPoint` walk
    looks for this).
  - Add `:class="{ 'drop-target': isDragging && draggingOverDate === cell.date, 'drop-rejected': isDragging && draggingOverDate === cell.date && !canDropHere }"`.
- `CalendarGrid.vue`:
  - Hosts the composable instance (provides it via `provide`/`inject` so chips and cells share one
    state machine per grid — do **not** instantiate per-chip).
  - Wires a global `pointermove`/`pointerup` listener while `isDragging` (or attaches them on the
    grid root; either is fine — pin one and test it).
  - Renders an error toast/banner when `error` is non-null (auto-clear after 5s or on next drag start).

### Tests (`ui/src/composables/useDragReschedule.spec.ts`)
- `onPointerDown` on a planned item sets `draggingItem`; on a completed/event item is a no-op.
- `onPointerMove` updates `draggingOverDate` based on a mocked `elementFromPoint` returning an element
  with a known `data-date`.
- `canDropHere` is true when the target date is within the plan window; false outside.
- `onPointerUp` on a valid target calls `reschedulePlannedWorkout` with the right ids + date; on a
  no-op (same date) does not call the service.
- `onPointerUp` on an invalid target (out of window) does not call the service and resets state.
- A mocked 400 response (out-of-window) sets `error` to the field-error message; the chip stays in place.
- A mocked 404 sets `error`; the chip stays in place (the feed re-fetch may reveal the item is gone).
- `onPointerCancel` and `cancelDrag` (Esc) reset state without calling the service.
- `pnpm run build` green; `pnpm test` green.

### Manual smoke test (documented in the impl spec, not automated)
- Drag a planned chip to another day in the same month, within the plan window → chip moves, survives
  reload, feed re-fetches.
- Drag a planned chip to a day outside the plan window → "rejected" visual on hover, drop is a no-op.
- Tap-to-move (mobile): tap a planned chip → it enters "selected" state; tap a target day cell →
  reschedule fires (the same composable handles this — `onPointerDown` + `onPointerUp` on a touch
  device with no `pointermove` between them is a tap; a tap on a different cell is a tap-to-move).
  Document this explicitly so 16-5's mobile testing covers it.
- Out-of-window tap-to-move → same rejected visual + no-op.
- Esc during a drag → cancels.

## What NOT to modify
- No new npm package — no `vuedraggable`, `sortablejs`, `@vueuse/core` `useDraggable` (the last is
  borderline; if it covers the constrained-same-grid case cleanly, flag it for approval — but the
  default is hand-rolled pointer events per ADR-0008).
- Don't make completed or event chips draggable — only planned items reschedule.
- Don't change the 16-1 feed shape or the 16-2 endpoint — this task consumes them.
- Don't change `CalendarGrid`/`CalendarDayCell`/`CalendarItemChip` rendering beyond the
  pointer/dnd-specific class + attribute additions.
- Don't re-implement the compliance dot or chip layout — 16-3 owns that.
- Don't add a separate "tap-to-move" code path — the same composable handles pointer down/move/up;
  document the tap semantics in the composable's JSDoc.

## Suggested commit
```
feat(ui): calendar reschedule (pointer-event drag + tap-to-move)

useDragReschedule composable owns the drag state machine: pointerdown on
a planned chip starts a drag, pointermove tracks the hovered day cell via
elementFromPoint, pointerup calls reschedulePlannedWorkout when the
target is in-window (ADR-0008 §2). Out-of-window targets render a
rejected visual and the drop is a no-op; server 400s surface the
field-error message. Touch + desktop share one pointer-event path
(touch-action: none on draggable chips). No drag-and-drop library.
Vitest pins the state machine.
```
