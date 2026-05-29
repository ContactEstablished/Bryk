import { afterEach, describe, expect, it, vi } from 'vitest'
import { getRequired, getRecommended, getGoals } from '@/services/profile'
import type {
  ProfileRequiredResponse,
  ProfileRecommendedResponse,
  ProfileGoalsResponse,
} from '@/types/profile'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('profile service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('getRequired GETs /profile/required and returns the typed shape', async () => {
    const expected: ProfileRequiredResponse = {
      name: 'Test Athlete',
      gender: 'Other',
      dateOfBirth: '1990-01-01',
      heightCm: 180,
      weightKg: 75,
      yearsTraining: 5,
      typicalWeeklyHours: 10,
      methodology: 'Polarized',
    }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(expected))

    const result = await getRequired()

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/profile/required`)
    expect(init?.method ?? 'GET').toBe('GET')
    expect(result).toEqual(expected)
  })

  it('getRecommended GETs /profile/recommended and returns the typed shape', async () => {
    const expected: ProfileRecommendedResponse = {
      restingHr: 50,
      maxHr: 190,
      sportThresholds: [
        {
          sport: 'Bike',
          isActive: true,
          thresholdValue: 250,
          lt1: 180,
          lt2: 220,
          customZonesJson: null,
        },
      ],
    }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(expected))

    const result = await getRecommended()

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/profile/recommended`)
    expect(init?.method ?? 'GET').toBe('GET')
    expect(result).toEqual(expected)
  })

  it('getGoals GETs /profile/goals and returns the typed shape', async () => {
    const expected: ProfileGoalsResponse = {
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
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(expected))

    const result = await getGoals()

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/profile/goals`)
    expect(init?.method ?? 'GET').toBe('GET')
    expect(result).toEqual(expected)
  })
})
