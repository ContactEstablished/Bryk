import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import DayDetailPopover from '@/components/calendar/DayDetailPopover.vue'
import type { CalendarDayCell } from '@/lib/calendar'
import type { CalendarItemDto } from '@/types/calendar'

function cell(date: string, items: CalendarItemDto[]): CalendarDayCell {
  return { date, items, isInMonth: true, isToday: false }
}

function plannedI(overrides: Partial<CalendarItemDto> = {}): CalendarItemDto {
  return {
    id: 'pw-1',
    kind: 'Planned',
    title: 'Morning Run',
    load: 55,
    isUnplanned: false,
    trainingPlanId: 'plan-1',
    sport: 'Run',
    compliance: 'Grey',
    ...overrides,
  }
}

function completedI(overrides: Partial<CalendarItemDto> = {}): CalendarItemDto {
  return {
    id: 'w-1',
    kind: 'Completed',
    title: 'Morning Run',
    load: 60,
    isUnplanned: false,
    workoutId: 'w-1',
    sport: 'Run',
    compliance: 'Green',
    ...overrides,
  }
}

function eventI(overrides: Partial<CalendarItemDto> = {}): CalendarItemDto {
  return {
    id: 'ev-1',
    kind: 'Event',
    title: '5K Race',
    isUnplanned: false,
    sport: 'Run',
    notes: 'Goal: sub-20',
    priority: 'A',
    ...overrides,
  }
}

// A fake DOMRect for anchor positioning.
const rect = { top: 100, left: 200, bottom: 120, right: 300, width: 100, height: 20, x: 200, y: 100 } as DOMRect

function mountPopover(props: { cell: CalendarDayCell; anchorRect: DOMRect }) {
  return mount(DayDetailPopover, {
    props,
    global: {
      stubs: { RouterLink: RouterLinkStub },
    },
    attachTo: document.body,
  })
}

describe('DayDetailPopover', () => {
  it('renders the full date header', () => {
    const wrapper = mountPopover({
      cell: cell('2026-06-19', [plannedI()]),
      anchorRect: rect,
    })

    expect(wrapper.text()).toContain('Friday, June 19')
    expect(wrapper.text()).toContain('1 item')

    wrapper.unmount()
  })

  it('renders a planned item with link to the plan browser', () => {
    const wrapper = mountPopover({
      cell: cell('2026-06-10', [plannedI({ trainingPlanId: 'plan-1' })]),
      anchorRect: rect,
    })

    const text = wrapper.text()
    expect(text).toContain('Morning Run')
    expect(text).toContain('Planned')
    expect(text).toContain('55 load')
    expect(text).toContain('View structure →')

    // Link targets /plans/{trainingPlanId}
    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.length).toBe(1)
    expect(links[0].props('to')).toBe('/plans/plan-1')

    wrapper.unmount()
  })

  it('renders a completed item with link to the workout detail', () => {
    const wrapper = mountPopover({
      cell: cell('2026-06-15', [completedI({ id: 'w-1' })]),
      anchorRect: rect,
    })

    const text = wrapper.text()
    expect(text).toContain('Completed')
    expect(text).toContain('60 load')
    expect(text).toContain('View workout →')

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.length).toBe(1)
    expect(links[0].props('to')).toBe('/workouts/w-1')

    wrapper.unmount()
  })

  it('renders an unplanned completed item with the unplanned tag', () => {
    const wrapper = mountPopover({
      cell: cell('2026-06-15', [completedI({ isUnplanned: true })]),
      anchorRect: rect,
    })

    expect(wrapper.text()).toContain('unplanned')

    wrapper.unmount()
  })

  it('renders event notes and priority badge', () => {
    const wrapper = mountPopover({
      cell: cell('2026-06-20', [eventI({ notes: 'Goal: sub-20', priority: 'A' })]),
      anchorRect: rect,
    })

    const text = wrapper.text()
    expect(text).toContain('5K Race')
    expect(text).toContain('Goal: sub-20')
    expect(text).toContain('A priority')
    expect(text).toContain('Run')

    // Events have no links
    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.length).toBe(0)

    wrapper.unmount()
  })

  it('renders multiple items in a day', () => {
    const wrapper = mountPopover({
      cell: cell('2026-06-18', [plannedI(), completedI()]),
      anchorRect: rect,
    })

    expect(wrapper.text()).toContain('2 items')
    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.length).toBe(2)

    wrapper.unmount()
  })

  it('emits close on Esc keydown', async () => {
    const wrapper = mountPopover({
      cell: cell('2026-06-19', [plannedI()]),
      anchorRect: rect,
    })

    await wrapper.trigger('keydown.Escape')

    expect(wrapper.emitted('close')).toHaveLength(1)

    wrapper.unmount()
  })
})
