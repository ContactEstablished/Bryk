import { afterEach, describe, expect, it, vi } from 'vitest'
import { getWellnessRange, getWellnessSummary, putWellness } from '@/services/wellness'
import { ApiError } from '@/services/api'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

// Explicit nulls are part of the contract: PUT replaces the day, so an omitted metric must travel
// as null rather than being dropped from the body.
const request: WellnessEntryRequest = {
  sleepHours: 7.5,
  sleepQuality: 4,
  restingHr: 48,
  weightKg: null,
  soreness: 3,
  hrvMs: null,
  notes: null,
}

const entry: WellnessEntryResponse = { id: 'w1', date: '2026-07-26', ...request }

function emptyMetric(): WellnessMetricSummary {
  return { average: null, priorAverage: null, delta: null, daysWithData: 0 }
}

const summary: WellnessSummaryResponse = {
  to: '2026-07-26',
  from: '2026-07-20',
  priorFrom: '2026-07-13',
  sleepHours: emptyMetric(),
  sleepQuality: emptyMetric(),
  restingHr: emptyMetric(),
  weightKg: emptyMetric(),
  soreness: emptyMetric(),
  hrvMs: emptyMetric(),
  days: [],
  hasAnyEntries: false,
}

describe('wellness service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('putWellness PUTs /api/v1/wellness/{date} with the metric body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(entry))

    const result = await putWellness('2026-07-26', request)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/wellness/2026-07-26`)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual(request)
    expect(result).toEqual(entry)
  })

  it('getWellnessRange builds the from/to query string', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse([entry]))

    const result = await getWellnessRange('2026-07-13', '2026-07-26')

    const [url] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/wellness?from=2026-07-13&to=2026-07-26`)
    expect(result).toEqual([entry])
  })

  it('getWellnessRange returns [] when the body is null', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))

    await expect(getWellnessRange('2026-07-13', '2026-07-26')).resolves.toEqual([])
  })

  it('getWellnessSummary GETs /api/v1/wellness/summary', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(summary))

    const result = await getWellnessSummary()

    const [url] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/wellness/summary`)
    expect(result).toEqual(summary)
  })

  // The card maps the server's field-prefixed messages, so the ApiError must survive the service.
  it('putWellness throws ApiError for a 400', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse(
        { errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'] },
        { status: 400 },
      ),
    )

    const err = await putWellness('2026-07-26', request).catch((e) => e)

    expect(err).toBeInstanceOf(ApiError)
    expect((err as ApiError).status).toBe(400)
    expect((err as ApiError).body).toEqual({
      errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'],
    })
  })
})
