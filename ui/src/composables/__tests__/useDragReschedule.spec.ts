import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import { useDragReschedule, type PlanWindow } from '@/composables/useDragReschedule'
import type { CalendarItemDto } from '@/types/calendar'

// ── Mock reschedulePlannedWorkout ──
const mockReschedule = vi.fn()
vi.mock('@/services/calendar', () => ({
  getCalendarFeed: vi.fn(),
  reschedulePlannedWorkout: (...args: unknown[]) => mockReschedule(...args),
}))

// ── Helpers ──

/**
 * Create a PointerEvent with a real Element target.
 * jsdom's PointerEvent constructor doesn't set target, so we
 * override it after construction.
 */
function ptrEvent(target: Element, extra: Partial<PointerEvent> = {}): PointerEvent {
  const ev = new PointerEvent('pointerdown', { pointerId: 1, ...extra }) as PointerEvent
  Object.defineProperty(ev, 'target', { value: target, writable: false })
  return ev
}

function item(overrides: Partial<CalendarItemDto> = {}): CalendarItemDto {
  return {
    id: 'pw-1',
    kind: 'Planned',
    title: 'Morning Run',
    isUnplanned: false,
    trainingPlanId: 'plan-1',
    ...overrides,
  }
}

function window(start: string, end: string): PlanWindow {
  return { start, end }
}

/**
 * Stub document.elementFromPoint to return a child HTMLElement whose
 * `.closest('[data-date]')` returns a cell with the given data-date.
 *
 * jsdom's document.elementFromPoint exists but is not spyable via vi.spyOn,
 * so we replace it directly.
 */
function stubDropTarget(dataDate: string | null) {
  const cell = dataDate
    ? ({
        dataset: { date: dataDate },
        closest: (_sel: string) => cell as unknown as Element,
      } as unknown as Element)
    : null

  const child = {
    tagName: 'DIV',
    closest: (sel: string) => (sel === '[data-date]' ? cell : null),
  } as unknown as Element

  document.elementFromPoint = vi.fn().mockReturnValue(child) as unknown as typeof document.elementFromPoint
}

/** A dummy element to serve as event.target. */
const dummyTarget = Object.assign(document.createElement('div'), { id: 'dummy-target' })

// The test-setup already stubs setPointerCapture/releasePointerCapture on
// Element.prototype, so we don't need to mock those.

function createDrag(options?: { planWindows?: Record<string, PlanWindow> }) {
  const planWindows = ref(options?.planWindows ?? { 'plan-1': window('2026-06-01', '2026-06-30') })
  const onRescheduled = vi.fn().mockResolvedValue(undefined)
  return { ...useDragReschedule({ planWindows, onRescheduled }), onRescheduled }
}

// Simulate a drag that moved to a new date inside the plan window.
function dragToTarget(drag: ReturnType<typeof createDrag>, targetDate: string) {
  drag.onPointerDown(
    { ...item(), __currentDate: '2026-06-10' } as CalendarItemDto,
    ptrEvent(dummyTarget),
  )
  stubDropTarget(targetDate)
  drag.onPointerMove(ptrEvent(dummyTarget))
}

