import { afterEach, describe, expect, it, vi } from 'vitest'
import { updatePlan, getWeeklyTargets } from '@/services/training'
import type {
  TrainingPlanResponse,
  TrainingPlanUpdateRequest,
  WeeklyTargetsResponse,
} from '@/types/training'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

const updateRequest: TrainingPlanUpdateRequest = {
  name: 'Renamed Block',
  methodology: 'Polarized',
  startDate: '2026-06-08',
  endDate: '2026-08-03',
  eventId: null,
  buildWeeks: 3,
  recoveryWeeks: 1,
  recoveryWeekPercentage: 60,
}

describe('training service — plan metadata + weekly targets', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('updatePlan PUTs the metadata body to /trainingplans/{id}', async () => {
    const updated: TrainingPlanResponse = {
      id: 'p1',
      ...updateRequest,
      plannedWorkouts: [],
    }
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(updated))

    const result = await updatePlan('p1', updateRequest)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/trainingplans/p1`)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual(updateRequest)
    expect(result).toEqual(updated)
  })

  it('getWeeklyTargets GETs /trainingplans/{id}/weekly-targets', async () => {
    const targets: WeeklyTargetsResponse = {
      planId: 'p1',
      startDate: '2026-06-08',
      endDate: '2026-07-05',
      baseline: 200,
      baselineSource: 'TrailingActual',
      weeks: [
        {
          weekStart: '2026-06-08',
          targetLoad: 200,
          isRecoveryWeek: false,
          isTaperWeek: false,
          plannedLoad: 0,
          actualLoad: 0,
        },
      ],
    }
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(targets))

    const result = await getWeeklyTargets('p1')

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/trainingplans/p1/weekly-targets`)
    expect(result).toEqual(targets)
  })
})
