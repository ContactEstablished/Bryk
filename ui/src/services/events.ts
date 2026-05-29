import { apiFetch } from '@/services/api'
import type { EventDto } from '@/types/onboarding'
import type { EventResponse } from '@/types/profile'

export async function createEvent(data: EventDto): Promise<EventResponse> {
  const result = await apiFetch<EventResponse>('/events', {
    method: 'POST',
    body: JSON.stringify(data),
  })
  if (result === null) {
    throw new Error('Unexpected empty response from POST /events')
  }
  return result
}

export async function updateEvent(
  id: string,
  data: EventDto,
): Promise<EventResponse> {
  const result = await apiFetch<EventResponse>(`/events/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
  if (result === null) {
    throw new Error(`Unexpected empty response from PUT /events/${id}`)
  }
  return result
}

export async function deleteEvent(id: string): Promise<void> {
  await apiFetch<void>(`/events/${id}`, { method: 'DELETE' })
}
