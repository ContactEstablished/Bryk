import { apiFetch } from '@/services/api'
import type { DailyLoadPoint, PmcResponse, WeeklyLoadResponse } from '@/types/analytics'

// Local 'YYYY-MM-DD' (matches the DateOnly the API expects, with no timezone shift from toISOString).
export function isoDate(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

// Default dashboard range: today and the 90 days before it — wide enough that "7 days ago" is always
// in-series for the Form-tile delta.
export function defaultPmcRange(now: Date = new Date()): { from: string; to: string } {
  const to = isoDate(now)
  const fromDate = new Date(now)
  fromDate.setDate(fromDate.getDate() - 90)
  return { from: isoDate(fromDate), to }
}

// The Progress page PMC range toggle (ADR-0007 §5). The query param `?pmc=` carries one of these.
export type PmcRange = '6w' | '3m' | '6m'

export const PMC_RANGES: { value: PmcRange; label: string }[] = [
  { value: '6w', label: '6W' },
  { value: '3m', label: '3M' },
  { value: '6m', label: '6M' },
]

const PMC_RANGE_DAYS: Record<PmcRange, number> = { '6w': 42, '3m': 90, '6m': 180 }

// `to = today`, `from = today − {42|90|180} days`. Local dates, no timezone shift.
export function pmcRangeToDates(range: PmcRange, now: Date = new Date()): { from: string; to: string } {
  const to = isoDate(now)
  const fromDate = new Date(now)
  fromDate.setDate(fromDate.getDate() - PMC_RANGE_DAYS[range])
  return { from: isoDate(fromDate), to }
}

export async function getPmc(from: string, to: string): Promise<PmcResponse> {
  const result = await apiFetch<PmcResponse>(`/analytics/pmc?from=${from}&to=${to}`)
  if (result === null) {
    throw new Error('Unexpected empty response from /analytics/pmc')
  }
  return result
}

// Provided for Phase 15's charts (not used by the Phase 14 tiles, which read pmc's current summary).
export async function getDailyLoad(from: string, to: string): Promise<DailyLoadPoint[]> {
  return (await apiFetch<DailyLoadPoint[]>(`/analytics/daily-load?from=${from}&to=${to}`)) ?? []
}

// The Progress page weekly-load span toggle (ADR-0007 §5). The query param `?weeks=` carries the number.
export const WEEKLY_LOAD_RANGES: { value: string; label: string }[] = [
  { value: '8', label: '8W' },
  { value: '12', label: '12W' },
  { value: '26', label: '26W' },
]

export async function getWeeklyLoad(weeks: number): Promise<WeeklyLoadResponse> {
  const result = await apiFetch<WeeklyLoadResponse>(`/analytics/weekly-load?weeks=${weeks}`)
  if (result === null) {
    throw new Error('Unexpected empty response from /analytics/weekly-load')
  }
  return result
}
