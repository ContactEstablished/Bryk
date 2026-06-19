import type { CalendarDayDto, CalendarFeedResponse, CalendarItemDto, ComplianceBucket } from '@/types/calendar'

export interface CalendarDayCell {
  date: string
  items: CalendarItemDto[]
  isInMonth: boolean
  isToday: boolean
}

export function isoDate(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

export function groupItemsByDay(feed: CalendarFeedResponse): Map<string, CalendarDayDto> {
  const map = new Map<string, CalendarDayDto>()
  for (const day of feed.days) map.set(day.date, day)
  return map
}

export function buildMonthMatrix(
  days: CalendarDayDto[],
  anchorMonth: { year: number; month: number },
  today: string,
): CalendarDayCell[][] {
  const byDate = new Map(days.map((d) => [d.date, d]))
  const firstOfMonth = new Date(anchorMonth.year, anchorMonth.month - 1, 1)
  // Monday-anchored: ((int)DayOfWeek + 6) % 7, mirroring ThisWeekService.
  const leadingBlanks = (firstOfMonth.getDay() + 6) % 7
  const gridStart = new Date(firstOfMonth)
  gridStart.setDate(gridStart.getDate() - leadingBlanks)

  const cells: CalendarDayCell[][] = []
  let cursor = new Date(gridStart)
  for (let week = 0; week < 6; week++) {
    const row: CalendarDayCell[] = []
    for (let dow = 0; dow < 7; dow++) {
      const dateStr = isoDate(cursor)
      const dayDto = byDate.get(dateStr)
      row.push({
        date: dateStr,
        items: dayDto?.items ?? [],
        isInMonth: cursor.getMonth() + 1 === anchorMonth.month && cursor.getFullYear() === anchorMonth.year,
        isToday: dateStr === today,
      })
      cursor = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate() + 1)
    }
    // Stop before pushing a row that has no in-month cells (the month is fully rendered).
    if (!row.some((c) => c.isInMonth) && cells.length >= 4) break
    cells.push(row)
  }
  return cells
}

export function complianceColor(bucket: ComplianceBucket | null | undefined): { dot: string } {
  switch (bucket) {
    case 'Green': return { dot: 'bg-emerald-500' }
    case 'Yellow': return { dot: 'bg-amber-400' }
    case 'Red': return { dot: 'bg-rose-500' }
    case 'Grey': return { dot: 'bg-slate-400' }
    default: return { dot: '' }
  }
}

export function sportColor(sport?: string | null): string {
  switch (sport) {
    case 'Bike': return 'bg-sky-500'
    case 'Run': return 'bg-emerald-500'
    case 'Swim': return 'bg-teal-500'
    case 'Strength': return 'bg-orange-500'
    default: return 'bg-slate-400'
  }
}
