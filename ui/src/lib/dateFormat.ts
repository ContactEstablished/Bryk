// UTC-stable math and display for the server's DateOnly strings ('YYYY-MM-DD'). Kept apart
// from lib/format.ts (analytics display formatters) because these parse the date as a UTC
// calendar date, so day counts never shift with the viewer's timezone.

// Whole days from today (UTC) to the given date. Negative once the date is in the past.
export function daysUntil(isoDate: string): number {
  const [y, m, d] = isoDate.split('-').map(Number)
  const target = Date.UTC(y, m - 1, d)
  const now = new Date()
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())
  return Math.round((target - today) / 86_400_000)
}

// 'YYYY-MM-DD' → 'Mon, Apr 20, 2026' in the viewer's locale, without a timezone shift.
export function formatEventDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
}
