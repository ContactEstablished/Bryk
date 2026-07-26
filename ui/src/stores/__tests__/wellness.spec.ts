import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useWellnessStore } from '@/stores/wellness'
import { getWellnessRange, getWellnessSummary, putWellness } from '@/services/wellness'
import { ApiError } from '@/services/api'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

vi.mock('@/services/wellness', () => ({
  putWellness: vi.fn(),
  getWellnessRange: vi.fn(),
  getWellnessSummary: vi.fn(),
}))

const putWellnessMock = vi.mocked(putWellness)
const getWellnessRangeMock = vi.mocked(getWellnessRange)
const getWellnessSummaryMock = vi.mocked(getWellnessSummary)

// The store's own helper, copied so the expected URL date is exact rather than approximate.
function utcTodayIso(): string {
  const now = new Date()
  return [
    now.getUTCFullYear(),
    String(now.getUTCMonth() + 1).padStart(2, '0'),
    String(now.getUTCDate()).padStart(2, '0'),
  ].join('-')
}

const request: WellnessEntryRequest = {
  sleepHours: 7.5,
  sleepQuality: null,
  restingHr: 48,
  weightKg: null,
  soreness: null,
  hrvMs: null,
  notes: null,
}

const entry: WellnessEntryResponse = { id: 'w1', date: utcTodayIso(), ...request }

function emptyMetric(): WellnessMetricSummary {
  return { average: null, priorAverage: null, delta: null, daysWithData: 0 }
}

const summary: WellnessSummaryResponse = {
  to: utcTodayIso(),
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

describe('wellness store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loadToday assigns the single row returned for today', async () => {
    getWellnessRangeMock.mockResolvedValue([entry])
    const store = useWellnessStore()

    await store.loadToday()

    expect(store.today).toEqual(entry)
    // The single-day read uses the same date for both bounds.
    expect(getWellnessRangeMock).toHaveBeenCalledWith(utcTodayIso(), utcTodayIso())
    expect(store.loadingToday).toBe(false)
  })

  it('loadToday leaves today null when the range comes back empty', async () => {
    getWellnessRangeMock.mockResolvedValue([])
    const store = useWellnessStore()

    await store.loadToday()

    expect(store.today).toBeNull()
    expect(store.error).toBeNull()
  })

  it('loadSummary assigns the summary and clears error', async () => {
    getWellnessSummaryMock.mockResolvedValue(summary)
    const store = useWellnessStore()
    store.error = new Error('stale')

    await store.loadSummary()

    expect(store.summary).toEqual(summary)
    expect(store.error).toBeNull()
    expect(store.loadingSummary).toBe(false)
  })

  it("saveToday PUTs today's date and re-fetches both reads", async () => {
    putWellnessMock.mockResolvedValue(entry)
    getWellnessRangeMock.mockResolvedValue([entry])
    getWellnessSummaryMock.mockResolvedValue(summary)
    const store = useWellnessStore()

    await store.saveToday(request)

    expect(putWellnessMock).toHaveBeenCalledWith(utcTodayIso(), request)
    expect(getWellnessRangeMock).toHaveBeenCalledTimes(1)
    expect(getWellnessSummaryMock).toHaveBeenCalledTimes(1)
    // Both re-fetches happen AFTER the write, so the store ends on server truth.
    expect(putWellnessMock.mock.invocationCallOrder[0]).toBeLessThan(
      getWellnessRangeMock.mock.invocationCallOrder[0],
    )
    expect(putWellnessMock.mock.invocationCallOrder[0]).toBeLessThan(
      getWellnessSummaryMock.mock.invocationCallOrder[0],
    )
    expect(store.saving).toBe(false)
  })

  it('saveToday re-throws an ApiError and clears saving', async () => {
    putWellnessMock.mockRejectedValue(
      new ApiError(400, 'Bad Request', {
        errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'],
      }),
    )
    const store = useWellnessStore()

    await expect(store.saveToday(request)).rejects.toBeInstanceOf(ApiError)

    expect(store.saving).toBe(false)
    expect(getWellnessRangeMock).not.toHaveBeenCalled()
    expect(getWellnessSummaryMock).not.toHaveBeenCalled()
  })
})
