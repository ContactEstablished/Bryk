import { afterEach, describe, expect, it, vi } from 'vitest'
import { createGoal, updateGoal, deleteGoal } from '@/services/goals'
import type { GoalDto } from '@/types/onboarding'
import type { GoalResponse } from '@/types/profile'

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

const validGoal: GoalDto = {
  type: 'General',
  description: 'Sub 3 hours',
  targetDate: '2026-09-01',
}

describe('goals service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('createGoal POSTs JSON to /goals and returns the created GoalResponse', async () => {
    const created: GoalResponse = { id: 'g1', ...validGoal }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(created, { status: 201 }))

    const result = await createGoal(validGoal)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/goals`)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual(validGoal)
    expect(result).toEqual(created)
  })

  it('updateGoal PUTs JSON to /goals/{id} and returns the updated GoalResponse', async () => {
    const updated: GoalResponse = { id: 'g1', ...validGoal, description: 'Sub 2:55' }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(updated))

    const result = await updateGoal('g1', { ...validGoal, description: 'Sub 2:55' })

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/goals/g1`)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(init?.body as string)).toEqual({ ...validGoal, description: 'Sub 2:55' })
    expect(result).toEqual(updated)
  })

  it('deleteGoal DELETEs /goals/{id} and resolves on 204', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(noContent())

    const result = await deleteGoal('g1')

    expect(result).toBeUndefined()
    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/goals/g1`)
    expect(init?.method).toBe('DELETE')
  })
})
