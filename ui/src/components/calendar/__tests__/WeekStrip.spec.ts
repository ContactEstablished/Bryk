import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import WeekStrip from '@/components/calendar/WeekStrip.vue'
import CalendarDayCell from '@/components/calendar/CalendarDayCell.vue'
import type { CalendarDayDto } from '@/types/calendar'

function daysForWeek(weekStart: string, items: string[] = []): CalendarDayDto[] {
  const start = new Date(weekStart + 'T00:00:00')
  const result: CalendarDayDto[] = []
  for (let i = 0; i < 7; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    const y = d.getFullYear()
    const m = String(d.getMonth() + 1).padStart(2, '0')
    const day = String(d.getDate()).padStart(2, '0')
    const dateStr = `${y}-${m}-${day}`
    result.push({
      date: dateStr,
      items: items.includes(dateStr)
        ? [{ id: `pw-${dateStr}`, kind: 'Planned', title: 'Run', isUnplanned: false }]
        : [],
    })
  }
  return result
}

function mountStrip(weekStart: string, daysOverride?: CalendarDayDto[]) {
  const days = daysOverride ?? daysForWeek(weekStart)
  return mount(WeekStrip, {
    props: { days, weekStart },
    attachTo: document.body,
  })
}

// jsdom: fake the elementFromPoint needed by setup.ts stubs
const _orig = document.elementFromPoint

describe('WeekStrip', () => {
  beforeEach(() => {
    // elementFromPoint stub for drag composable (WeekStrip doesn't own it, but CalendarDayCell may inject)
    document.elementFromPoint = (() => null) as unknown as typeof document.elementFromPoint
  })

  afterEach(() => {
    document.elementFromPoint = _orig
  })

  it('renders 7 CalendarDayCell instances for the selected week', () => {
    const wrapper = mountStrip('2026-06-15')

    const cells = wrapper.findAllComponents(CalendarDayCell)
    expect(cells.length).toBe(7)

    wrapper.unmount()
  })

  it('shows Mon-Sun day-of-week headers', () => {
    const wrapper = mountStrip('2026-06-15')

    const text = wrapper.text()
    expect(text).toContain('Mon')
    expect(text).toContain('Tue')
    expect(text).toContain('Wed')
    expect(text).toContain('Thu')
    expect(text).toContain('Fri')
    expect(text).toContain('Sat')
    expect(text).toContain('Sun')

    wrapper.unmount()
  })

  it('renders dates for the June 15–21, 2026 week (Monday-anchored)', () => {
    const wrapper = mountStrip('2026-06-15')

    // Each cell should show its date number
    const text = wrapper.text()
    // June 15–21
    expect(text).toContain('15')
    expect(text).toContain('16')
    expect(text).toContain('17')
    expect(text).toContain('18')
    expect(text).toContain('19')
    expect(text).toContain('20')
    expect(text).toContain('21')

    wrapper.unmount()
  })

  it('shifts dates when weekStart advances by 7 days', () => {
    const wrapper1 = mountStrip('2026-06-15')
    expect(wrapper1.text()).toContain('15')
    wrapper1.unmount()

    const wrapper2 = mountStrip('2026-06-22')
    expect(wrapper2.text()).toContain('22')
    expect(wrapper2.text()).toContain('28')
    wrapper2.unmount()
  })

  it('passes items from days to CalendarDayCell', () => {
    const days = daysForWeek('2026-06-15', ['2026-06-16', '2026-06-18'])
    const wrapper = mountStrip('2026-06-15', days)

    const cells = wrapper.findAllComponents(CalendarDayCell)
    // Cell for June 16 should have 1 item
    const cell16 = cells.find((c) => c.props('cell').date === '2026-06-16')
    expect(cell16).toBeDefined()
    expect(cell16!.props('cell').items).toHaveLength(1)

    // Cell for June 17 should have 0 items
    const cell17 = cells.find((c) => c.props('cell').date === '2026-06-17')
    expect(cell17).toBeDefined()
    expect(cell17!.props('cell').items).toHaveLength(0)

    wrapper.unmount()
  })

  it('forwards the openPopover event from CalendarDayCell', async () => {
    const days = daysForWeek('2026-06-15', ['2026-06-16'])
    const wrapper = mountStrip('2026-06-15', days)

    const cells = wrapper.findAllComponents(CalendarDayCell)
    const targetCell = cells.find((c) => c.props('cell').date === '2026-06-16')!

    const fakeRect = { top: 0, left: 0, bottom: 10, right: 10, width: 10, height: 10, x: 0, y: 0 } as DOMRect
    await targetCell.vm.$emit('openPopover', targetCell.props('cell'), fakeRect)

    expect(wrapper.emitted('openPopover')).toHaveLength(1)
    expect(wrapper.emitted('openPopover')![0][0].date).toBe('2026-06-16')

    wrapper.unmount()
  })
})
