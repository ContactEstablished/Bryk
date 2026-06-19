# Impl 16-4 — Build order: reschedule interactions (pointer-event drag + tap-to-move)

**Executor:** GLM 5.2. **Acceptance contract:** `md/Tasks-16-4.md`. **Decision lock:** ADR-0008 §2.
**Scope:** Frontend only. No new npm package (no `vuedraggable`, no `@vueuse/core` `useDraggable`
without explicit approval).

## Step 0 — Pre-flight

- `git status` clean (16-1/2/3 committed). `pnpm run build` + `pnpm test` green.
- Re-read `md/Tasks-16-4.md` + ADR-0008 §2. Open: `ui/src/components/calendar/CalendarItemChip.vue`,
  `CalendarDayCell.vue`, `CalendarGrid.vue` (from 16-3); `ui/src/composables/` (pick an existing
  composable for style); `ui/src/services/apiErrors.ts` (the `extractApiValidationMessages` helper —
  reuse it for the out-of-window 400 message); `ui/src/services/calendar.ts`
  (`reschedulePlannedWorkout` from 16-3); `ui/src/stores/calendar.ts`.
- **Confirm 16-1 carries `TrainingPlanId` on planned `CalendarItemDto`** (the 16-3 amendment). If not,
  stop and amend 16-1 first — 16-4's PATCH route needs it.

## Step 1 — `useDragReschedule` composable

**New file** `ui/src/composables/useDragReschedule.ts`.

```ts
import { computed, ref, type Ref } from 'vue'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { reschedulePlannedWorkout } from '@/services/calendar'
import type { CalendarItemDto } from '@/types/calendar'

interface PlanWindow { start: string; end: string }

export function useDragReschedule(options: {
  planWindows: Ref<Record<string, PlanWindow>>  // keyed by planId
  onRescheduled: () => void | Promise<void>     // store re-fetch
}) {
  const draggingItem = ref<CalendarItemDto | null>(null)
  const draggingOverDate = ref<string | null>(null)
  const error = ref<string | null>(null)
  let pointerId: number | null = null

  const isDragging = computed(() => draggingItem.value !== null)
  const canDropHere = computed(() => {
    const item = draggingItem.value
    const over = draggingOverDate.value
    if (!item || !over || !item.trainingPlanId) return false
    const window = options.planWindows.value[item.trainingPlanId]
    if (!window) return false
    return over >= window.start && over <= window.end
  })

  function onPointerDown(item: CalendarItemDto, event: PointerEvent): void {
    if (item.kind !== 'Planned') return
    // Distinguish a drag from a click: start dragging immediately but only "commit" if pointermove
    // crosses a threshold. For simplicity here, set draggingItem on down and let pointerup decide:
    // if pointerup fires with no move (a tap), treat as a no-op drag (16-5's tap-to-move uses a
    // separate tap on a target cell after selecting a chip — see the note below).
    draggingItem.value = item
    draggingOverDate.value = null
    error.value = null
    pointerId = event.pointerId
    // Pointer capture ensures we keep receiving move/up even if the pointer leaves the chip.
    ;(event.target as Element).setPointerCapture?.(event.pointerId)
  }

  function onPointerMove(event: PointerEvent): void {
    if (!draggingItem.value) return
    const el = document.elementFromPoint(event.clientX, event.clientY)
    const cell = el?.closest('[data-date]') as HTMLElement | null
    draggingOverDate.value = cell?.dataset.date ?? null
  }

  async function onPointerUp(event: PointerEvent): Promise<void> {
    const item = draggingItem.value
    const over = draggingOverDate.value
    try {
      ;(event.target as Element).releasePointerCapture?.(event.pointerId)
    } catch { /* ignore */ }
    draggingItem.value = null
    draggingOverDate.value = null
    pointerId = null
    if (!item || !over || !item.trainingPlanId || !item.id) return
    if (over === itemDateKey(item)) return  // no-op: dropped on same day
    if (!canDropHere.value) return          // rejected by window
    try {
      await reschedulePlannedWorkout(item.trainingPlanId, item.id, over)
      await options.onRescheduled()
    } catch (e) {
      error.value = errorMessage(e)
    }
  }

  function onPointerCancel(): void {
    draggingItem.value = null
    draggingOverDate.value = null
    error.value = null
    pointerId = null
  }

  function cancelDrag(): void { onPointerCancel() }

  function errorMessage(e: unknown): string {
    const msgs = extractApiValidationMessages(e)
    if (msgs && msgs.length) return msgs[0]
    if (e instanceof ApiError) return e.statusText || `Request failed (${e.status})`
    return 'Could not reschedule workout.'
  }

  function itemDateKey(item: CalendarItemDto): string | null {
    // The chip doesn't carry its own date — the cell does. The caller passes the current cell's date
    // via a closure when wiring onPointerDown (see Step 2). Stash it on the item reference.
    return (item as CalendarItemDto & { __currentDate?: string }).__currentDate ?? null
  }

  return {
    draggingItem, draggingOverDate, isDragging, canDropHere, error,
    onPointerDown, onPointerMove, onPointerUp, onPointerCancel, cancelDrag,
  }
}
```

