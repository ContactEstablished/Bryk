import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useActivityFilesStore } from '@/stores/activityFiles'
import * as service from '@/services/activityFiles'
import { ApiError } from '@/services/api'
import type { ActivityFileUploadResponse, MatchCandidate } from '@/types/activityFiles'

vi.mock('@/services/activityFiles')

function candidate(o: Partial<MatchCandidate> & { plannedWorkoutId: string }): MatchCandidate {
  return {
    trainingPlanId: 'tp1',
    title: 'Session',
    sport: 'Run',
    scheduledDate: '2026-06-01',
    plannedLoad: 50,
    dayOffset: 0,
    ...o,
  }
}

function preview(candidates: MatchCandidate[]): ActivityFileUploadResponse {
  return {
    id: 'f1',
    fileName: 'run.tcx',
    format: 'Tcx',
    byteSize: 1024,
    parsed: {
      sport: 'Run',
      completedDate: '2026-06-01',
      startTimeUtc: '2026-06-01T06:00:00Z',
      durationSeconds: 600,
      distanceMeters: 2000,
      avgHr: 144,
      maxHr: 160,
      avgPower: null,
      avgPace: 300,
      sampleCount: 5,
    },
    computedLoad: 42,
    zoneSeconds: [{ zoneNumber: 1, seconds: 600 }],
    matchCandidates: candidates,
  }
}

describe('activityFiles store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('upload stores the preview and preselects a single same-day candidate', async () => {
    vi.mocked(service.uploadActivityFile).mockResolvedValue(
      preview([candidate({ plannedWorkoutId: 'pw1', dayOffset: 0 })]),
    )
    const store = useActivityFilesStore()

    await store.upload(new File([''], 'run.tcx'))

    expect(store.preview?.id).toBe('f1')
    expect(store.selectedPlannedWorkoutId).toBe('pw1')
    expect(store.uploading).toBe(false)
  })

  it('upload with two same-day candidates leaves the selection null', async () => {
    vi.mocked(service.uploadActivityFile).mockResolvedValue(
      preview([
        candidate({ plannedWorkoutId: 'pw1', dayOffset: 0 }),
        candidate({ plannedWorkoutId: 'pw2', dayOffset: 0 }),
      ]),
    )
    const store = useActivityFilesStore()

    await store.upload(new File([''], 'run.tcx'))

    expect(store.selectedPlannedWorkoutId).toBeNull()
  })

  it("upload maps an ApiError's first errors[] entry into uploadError and does not throw", async () => {
    vi.mocked(service.uploadActivityFile).mockRejectedValue(
      new ApiError(400, 'Bad Request', { errors: ['File: The .tcx file could not be parsed.'] }),
    )
    const store = useActivityFilesStore()

    await expect(store.upload(new File([''], 'bad.tcx'))).resolves.toBeUndefined()

    expect(store.uploadError).toBe('File: The .tcx file could not be parsed.')
    expect(store.preview).toBeNull()
  })

  it('commit returns the new workoutId and passes the selected plannedWorkoutId', async () => {
    vi.mocked(service.uploadActivityFile).mockResolvedValue(
      preview([candidate({ plannedWorkoutId: 'pw1', dayOffset: 0 })]),
    )
    vi.mocked(service.commitActivityFile).mockResolvedValue({
      workoutId: 'w9',
      plannedWorkoutId: 'pw1',
      computedLoad: 42,
    })
    const store = useActivityFilesStore()
    await store.upload(new File([''], 'run.tcx'))

    const workoutId = await store.commit()

    expect(workoutId).toBe('w9')
    expect(service.commitActivityFile).toHaveBeenCalledWith('f1', 'pw1')
  })

  it('discard clears the preview', async () => {
    vi.mocked(service.uploadActivityFile).mockResolvedValue(preview([]))
    vi.mocked(service.discardActivityFile).mockResolvedValue(undefined)
    const store = useActivityFilesStore()
    await store.upload(new File([''], 'run.tcx'))

    await store.discard()

    expect(service.discardActivityFile).toHaveBeenCalledWith('f1')
    expect(store.preview).toBeNull()
  })

  it('loadSource swallows an error and leaves source null', async () => {
    vi.mocked(service.getWorkoutSource).mockRejectedValue(new Error('boom'))
    const store = useActivityFilesStore()

    await expect(store.loadSource('w1')).resolves.toBeUndefined()

    expect(store.source).toBeNull()
  })
})
