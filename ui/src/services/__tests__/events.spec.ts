import { afterEach, describe, expect, it, vi } from 'vitest'
import { createEvent, updateEvent, deleteEvent } from '@/services/events'
import type { EventDto } from '@/types/onboarding'
import type { EventResponse } from '@/types/profile'

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

const validEvent: EventDto = {
  name: 'Spring Marathon',
  eventDate: '2026-09-01',
  sport: 'Run',
  triathlonDistance: null,
  customDistanceName: null,
  priority: 'A',
  notes: 'Goal race',
}

describe('events service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('createEvent POSTs JSON to /events and returns the created EventResponse', async () => {
    const created: EventResponse = { id: 'e1', ...validEvent }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(created, { status: 201 }))

    const result = await createEvent(validEvent)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/events`)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual(validEvent)
    expect(result).toEqual(created)
  })

  it('updateEvent PUTs JSON to /events/{id} and returns the updated EventResponse', async () => {
    const updated: EventResponse = { id: 'e1', ...validEvent, name: 'Renamed' }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(updated))

    const result = await updateEvent('e1', { ...validEvent, name: 'Renamed' })

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/events/e1`)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(init?.body as string)).toEqual({ ...validEvent, name: 'Renamed' })
    expect(result).toEqual(updated)
  })

  it('deleteEvent DELETEs /events/{id} and resolves on 204', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(noContent())

    const result = await deleteEvent('e1')

    expect(result).toBeUndefined()
    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/events/e1`)
    expect(init?.method).toBe('DELETE')
  })
})