**Tap-to-move note:** the composable's pointerdown/up with no move between is a tap. For 16-5's
tap-to-move, the chip stays "selected" after a tap and a subsequent tap on a target cell fires the
reschedule — that's a separate small state machine. **For 16-4**, ship drag only; tap-to-move lands
with 16-5 (document this in the composable's JSDoc so 16-5 knows where to add it).

## Step 2 — Wire the composable into `CalendarGrid.vue`

`CalendarGrid` instantiates the composable (one instance per grid) and `provide`s it so chips and
cells share state:

```ts
// in CalendarGrid.vue <script setup>
import { provide, ref, type Ref } from 'vue'
import { useDragReschedule } from '@/composables/useDragReschedule'
import { useCalendarStore } from '@/stores/calendar'

const props = defineProps<{ days: CalendarDayDto[]; anchorMonth: { year: number; month: number } }>()

const planWindows = ref<Record<string, { start: string; end: string }>>({})
// Populate planWindows from the loaded plans — the calendar store's feed doesn't carry plan windows
// directly. Options: (a) load the athlete's plans via getPlans() and build the map; (b) extend the
// 16-1 feed to include plan windows. Preferred: (a) — a one-time getPlans() call in CalendarView
// on mount, passed down as a prop. Add a prop `planWindows` to CalendarGrid.
const { draggingItem, draggingOverDate, isDragging, canDropHere, error, onPointerDown, onPointerMove, onPointerUp, onPointerCancel, cancelDrag } =
  useDragReschedule({ planWindows, onRescheduled: () => calendarStore.loadFeed() })

provide('dragReschedule', { onPointerDown, onPointerMove, onPointerUp, onPointerCancel, draggingItem, draggingOverDate, isDragging, canDropHere, cancelDrag })

// Global listeners while dragging — wire onMounted/onUnmounted, or use @vueuse/core's useEventListener
// if it's already a dep; otherwise hand-roll window.addEventListener.
```

**Plan windows source:** In `CalendarView.vue` `onMounted`, call `getPlans()` (already in
`services/training.ts`) and build `planWindows: Record<string, { start: string; end: string }>` keyed
by `plan.id` with `{ start: isoDate(plan.startDate), end: isoDate(plan.endDate) }`. Pass as a prop to
`CalendarGrid`. (Verify `TrainingPlanResponse` carries `startDate`/`endDate` as strings — read
`types/training.ts`.)

## Step 3 — `CalendarItemChip.vue` additions

- Add `@pointerdown="onPointerDown(itemWithDate, $event)"` on the root, where `itemWithDate` is the
  item with `__currentDate` set to the cell's date (the cell passes its date down via a prop or
  provide/inject — simplest: the cell wraps the chip and passes `currentDate` as a prop, and the chip
  stamps it onto the item before calling `onPointerDown`).
