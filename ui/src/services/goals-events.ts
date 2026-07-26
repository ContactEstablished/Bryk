import { apiFetch } from '@/services/api'
import type { EventListItem, GoalListItem } from '@/types/goals'

// Read layer for the /goals page. The write side (POST/PUT/DELETE) already lives in
// services/events.ts and services/goals.ts — these are the GET counterparts only.
// The API scopes every read to the current athlete; no athlete id is sent.

export async function getEvents(upcoming?: boolean): Promise<EventListItem[]> {
  const result = await apiFetch<EventListItem[]>(`/events${upcoming ? '?upcoming=true' : ''}`)
  if (result === null) {
    throw new Error('Unexpected empty response from /events')
  }
  return result
}

export async function getEvent(id: string): Promise<EventListItem> {
  const result = await apiFetch<EventListItem>(`/events/${id}`)
  if (result === null) {
    throw new Error(`Unexpected empty response from /events/${id}`)
  }
  return result
}

export async function getGoalsList(): Promise<GoalListItem[]> {
  const result = await apiFetch<GoalListItem[]>('/goals')
  if (result === null) {
    throw new Error('Unexpected empty response from /goals')
  }
  return result
}
