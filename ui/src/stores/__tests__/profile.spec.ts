import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useProfileStore } from '@/stores/profile'
import type { ProfileGoalsResponse } from '@/types/profile'
import type { EventDto } from '@/types/onboarding'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function noContent(): Response {
  return new Response(null, { status: 204 })
}

const seededGoals: ProfileGoalsResponse = {
  events: [
    {
      id: 'e1',
      name: 'Spring Marathon',
      eventDate: '2026-09-01',
      sport: 'Run',
      triathlonDistance: null,
      customDistanceName: null,
      priority: 'A',
      notes: null,
    },
  ],
  goals: [
    { id: 'g1', type: 'General', description: 'Sub 3 hours', targetDate: '2026-09-01' },
  ],
}

describe('useProfileStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('loadGoals populates goals from /profile/goals', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(seededGoals))

    const store = useProfileStore()
    await store.loadGoals()

    expect(store.goals).toEqual(seededGoals)
    expect(store.loadingGoals).toBe(false)
    expect(store.goalsError).toBeNull()
  })

  it('createEvent POSTs the event then re-fetches the goals list', async () => {
    const newEvent: EventDto = {
      name: 'Fall 10k',
      eventDate: '2026-11-01',
      sport: 'Run',
      triathlonDistance: null,
      customDistanceName: null,
      priority: 'B',
      notes: null,
    }
    const afterCreate: ProfileGoalsResponse = {
      events: [...seededGoals.events, { id: 'e2', ...newEvent }],
      goals: seededGoals.goals,
    }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(jsonResponse({ id: 'e2', ...newEvent }, { status: 201 }))
      .mockResolvedValueOnce(jsonResponse(afterCreate))

    const store = useProfileStore()
    await store.createEvent(newEvent)

    expect(fetchSpy).toHaveBeenCalledTimes(2)
    expect(fetchSpy.mock.calls[0][0]).toBe(`${BASE_URL}/events`)
    expect(fetchSpy.mock.calls[0][1]?.method).toBe('POST')
    expect(fetchSpy.mock.calls[1][0]).toBe(`${BASE_URL}/profile/goals`)
    expect(store.goals?.events).toHaveLength(2)
    expect(store.goals?.events[1].id).toBe('e2')
  })

  it('deleteGoal DELETEs the goal then re-fetches the goals list', async () => {
    const afterDelete: ProfileGoalsResponse = {
      events: seededGoals.events,
      goals: [],
    }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(noContent())
      .mockResolvedValueOnce(jsonResponse(afterDelete))

    const store = useProfileStore()
    await store.deleteGoal('g1')

    expect(fetchSpy).toHaveBeenCalledTimes(2)
    expect(fetchSpy.mock.calls[0][0]).toBe(`${BASE_URL}/goals/g1`)
    expect(fetchSpy.mock.calls[0][1]?.method).toBe('DELETE')
    expect(fetchSpy.mock.calls[1][0]).toBe(`${BASE_URL}/profile/goals`)
    expect(store.goals?.goals).toHaveLength(0)
  })
})