- Only for `item.kind === 'Planned'` — completed/event chips skip the handler.
- `:class="{ 'cursor-grab': item.kind === 'Planned', 'cursor-default': item.kind !== 'Planned', 'is-dragging': isDragging && draggingItem?.id === item.id }"`.
- `style="touch-action: none"` (or Tailwind `touch-none`) on the root for planned chips only —
  prevents the browser from scrolling on touch drag.
- Inject the composable's exposed handlers via `inject('dragReschedule')`.

## Step 4 — `CalendarDayCell.vue` additions

- Add `:data-date="cell.date"` on the root (the composable's `elementFromPoint` walk looks for this).
- `:class="{ 'drop-target': isDragging && draggingOverDate === cell.date, 'drop-rejected': isDragging && draggingOverDate === cell.date && !canDropHere }"`.
- CSS: `.drop-target { outline: 2px dashed var(--primary-hi); }`,
  `.drop-rejected { outline: 2px dashed var(--rose-500); }` (in a scoped style block or a Tailwind
  arbitrary class).

## Step 5 — Error display in `CalendarGrid.vue` (or `CalendarView.vue`)

- Render an error toast/banner when `error.value` is non-null. Auto-clear after 5s
  (`setTimeout(() => { error.value = null }, 5000)`) and on the next drag start (the composable
  already clears on `onPointerDown`).
- Place at the top of the grid view, below the header.

## Step 6 — Esc-to-cancel

In `CalendarGrid.vue` `onMounted`: `window.addEventListener('keydown', onKey)`, where `onKey` calls
`cancelDrag()` if `e.key === 'Escape' && isDragging.value`. `onUnmounted`: remove the listener.

## Step 7 — Tests (`ui/src/composables/useDragReschedule.spec.ts`)

- Mock `reschedulePlannedWorkout` (vi.mock) and `document.elementFromPoint`.
- `onPointerDown` on a planned item sets `draggingItem`; on a completed/event item is a no-op.
- `onPointerMove` with a mocked `elementFromPoint` returning an element with `data-date="2026-06-20"`
  sets `draggingOverDate` to `"2026-06-20"`.
- `canDropHere`: target inside window → true; outside → false; no plan window → false.
- `onPointerUp` on a valid different-day target calls `reschedulePlannedWorkout(planId, pwId, "2026-06-20")`
  + `onRescheduled`; same-day → no call; out-of-window → no call.
- Mocked 400 (ApiError with `body.errors = ["ScheduledDate: ..."]`) sets `error` to that message; no
  re-fetch.
- Mocked 404 sets `error`; no re-fetch.
- `onPointerCancel` and `cancelDrag` reset state, no call.

**Verify:** `pnpm run build` + `pnpm test` green.

## Step 8 — Manual smoke (documented; not automated)

- Desktop drag: planned chip → another day in-window → chip moves, survives reload.
- Desktop drag out-of-window → "rejected" outline, drop no-op.
- Touch drag (mobile or devtools touch emulation): same as desktop, `touch-action: none` prevents scroll.
- Esc during a drag → cancels, chip stays.
- Server 400 (force by stale plan window) → error toast appears.
- **Tap-to-move is deferred to 16-5** — document in the composable's JSDoc.

## Step 9 — Final verification + commit

- `pnpm run build` + `pnpm test` green.
- `git diff --stat` — only `useDragReschedule.ts` + spec, edits to `CalendarGrid.vue`,
  `CalendarDayCell.vue`, `CalendarItemChip.vue`, `CalendarView.vue` (for the planWindows prop). No
  new package, no changes to the feed or endpoint.
- Commit with the message in `Tasks-16-4.md`.
