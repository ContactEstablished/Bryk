import { computed, ref, type Ref } from 'vue'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { reschedulePlannedWorkout } from '@/services/calendar'
import type { CalendarItemDto } from '@/types/calendar'

export interface PlanWindow {
  start: string
  end: string
}

/**
 * Draggable item extended with the origin date stamped on pointerdown
 * so the composable can compare the drop target against the item's current cell.
 */
type DragItem = CalendarItemDto & { __currentDate?: string }

/**
 * The public API exposed by `useDragReschedule` and shared via `provide`/`inject`.
 */
export interface DragRescheduleContext {
  draggingItem: Ref<CalendarItemDto | null>
  draggingOverDate: Ref<string | null>
  isDragging: Ref<boolean>
  canDropHere: Ref<boolean>
  error: Ref<string | null>
  onPointerDown: (item: CalendarItemDto, event: PointerEvent) => void
  onPointerMove: (event: PointerEvent) => void
  onPointerUp: (event: PointerEvent) => Promise<void>
  onPointerCancel: () => void
  cancelDrag: () => void
}

/**
 * Pointer-event drag state machine for rescheduling planned workouts on the calendar.
 *
 * Pointerdown on a planned chip starts a drag; pointermove tracks the hovered day cell
 * via `document.elementFromPoint` → `closest('[data-date]')`; pointerup calls
 * `reschedulePlannedWorkout` when the target is in-window and differs from the origin
 * day. Out-of-window targets render a rejected visual and the drop is a no-op.
 * Server 400s surface the field-error message via `extractApiValidationMessages`.
 *
 * Touch and desktop share one pointer-event path — draggable chips set
 * `touch-action: none` to prevent the browser from hijacking the gesture for scroll.
 *
 * **Tap-to-move is deferred to 16-5.** A pointerdown/up with no intervening move is
 * a tap; for 16-4 it is treated as a no-op (the drag starts and immediately ends
 * on the same day). 16-5 will add a "selected" chip state and a tap-on-target-cell
 * path that fires `reschedulePlannedWorkout` through this same composable.
 */
export function useDragReschedule(options: {
  planWindows: Ref<Record<string, PlanWindow>>
  onRescheduled: () => void | Promise<void>
}): DragRescheduleContext {
  const draggingItem = ref<CalendarItemDto | null>(null)
  const draggingOverDate = ref<string | null>(null)
  const error = ref<string | null>(null)

  const isDragging = computed(() => draggingItem.value !== null)

  const canDropHere = computed(() => {
    const item = draggingItem.value as DragItem | null
    const over = draggingOverDate.value
    if (!item || !over || !item.trainingPlanId) return false
    const window = options.planWindows.value[item.trainingPlanId]
    if (!window) return false
    return over >= window.start && over <= window.end
  })

  function onPointerDown(item: CalendarItemDto, event: PointerEvent): void {
    if (item.kind !== 'Planned') return
    draggingItem.value = item
    draggingOverDate.value = null
    error.value = null
    ;(event.target as Element).setPointerCapture?.(event.pointerId)
  }

  function onPointerMove(event: PointerEvent): void {
    if (!draggingItem.value) return
    const el = document.elementFromPoint(event.clientX, event.clientY)
    const cell = el?.closest('[data-date]') as HTMLElement | null
    draggingOverDate.value = cell?.dataset.date ?? null
  }

  async function onPointerUp(event: PointerEvent): Promise<void> {
    const item = draggingItem.value as DragItem | null
    const over = draggingOverDate.value
    const canDrop = canDropHere.value // snapshot before clearing draggingItem
    try {
      ;(event.target as Element).releasePointerCapture?.(event.pointerId)
    } catch {
      /* ignore — the capture may already be released */
    }
    draggingItem.value = null
    draggingOverDate.value = null
    if (!item || !over || !item.trainingPlanId || !item.id) return
    if (over === item.__currentDate) return // no-op: dropped on same day
    if (!canDrop) return // rejected by window
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
  }

  function cancelDrag(): void {
    onPointerCancel()
  }

  return {
    draggingItem,
    draggingOverDate,
    isDragging,
    canDropHere,
    error,
    onPointerDown,
    onPointerMove,
    onPointerUp,
    onPointerCancel,
    cancelDrag,
  }
}

function errorMessage(e: unknown): string {
  const msgs = extractApiValidationMessages(e)
  if (msgs && msgs.length) return msgs[0]
  if (e instanceof ApiError) return e.statusText || `Request failed (${e.status})`
  return 'Could not reschedule workout.'
}
