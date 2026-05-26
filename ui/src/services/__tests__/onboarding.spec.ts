import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  getStatus,
  submitRequired,
  submitRecommended,
  submitGoals,
} from '@/services/onboarding'
import type {
  OnboardingGoalsRequest,
  OnboardingRecommendedRequest,
  OnboardingRequiredRequest,
  OnboardingStatusResponse,
} from '@/types/onboarding'

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

describe('onboarding service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('getStatus GETs /onboarding/status and returns the typed shape', async () => {
    const expected: OnboardingStatusResponse = {
      requiredComplete: true,
      recommendedComplete: false,
      goalsComplete: false,
    }
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse(expected))

    const result = await getStatus()

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/onboarding/status`)
    // apiFetch passes undefined init for plain GETs, but always layers headers.
    expect(init?.method ?? 'GET').toBe('GET')
    expect(result).toEqual(expected)
  })

  it('submitRequired POSTs JSON to /onboarding/required', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(noContent())
    const payload: OnboardingRequiredRequest = {
      name: 'Test Athlete',
      gender: 'Other',
      dateOfBirth: '1990-01-01',
      heightCm: 180,
      weightKg: 75,
      yearsTraining: 5,
      typicalWeeklyHours: 10,
      methodology: 'Polarized',
    }

    const result = await submitRequired(payload)

    expect(result).toBeUndefined()
    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/onboarding/required`)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual(payload)
  })

  it('submitRecommended POSTs JSON to /onboarding/recommended', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(noContent())
    const payload: OnboardingRecommendedRequest = {
      restingHr: 50,
      maxHr: 190,
      sportThresholds: [],
    }

    await submitRecommended(payload)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/onboarding/recommended`)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual(payload)
  })

  it('submitGoals POSTs JSON to /onboarding/goals', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(noContent())
    const payload: OnboardingGoalsRequest = {
      events: [],
      goals: [],
    }

    await submitGoals(payload)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/onboarding/goals`)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual(payload)
  })
})
