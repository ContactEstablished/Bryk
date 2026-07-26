import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  commitActivityFile,
  discardActivityFile,
  getWorkoutSource,
  uploadActivityFile,
} from '@/services/activityFiles'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('activityFiles service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('uploadActivityFile posts multipart to /activityfiles with the part named "file"', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ id: 'f1' }))
    const file = new File(['<xml/>'], 'ride.tcx', { type: 'application/xml' })

    await uploadActivityFile(file)

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/activityfiles`)
    expect(init?.method).toBe('POST')
    expect(init?.body).toBeInstanceOf(FormData)
    expect((init?.body as FormData).get('file')).toBe(file)
  })

  it('commitActivityFile posts the plannedWorkoutId body to /activityfiles/{id}/commit', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse({ workoutId: 'w1', plannedWorkoutId: 'pw1', computedLoad: 80 }))

    await commitActivityFile('f1', 'pw1')

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/activityfiles/f1/commit`)
    expect(JSON.parse(String(init?.body))).toEqual({ plannedWorkoutId: 'pw1' })
  })

  it('commitActivityFile sends a null plannedWorkoutId for an unplanned import', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(jsonResponse({ workoutId: 'w1', plannedWorkoutId: null, computedLoad: 80 }))

    await commitActivityFile('f1', null)

    expect(JSON.parse(String(fetchSpy.mock.calls[0][1]?.body))).toEqual({ plannedWorkoutId: null })
  })

  it('discardActivityFile deletes and resolves on 204', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 204 }))

    await expect(discardActivityFile('f1')).resolves.toBeUndefined()

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/activityfiles/f1`)
    expect(init?.method).toBe('DELETE')
  })

  it('getWorkoutSource returns null for a null body without throwing', async () => {
    // 200 + literal null is the normal answer for a manually logged workout.
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(null))

    await expect(getWorkoutSource('w1')).resolves.toBeNull()
  })

  it('getWorkoutSource returns the summary when the workout came from a file', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse({ id: 'f1', fileName: 'ride.tcx', format: 'Tcx', uploadedAt: '2026-07-26T10:00:00Z' }),
    )

    const result = await getWorkoutSource('w1')

    expect(result?.fileName).toBe('ride.tcx')
  })
})
