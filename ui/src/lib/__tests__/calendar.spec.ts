import { describe, expect, it } from 'vitest'
import { buildMonthMatrix, complianceColor, groupItemsByDay, isoDate, sportColor } from '@/lib/calendar'
import type { CalendarDayDto, CalendarFeedResponse } from '@/types/calendar'

function day(date: string): CalendarDayDto {
  return { date, items: [] }
}

describe('isoDate', () => {
  it('formats a known date as YYYY-MM-DD', () => {
    expect(isoDate(new Date(2026, 5, 19))).toBe('2026-06-19')
  })

  it('zero-pads single-digit months and days', () => {
    expect(isoDate(new Date(2026, 2, 3))).toBe('2026-03-03')
  })
})

describe('groupItemsByDay', () => {
  it('produces a map keyed by date', () => {
    const feed: CalendarFeedResponse = {
      rangeStart: '2026-06-01',
      rangeEnd: '2026-06-03',
      days: [day('2026-06-01'), day('2026-06-02'), day('2026-06-03')],
    }
    const map = groupItemsByDay(feed)
    expect(map.size).toBe(3)
    expect(map.has('2026-06-01')).toBe(true)
    expect(map.has('2026-06-02')).toBe(true)
    expect(map.has('2026-06-03')).toBe(true)
  })
})

describe('buildMonthMatrix', () => {
  it('builds a Monday-anchored 5-week grid for June 2026 (starts Monday)', () => {
    const today = '2026-06-19'
    const matrix = buildMonthMatrix([], { year: 2026, month: 6 }, today)

    // June 2026 starts on Monday → 0 leading blanks, 5 weeks.
    expect(matrix).toHaveLength(5)
    expect(matrix.every((row) => row.length === 7)).toBe(true)

    // Cell [0][0] = Monday June 1.
    expect(matrix[0][0].date).toBe('2026-06-01')
    expect(matrix[0][0].isInMonth).toBe(true)

    // Cell [4][6] = Sunday July 5.
    expect(matrix[4][6].date).toBe('2026-07-05')
    expect(matrix[4][6].isInMonth).toBe(false)

    // Today's cell.
    const todayCell = matrix.flat().find((c) => c.isToday)
    expect(todayCell).toBeDefined()
    expect(todayCell!.date).toBe('2026-06-19')
  })

  it('marks days outside the anchor month as isInMonth=false', () => {
    const matrix = buildMonthMatrix([], { year: 2026, month: 6 }, '2026-06-01')

    // June 2026: first row is all June (leadingBlanks=0). Last row has July 1-5.
    const lastRow = matrix[matrix.length - 1]
    expect(lastRow[0].date).toBe('2026-06-29')
    expect(lastRow[0].isInMonth).toBe(true)
    expect(lastRow[6].date).toBe('2026-07-05')
    expect(lastRow[6].isInMonth).toBe(false)
    expect(lastRow[6].items).toEqual([])
  })

  it('populates items from the provided days when available', () => {
    const days: CalendarDayDto[] = [
      { date: '2026-06-01', items: [{ id: 'a', kind: 'Planned', title: 'Run', isUnplanned: false }] },
    ]
    const matrix = buildMonthMatrix(days, { year: 2026, month: 6 }, '2026-06-01')
    expect(matrix[0][0].items).toHaveLength(1)
    expect(matrix[0][0].items[0].title).toBe('Run')
  })

  it('handles a month starting mid-week (July 2026 starts Wednesday)', () => {
    // July 2026: Wednesday = day 3, leadingBlanks = (3+6)%7 = 2.
    const matrix = buildMonthMatrix([], { year: 2026, month: 7 }, '2026-07-01')

    // Cell [0][0] = Monday June 29 (2 leading blanks).
    expect(matrix[0][0].date).toBe('2026-06-29')
    expect(matrix[0][0].isInMonth).toBe(false)
    // Cell [0][2] = Wednesday July 1.
    expect(matrix[0][2].date).toBe('2026-07-01')
    expect(matrix[0][2].isInMonth).toBe(true)
  })
})

describe('complianceColor', () => {
  it('returns the pinned Tailwind class for each bucket', () => {
    expect(complianceColor('Green').dot).toBe('bg-emerald-500')
    expect(complianceColor('Yellow').dot).toBe('bg-amber-400')
    expect(complianceColor('Red').dot).toBe('bg-rose-500')
    expect(complianceColor('Grey').dot).toBe('bg-slate-400')
  })

  it('returns an empty dot for null/undefined (events, no compliance)', () => {
    expect(complianceColor(null).dot).toBe('')
    expect(complianceColor(undefined).dot).toBe('')
  })
})

describe('sportColor', () => {
  it('returns the pinned Tailwind class for each sport', () => {
    expect(sportColor('Bike')).toBe('bg-sky-500')
    expect(sportColor('Run')).toBe('bg-emerald-500')
    expect(sportColor('Swim')).toBe('bg-teal-500')
    expect(sportColor('Strength')).toBe('bg-orange-500')
  })

  it('falls back to slate for unknown/missing sport', () => {
    expect(sportColor('Triathlon')).toBe('bg-slate-400')
    expect(sportColor(null)).toBe('bg-slate-400')
    expect(sportColor(undefined)).toBe('bg-slate-400')
  })
})