import { apiFetch } from '@/services/api'
import type { CalendarFeedResponse } from '@/types/calendar'

export async function getCalendarFeed(from?: string, to?: string): Promise<CalendarFeedResponse> {
  const qs = new URLSearchParams()
  if (from) qs.set('from', from)
  if (to) qs.set('to', to)
  const query = qs.toString()
  const result = await apiFetch<CalendarFeedResponse>(`/calendar${query ? `?${query}` : ''}`)
  if (result === null) {
    throw new Error('Unexpected empty response from /calendar')
  }
  return result
}

// Declared here so the service module is complete; 16-4 wires the interaction to call it.
export async function reschedulePlannedWorkout(
  planId: string,
  plannedWorkoutId: string,
  scheduledDate: string,
): Promise<void> {
  await apiFetch<void>(
    `/trainingplans/${planId}/plannedworkouts/${plannedWorkoutId}/schedule`,
    { method: 'PATCH', body: JSON.stringify({ scheduledDate }) },
  )
}
