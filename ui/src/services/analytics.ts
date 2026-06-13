import { apiFetch } from '@/services/api'
import type { DailyLoadPoint, PmcResponse } from '@/types/analytics'

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
