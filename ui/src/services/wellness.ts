import { apiFetch } from '@/services/api'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessSummaryResponse,
} from '@/types/wellness'

// PUT is the whole write surface (ADR-0011 §2): it replaces the day, so a metric sent as null is
// cleared rather than preserved. The server answers 200 for both create and update - never 201.
// `date` is already 'YYYY-MM-DD'; interpolate it as-is. Do NOT encodeURIComponent it or reformat it -
// the route carries a {date:datetime} constraint that a re-encoded segment would fail (404).
export async function putWellness(
  date: string,
  data: WellnessEntryRequest,
): Promise<WellnessEntryResponse> {
  const result = await apiFetch<WellnessEntryResponse>(`/wellness/${date}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
  if (result === null) {
    throw new Error(`Unexpected empty response from PUT /wellness/${date}`)
  }
  return result
}

// Sparse, ascending. Both bounds are required by the server (400 otherwise); an empty body means
// "no entries in that window", which is a normal answer, not an error.
export async function getWellnessRange(
  from: string,
  to: string,
): Promise<WellnessEntryResponse[]> {
  return (await apiFetch<WellnessEntryResponse[]>(`/wellness?from=${from}&to=${to}`)) ?? []
}

// 7-day averages + deltas versus the prior 7 + a sparse 14-day daily series, in one call.
export async function getWellnessSummary(): Promise<WellnessSummaryResponse> {
  const result = await apiFetch<WellnessSummaryResponse>('/wellness/summary')
  if (result === null) {
    throw new Error('Unexpected empty response from GET /wellness/summary')
  }
  return result
}