// ── Tests ──
describe('useDragReschedule', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    mockReschedule.mockClear()
    // Restore the original elementFromPoint after each test.
    document.elementFromPoint = (() => null) as unknown as typeof document.elementFromPoint
  })

  afterEach(() => {
    document.elementFromPoint = (() => null) as unknown as typeof document.elementFromPoint
  })

  describe('onPointerDown', () => {
    it('sets draggingItem for a Planned item', () => {
      const drag = createDrag()
      const i = item()
      drag.onPointerDown(i, ptrEvent(dummyTarget))
      expect(drag.draggingItem.value).toStrictEqual(i)
    })

    it('is a no-op for a Completed item', () => {
      const drag = createDrag()
      drag.onPointerDown(item({ kind: 'Completed' }), ptrEvent(dummyTarget))
      expect(drag.draggingItem.value).toBeNull()
    })

    it('is a no-op for an Event item', () => {
      const drag = createDrag()
      drag.onPointerDown(item({ kind: 'Event' }), ptrEvent(dummyTarget))
      expect(drag.draggingItem.value).toBeNull()
    })

    it('clears error on new drag start', () => {
      const drag = createDrag()
      drag.error.value = 'previous error'
      drag.onPointerDown(item(), ptrEvent(dummyTarget))
      expect(drag.error.value).toBeNull()
    })

    it('sets draggingOverDate to null', () => {
      const drag = createDrag()
      drag.draggingOverDate.value = '2026-06-15'
      drag.onPointerDown(item(), ptrEvent(dummyTarget))
      expect(drag.draggingOverDate.value).toBeNull()
    })
  })

  describe('onPointerMove', () => {
    it('updates draggingOverDate from elementFromPoint', () => {
      const drag = createDrag()
      drag.onPointerDown(item(), ptrEvent(dummyTarget))
      stubDropTarget('2026-06-20')
      drag.onPointerMove(ptrEvent(dummyTarget))
      expect(drag.draggingOverDate.value).toBe('2026-06-20')
    })

    it('sets null when no [data-date] ancestor is found', () => {
      const drag = createDrag()
      drag.onPointerDown(item(), ptrEvent(dummyTarget))
      stubDropTarget(null)
      drag.onPointerMove(ptrEvent(dummyTarget))
      expect(drag.draggingOverDate.value).toBeNull()
    })

    it('is a no-op when not dragging', () => {
      const drag = createDrag()
      stubDropTarget('2026-06-20')
      drag.onPointerMove(ptrEvent(dummyTarget))
      expect(drag.draggingOverDate.value).toBeNull()
    })
  })

  describe('canDropHere', () => {
    it('is true when target is inside the plan window', () => {
      const drag = createDrag()
      drag.onPointerDown(item(), ptrEvent(dummyTarget))
      stubDropTarget('2026-06-20')
      drag.onPointerMove(ptrEvent(dummyTarget))
      expect(drag.canDropHere.value).toBe(true)
    })

    it('is false when target is outside the plan window', () => {
      const drag = createDrag()
      drag.onPointerDown(item(), ptrEvent(dummyTarget))
      stubDropTarget('2026-05-15') // before plan start
      drag.onPointerMove(ptrEvent(dummyTarget))
      expect(drag.canDropHere.value).toBe(false)
    })

    it('is false when there is no plan window', () => {
      const drag = createDrag({ planWindows: {} })
      drag.onPointerDown(item(), ptrEvent(dummyTarget))
      stubDropTarget('2026-06-20')
      drag.onPointerMove(ptrEvent(dummyTarget))
      expect(drag.canDropHere.value).toBe(false)
    })

    it('is false when item has no trainingPlanId', () => {
      const drag = createDrag()
      drag.onPointerDown(item({ trainingPlanId: null }), ptrEvent(dummyTarget))
      stubDropTarget('2026-06-20')
      drag.onPointerMove(ptrEvent(dummyTarget))
      expect(drag.canDropHere.value).toBe(false)
    })
  })

  describe('onPointerUp', () => {
    it('calls reschedulePlannedWorkout + onRescheduled for a valid different-day target', async () => {
      mockReschedule.mockResolvedValue(undefined)
      const drag = createDrag()
      dragToTarget(drag, '2026-06-20')

      await drag.onPointerUp(ptrEvent(dummyTarget))

      expect(mockReschedule).toHaveBeenCalledWith('plan-1', 'pw-1', '2026-06-20')
      expect(drag.onRescheduled).toHaveBeenCalled()
      expect(drag.draggingItem.value).toBeNull()
      expect(drag.error.value).toBeNull()
    })

    it('does not call the service when dropped on the same day', async () => {
      const drag = createDrag()
      dragToTarget(drag, '2026-06-10') // same as __currentDate

      await drag.onPointerUp(ptrEvent(dummyTarget))

      expect(mockReschedule).not.toHaveBeenCalled()
      expect(drag.onRescheduled).not.toHaveBeenCalled()
    })

    it('does not call the service when the target is outside the plan window', async () => {
      const drag = createDrag()
      drag.onPointerDown(
        { ...item(), __currentDate: '2026-06-10' } as CalendarItemDto,
        ptrEvent(dummyTarget),
      )
      stubDropTarget('2026-05-15') // before plan start
      drag.onPointerMove(ptrEvent(dummyTarget))

      await drag.onPointerUp(ptrEvent(dummyTarget))

      expect(mockReschedule).not.toHaveBeenCalled()
      expect(drag.onRescheduled).not.toHaveBeenCalled()
    })

    it('sets error on a mocked 400 (out-of-window via server)', async () => {
      mockReschedule.mockRejectedValue(
        new ApiError(400, 'Bad Request', {
          errors: ['ScheduledDate must be within the plan window.'],
        }),
      )
      const drag = createDrag()
      dragToTarget(drag, '2026-06-20')

      await drag.onPointerUp(ptrEvent(dummyTarget))

      expect(drag.error.value).toBe('ScheduledDate must be within the plan window.')
      expect(drag.onRescheduled).not.toHaveBeenCalled()
    })

    it('sets error on a mocked 404', async () => {
      mockReschedule.mockRejectedValue(new ApiError(404, 'Not Found', null))
      const drag = createDrag()
      dragToTarget(drag, '2026-06-20')

      await drag.onPointerUp(ptrEvent(dummyTarget))

      expect(drag.error.value).toBe('Not Found')
      expect(drag.onRescheduled).not.toHaveBeenCalled()
    })

    it('sets error on a generic Error', async () => {
      mockReschedule.mockRejectedValue(new Error('Network failure'))
      const drag = createDrag()
      dragToTarget(drag, '2026-06-20')

      await drag.onPointerUp(ptrEvent(dummyTarget))

      expect(drag.error.value).toBe('Could not reschedule workout.')
      expect(drag.onRescheduled).not.toHaveBeenCalled()
    })

    it('resets state to null after pointerup', async () => {
      mockReschedule.mockResolvedValue(undefined)
      const drag = createDrag()
      dragToTarget(drag, '2026-06-20')

      await drag.onPointerUp(ptrEvent(dummyTarget))

      expect(drag.draggingItem.value).toBeNull()
      expect(drag.draggingOverDate.value).toBeNull()
    })
  })

  describe('onPointerCancel', () => {
    it('resets state without calling the service', async () => {
      const drag = createDrag()
      drag.onPointerDown(
        { ...item(), __currentDate: '2026-06-10' } as CalendarItemDto,
        ptrEvent(dummyTarget),
      )

      drag.onPointerCancel()

      expect(drag.draggingItem.value).toBeNull()
      expect(drag.draggingOverDate.value).toBeNull()
      expect(mockReschedule).not.toHaveBeenCalled()
      expect(drag.onRescheduled).not.toHaveBeenCalled()
    })
  })

  describe('cancelDrag', () => {
    it('resets state without calling the service', async () => {
      const drag = createDrag()
      drag.onPointerDown(
        { ...item(), __currentDate: '2026-06-10' } as CalendarItemDto,
        ptrEvent(dummyTarget),
      )

      drag.cancelDrag()

      expect(drag.draggingItem.value).toBeNull()
      expect(drag.draggingOverDate.value).toBeNull()
      expect(mockReschedule).not.toHaveBeenCalled()
    })
  })
})
