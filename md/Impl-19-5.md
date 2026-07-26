# Impl 19-5 — Build order: upload entry, import review flow, "from file" badge (+ the `api.ts` FormData fix)

**Executor:** the architect-implementer. **Acceptance contract:** `md/Tasks-19-5.md`. **Decision lock:**
ADR-0010 §4 (the "from file" badge is a reverse lookup — there is no `sourceFileId` column on `Workout`
and none is added; ADR-0010 is written by Task 19-1 and referenced here without a code dependency on it,
same pattern Impl-18-4 used for ADR-0009) plus D3/D4 from the Phase-19 ground-facts doc (no `Workout`
entity change, one migration only, `LoadCalculator.cs` frozen — none of that is this task's surface, but
it is why the badge and the load numbers in the review card are display-only reads of server truth).
**Scope:** Frontend only. One verified blocker fix in `ui/src/services/api.ts`, a new service + types +
Pinia store for activity files, three new components under `ui/src/components/import/`, an upload
affordance on `WorkoutsView`, a source badge on `WorkoutDetailView`. No backend change, no migration, no
new npm package, no new route, no new sidebar item. Depends on Task 19-4 (the four `/activityfiles`
endpoints) — **Step 0 verifies they exist before any file is touched.**

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. One commit at the end with the message in `Tasks-19-5.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`.
- Baseline: `dotnet build api/Bryk.sln` green (16 warnings — the design-time NU1903 plus the two
  pre-existing `WorkoutsControllerTests.cs:121,150` nullable warnings; do not fix these). Run
  `dotnet test api/Bryk.sln` once and record the current passing count — it is the **262** baseline plus
  whatever 19-1…19-4 added. This task touches no backend file, so that count must be **byte-identical**
  at Step 16.
  `cd ui; pnpm run build` green. `cd ui; pnpm exec vitest run --no-file-parallelism` green — record
  **252 / 56 files** (the verified Phase-19 baseline); this task must **rise** from it, never fall.
- **Confirm Task 19-4 is actually merged** — this task cannot proceed without it:
  - Confirm `api/Bryk.API/Controllers/ActivityFilesController.cs` and
    `api/Bryk.Application/ActivityFiles/*` (service + DTOs) exist in the tree.
  - With the dev API running (`dotnet run` from `api/Bryk.API`), smoke-check all four endpoints against
    a seeded athlete: `POST /api/v1/activityfiles` (multipart, 201 `ActivityFileUploadResponse`),
    `POST /api/v1/activityfiles/{id}/commit` (201 `ActivityFileCommitResponse`),
    `DELETE /api/v1/activityfiles/{id}` (204), `GET /api/v1/activityfiles/by-workout/{workoutId}`
    (**200 with a `null` body**, not 404, for a workout with no source file). Confirm the exact camelCase
    field names on `ActivityFileUploadResponse`/`ParsedActivity`/`MatchCandidate` (.NET's default JSON
    casing) match `Tasks-19-5.md`'s Acceptance Criteria #2 **before** writing `types/activityFiles.ts` —
    if any field is missing or shaped differently, **STOP**, do not reimplement 19-4 inline, flag the gap
    and wait.
- Re-read `md/Tasks-19-5.md` in full. Open in the editor (read-only unless listed as an edit target
  below): `ui/src/services/api.ts` (all 46 lines), `ui/src/services/training.ts` (the service style to
  mirror — `updatePlan` L50–64, `deleteWorkout` L161), `ui/src/services/__tests__/training.spec.ts:1–50`
  (the fetch-spy harness), `ui/src/stores/training.ts:33–75` (the store shape to mirror),
  `ui/src/views/WorkoutsView.vue` (filter bar L100–142, history list L145–202, local `formatDuration`/
  `formatDistance` L82–93), `ui/src/views/WorkoutDetailView.vue:154–190` (header row + metric strip),
  `ui/src/components/analytics/TimeInZoneSection.vue:68–72,92–108` (**read only** — the badge and bar
  markup to mirror; Task 19-6 owns this file), `ui/src/components/common/MetricTile.vue`,
  `ui/src/components/layout/AppSidebar.vue:37`, `ui/src/router/index.ts:48,53`, `ui/src/lib/format.ts`
  (`formatHm`).
- **Fences to hold for the whole task** (re-check at Step 16's `git diff --stat`):
  - No file under `api/` changes.
  - `ui/src/router/index.ts` and `ui/src/components/layout/AppSidebar.vue` are **not edited** — `/workouts`
    (line 48) and `/workouts/:id` (line 53) already exist; the sidebar's Workouts item (line 37) already
    exists.
  - `ui/src/components/analytics/TimeInZoneSection.vue`, `ui/src/types/analytics.ts`, and
    `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts` are **not edited** — Task 19-6 owns
    all three. The zone-bar markup is duplicated into a new `ZoneHistogramBars.vue`, not shared.
  - `ui/src/types/training.ts`'s `WorkoutResponse` is **not edited** — no `sourceFileId` field. The badge
    is fed entirely by the reverse-lookup endpoint.
  - No new npm package. `File`/`FormData`/`DataTransfer` are browser/jsdom built-ins.

## Step 1 — The blocker fix: `ui/src/services/api.ts` + its regression test

**Edit** `ui/src/services/api.ts`. Replace lines 24–27 (the unconditional header block) with the guard
from `Tasks-19-5.md`'s Acceptance Criteria #1, verbatim:

```ts
  // A multipart body must NOT carry an explicit Content-Type: the browser has to set
  // 'multipart/form-data; boundary=…' itself, and overriding it makes the server unable to
  // locate the parts. Only default the JSON header for non-FormData bodies.
  const headers: HeadersInit =
    init?.body instanceof FormData
      ? { ...init?.headers }
      : { 'Content-Type': 'application/json', ...init?.headers }
```

Nothing else in the file changes: `BASE_URL`, `ApiError`, the 204 → `null` branch, the `!response.ok`
branch, and the final `response.json()` cast are all untouched.

**New file** `ui/src/services/__tests__/api.spec.ts` — the regression test that keeps line 25's behaviour
from returning. Mirror `training.spec.ts`'s fetch-spy harness:

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '@/services/api'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('apiFetch — Content-Type guard', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('sets application/json for a plain object body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ ok: true }))

    await apiFetch('/x', { method: 'POST', body: '{}' })

    const [, init] = fetchSpy.mock.calls[0]
    const headers = init?.headers as Record<string, string>
    expect(headers['Content-Type']).toBe('application/json')
  })

  it('omits Content-Type entirely for a FormData body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ ok: true }))

    await apiFetch('/x', { method: 'POST', body: new FormData() })

    const [, init] = fetchSpy.mock.calls[0]
    const headers = init?.headers as Record<string, string>
    expect('Content-Type' in headers).toBe(false)
  })

  it('still honours an explicitly passed header alongside FormData', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ ok: true }))

    await apiFetch('/x', {
      method: 'POST',
      body: new FormData(),
      headers: { 'X-Custom': 'yes' },
    })

    const [, init] = fetchSpy.mock.calls[0]
    const headers = init?.headers as Record<string, string>
    expect(headers['X-Custom']).toBe('yes')
    expect('Content-Type' in headers).toBe(false)
  })

  it('returns null for a 204', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))

    const result = await apiFetch('/x', { method: 'DELETE' })

    expect(result).toBeNull()
  })

  it('throws ApiError with the parsed body for a 400', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ errors: ['bad'] }, { status: 400 }))

    const err = await apiFetch('/x').catch((e) => e)

    expect(err).toBeInstanceOf(ApiError)
    expect((err as ApiError).status).toBe(400)
    expect((err as ApiError).body).toEqual({ errors: ['bad'] })
  })
})
```

**Verify:**
```
cd ui; pnpm exec vitest run ui/src/services/__tests__/api.spec.ts --no-file-parallelism
```
All 5 cases green, then:
```
cd ui; pnpm exec vitest run ui/src/services/__tests__ --no-file-parallelism
```
Every existing file under `services/__tests__` (`training.spec.ts`, `events.spec.ts`, etc.) still passes
unchanged — the guard must not alter behaviour for any caller that doesn't pass `FormData`.

## Step 2 — Types: `ui/src/types/activityFiles.ts` (new)

Mirrors 19-4's DTOs exactly (Acceptance Criteria #2 of `Tasks-19-5.md`). `Sport` values are the
serialized enum names, so reuse `PlannedSport` from `@/types/training`; dates are `'YYYY-MM-DD'` strings.

```ts
import type { PlannedSport } from '@/types/training'

export type ActivityFileFormat = 'Fit' | 'Tcx' | 'Gpx'

export interface ZoneHistogramEntry {
  zoneNumber: number
  seconds: number
}

export interface ParsedActivity {
  sport: PlannedSport
  completedDate: string
  startTimeUtc: string
  durationSeconds: number | null
  distanceMeters: number | null
  avgHr: number | null
  maxHr: number | null
  avgPower: number | null
  avgPace: number | null
  sampleCount: number
}

export interface MatchCandidate {
  plannedWorkoutId: string
  trainingPlanId: string
  title: string
  sport: PlannedSport
  scheduledDate: string
  plannedLoad: number | null
  dayOffset: number
}

export interface ActivityFileUploadResponse {
  id: string
  fileName: string
  format: ActivityFileFormat
  byteSize: number
  parsed: ParsedActivity
  computedLoad: number | null
  zoneSeconds: ZoneHistogramEntry[]
  matchCandidates: MatchCandidate[]
}

export interface ActivityFileCommitResponse {
  workoutId: string
  plannedWorkoutId: string | null
  computedLoad: number | null
}

export interface ActivityFileSource {
  id: string
  fileName: string
  format: ActivityFileFormat
  uploadedAt: string
}
```

**Verify:** `pnpm run build` green (type-checks; no consumers yet).

## Step 3 — Service: `ui/src/services/activityFiles.ts` (new)

Mirrors `training.ts`'s one-function-per-endpoint style, with two deliberate divergences noted inline:
`uploadActivityFile` sets no headers at all (the whole point of Step 1's fix), and `getWorkoutSource`
does **not** throw on a `null` body (a null body is the normal answer for a manually-logged workout).

```ts
import { apiFetch } from '@/services/api'
import type {
  ActivityFileUploadResponse,
  ActivityFileCommitResponse,
  ActivityFileSource,
} from '@/types/activityFiles'

// Mirrors the server's ~25 MB cap (Task 19-4) so the UI can refuse an oversized file before
// spending an upload; the server remains the authority.
export const ACCEPTED_EXTENSIONS = '.fit,.tcx,.gpx'
export const MAX_UPLOAD_BYTES = 25 * 1024 * 1024

// Multipart upload. The FormData body carries no explicit headers — apiFetch's FormData guard
// (Step 1) lets the browser supply the boundary itself. Part name 'file' matches the controller's
// `IFormFile? file` parameter.
export async function uploadActivityFile(file: File): Promise<ActivityFileUploadResponse> {
  const form = new FormData()
  form.append('file', file)
  const result = await apiFetch<ActivityFileUploadResponse>('/activityfiles', {
    method: 'POST',
    body: form,
  })
  if (result === null) {
    throw new Error('Unexpected empty response from POST /activityfiles')
  }
  return result
}

export async function commitActivityFile(
  id: string,
  plannedWorkoutId: string | null,
): Promise<ActivityFileCommitResponse> {
  const result = await apiFetch<ActivityFileCommitResponse>(`/activityfiles/${id}/commit`, {
    method: 'POST',
    body: JSON.stringify({ plannedWorkoutId }),
  })
  if (result === null) {
    throw new Error('Unexpected empty response from POST /activityfiles/{id}/commit')
  }
  return result
}

// Hard delete (204, no body) — an abandoned import leaves nothing behind.
export async function discardActivityFile(id: string): Promise<void> {
  await apiFetch<void>(`/activityfiles/${id}`, { method: 'DELETE' })
}

// Reverse lookup for the "from file" badge (ADR-0010 §4 — there is no column on Workout). A null
// body is the normal, 200 answer for a manually-logged workout: unlike every other function in this
// file, this one does NOT throw on a null result.
export async function getWorkoutSource(workoutId: string): Promise<ActivityFileSource | null> {
  return await apiFetch<ActivityFileSource | null>(`/activityfiles/by-workout/${workoutId}`)
}
```

**Verify:** `pnpm run build` green.

## Step 4 — Service spec: `ui/src/services/__tests__/activityFiles.spec.ts` (new)

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  uploadActivityFile,
  commitActivityFile,
  discardActivityFile,
  getWorkoutSource,
} from '@/services/activityFiles'
import type { ActivityFileUploadResponse, ActivityFileCommitResponse } from '@/types/activityFiles'

const BASE_URL = '/api/v1'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function uploadResponse(): ActivityFileUploadResponse {
  return {
    id: 'af1',
    fileName: 'ride.fit',
    format: 'Fit',
    byteSize: 5,
    parsed: {
      sport: 'Bike',
      completedDate: '2026-07-20',
      startTimeUtc: '2026-07-20T10:00:00Z',
      durationSeconds: 3600,
      distanceMeters: 30000,
      avgHr: 150,
      maxHr: 175,
      avgPower: 210,
      avgPace: null,
      sampleCount: 500,
    },
    computedLoad: 80,
    zoneSeconds: [],
    matchCandidates: [],
  }
}

describe('activityFiles service', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('uploadActivityFile posts multipart to /activityfiles with the part named "file"', async () => {
    const file = new File(['bytes'], 'ride.fit')
    const response = uploadResponse()
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(response, { status: 201 }))

    const result = await uploadActivityFile(file)

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/activityfiles`)
    expect(init?.method).toBe('POST')
    expect(init?.body).toBeInstanceOf(FormData)
    expect((init?.body as FormData).get('file')).toBe(file)
    expect(result).toEqual(response)
  })

  it.each([['pw1'], [null]])(
    'commitActivityFile posts the plannedWorkoutId body to /activityfiles/{id}/commit (%s)',
    async (plannedWorkoutId) => {
      const response: ActivityFileCommitResponse = { workoutId: 'w1', plannedWorkoutId, computedLoad: 80 }
      const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(response, { status: 201 }))

      const result = await commitActivityFile('af1', plannedWorkoutId)

      const [url, init] = fetchSpy.mock.calls[0]
      expect(url).toBe(`${BASE_URL}/activityfiles/af1/commit`)
      expect(init?.method).toBe('POST')
      expect(JSON.parse(String(init?.body))).toEqual({ plannedWorkoutId })
      expect(result).toEqual(response)
    },
  )

  it('discardActivityFile deletes and resolves on 204', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))

    const result = await discardActivityFile('af1')

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe(`${BASE_URL}/activityfiles/af1`)
    expect(init?.method).toBe('DELETE')
    expect(result).toBeUndefined()
  })

  it('getWorkoutSource returns null for a null body', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse(null))

    const result = await getWorkoutSource('w1')

    expect(result).toBeNull()
  })
})
```

**Verify:** `pnpm exec vitest run ui/src/services/__tests__/activityFiles.spec.ts --no-file-parallelism`
— all 5 cases green.

## Step 5 — Store: `ui/src/stores/activityFiles.ts` (new)

A new store, not an addition to `stores/training.ts`. Follows the `defineStore('…', () => { … })` shape
from `stores/training.ts:33–75`: `ref` state, one `loading*`/`*Error` pair per async surface, re-throwing
writes only where the caller needs to map an error onto a form (this store never does — `upload`/`commit`
swallow into their own error refs so the drop zone / review card can render them directly).

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  uploadActivityFile,
  commitActivityFile,
  discardActivityFile,
  getWorkoutSource,
} from '@/services/activityFiles'
import type { ActivityFileUploadResponse, ActivityFileSource } from '@/types/activityFiles'

interface ApiValidationBody {
  errors?: unknown
}

export const useActivityFilesStore = defineStore('activityFiles', () => {
  const preview = ref<ActivityFileUploadResponse | null>(null)
  const uploading = ref(false)
  const uploadError = ref<string | null>(null)
  const committing = ref(false)
  const commitError = ref<string | null>(null)
  const selectedPlannedWorkoutId = ref<string | null>(null)
  const source = ref<ActivityFileSource | null>(null)

  // Uploads and parses a file (Task 19-4's two-step API). Preselects the sole same-day candidate
  // (dayOffset === 0) when there's exactly one; leaves the selection null otherwise (including the
  // no-candidates case, where MatchCandidateList's "No planned workout" option is already selected
  // by null). Does NOT re-throw — the drop zone renders uploadError, sharing the message slot with
  // the client-side pre-checks (WorkoutsView, Step 12).
  async function upload(file: File) {
    uploadError.value = null
    commitError.value = null
    uploading.value = true
    try {
      const result = await uploadActivityFile(file)
      preview.value = result
      const sameDay = result.matchCandidates.filter((c) => c.dayOffset === 0)
      selectedPlannedWorkoutId.value = sameDay.length === 1 ? sameDay[0].plannedWorkoutId : null
    } catch (e) {
      const body = e instanceof ApiError ? (e.body as ApiValidationBody | null) : null
      const first = Array.isArray(body?.errors) ? body?.errors[0] : undefined
      uploadError.value = typeof first === 'string' ? first : "Couldn't read that file."
    } finally {
      uploading.value = false
    }
  }

  // Commits the preview against the selected planned workout (or none, for an unplanned import).
  // Returns the new workoutId on success so the caller can route to it; null on failure (commitError
  // is set for ImportReviewCard's destructive-style banner).
  async function commit(): Promise<string | null> {
    commitError.value = null
    committing.value = true
    try {
      const result = await commitActivityFile(preview.value!.id, selectedPlannedWorkoutId.value)
      return result.workoutId
    } catch (e) {
      commitError.value =
        e instanceof ApiError
          ? `Couldn't save: ${e.statusText} (${e.status})`
          : "Couldn't save — please try again."
      return null
    } finally {
      committing.value = false
    }
  }

  // Deletes the stored file so an abandoned import leaves nothing behind, then clears the preview.
  async function discard() {
    await discardActivityFile(preview.value!.id)
    preview.value = null
  }

  function reset() {
    preview.value = null
    uploadError.value = null
    commitError.value = null
    selectedPlannedWorkoutId.value = null
  }

  // Reverse lookup for the "from file" badge. Swallows errors into source = null — a missing badge
  // must never break the workout detail page.
  async function loadSource(workoutId: string) {
    try {
      source.value = await getWorkoutSource(workoutId)
    } catch {
      source.value = null
    }
  }

  return {
    preview,
    uploading,
    uploadError,
    committing,
    commitError,
    selectedPlannedWorkoutId,
    source,
    upload,
    commit,
    discard,
    reset,
    loadSource,
  }
})
```

`commit`/`discard` use `preview.value!` deliberately — both are only ever invoked from `ImportReviewCard`,
which only renders while `preview` is set (mirrors `Tasks-19-5.md`'s own `preview!.id` wording).

**Verify:** `pnpm run build` green.

## Step 6 — Store spec: `ui/src/stores/__tests__/activityFiles.spec.ts` (new)

Mock the service module with the factory-style `vi.mock` the repo already uses for store specs (see
`stores/__tests__/goals.spec.ts:10–25`), not `vi.mock('@/services/activityFiles')` with automock.

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useActivityFilesStore } from '@/stores/activityFiles'
import {
  uploadActivityFile,
  commitActivityFile,
  discardActivityFile,
  getWorkoutSource,
} from '@/services/activityFiles'
import { ApiError } from '@/services/api'
import type { ActivityFileUploadResponse, MatchCandidate } from '@/types/activityFiles'

vi.mock('@/services/activityFiles', () => ({
  uploadActivityFile: vi.fn(),
  commitActivityFile: vi.fn(),
  discardActivityFile: vi.fn(),
  getWorkoutSource: vi.fn(),
}))

const uploadActivityFileMock = vi.mocked(uploadActivityFile)
const commitActivityFileMock = vi.mocked(commitActivityFile)
const discardActivityFileMock = vi.mocked(discardActivityFile)
const getWorkoutSourceMock = vi.mocked(getWorkoutSource)

function candidate(o: Partial<MatchCandidate> & { plannedWorkoutId: string }): MatchCandidate {
  return {
    trainingPlanId: 'tp1',
    title: 'Threshold ride',
    sport: 'Bike',
    scheduledDate: '2026-07-20',
    plannedLoad: 80,
    dayOffset: 0,
    ...o,
  }
}

function upload(overrides: Partial<ActivityFileUploadResponse> = {}): ActivityFileUploadResponse {
  return {
    id: 'af1',
    fileName: 'ride.fit',
    format: 'Fit',
    byteSize: 100,
    parsed: {
      sport: 'Bike',
      completedDate: '2026-07-20',
      startTimeUtc: '2026-07-20T10:00:00Z',
      durationSeconds: 3600,
      distanceMeters: 30000,
      avgHr: 150,
      maxHr: 175,
      avgPower: 210,
      avgPace: null,
      sampleCount: 500,
    },
    computedLoad: 80,
    zoneSeconds: [],
    matchCandidates: [],
    ...overrides,
  }
}

describe('activityFiles store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('upload stores the preview and preselects a single same-day candidate', async () => {
    const response = upload({ matchCandidates: [candidate({ plannedWorkoutId: 'pw1', dayOffset: 0 })] })
    uploadActivityFileMock.mockResolvedValue(response)
    const store = useActivityFilesStore()

    await store.upload(new File(['x'], 'ride.fit'))

    expect(store.preview).toEqual(response)
    expect(store.selectedPlannedWorkoutId).toBe('pw1')
  })

  it('upload with two candidates leaves the selection null', async () => {
    const response = upload({
      matchCandidates: [
        candidate({ plannedWorkoutId: 'pw1', dayOffset: 0 }),
        candidate({ plannedWorkoutId: 'pw2', dayOffset: 0 }),
      ],
    })
    uploadActivityFileMock.mockResolvedValue(response)
    const store = useActivityFilesStore()

    await store.upload(new File(['x'], 'ride.fit'))

    expect(store.selectedPlannedWorkoutId).toBeNull()
  })

  it("upload maps an ApiError's first errors[] entry into uploadError and does not throw", async () => {
    uploadActivityFileMock.mockRejectedValue(
      new ApiError(400, 'Bad Request', { errors: ['Unsupported file format.'] }),
    )
    const store = useActivityFilesStore()

    await expect(store.upload(new File(['x'], 'ride.txt'))).resolves.toBeUndefined()

    expect(store.uploadError).toBe('Unsupported file format.')
    expect(store.preview).toBeNull()
  })

  it('commit returns the new workoutId and passes the selected plannedWorkoutId', async () => {
    uploadActivityFileMock.mockResolvedValue(upload())
    const store = useActivityFilesStore()
    await store.upload(new File(['x'], 'ride.fit'))
    store.selectedPlannedWorkoutId = 'pw1'
    commitActivityFileMock.mockResolvedValue({ workoutId: 'w1', plannedWorkoutId: 'pw1', computedLoad: 80 })

    const result = await store.commit()

    expect(result).toBe('w1')
    expect(commitActivityFileMock).toHaveBeenCalledWith('af1', 'pw1')
  })

  it('discard clears the preview', async () => {
    uploadActivityFileMock.mockResolvedValue(upload())
    const store = useActivityFilesStore()
    await store.upload(new File(['x'], 'ride.fit'))
    discardActivityFileMock.mockResolvedValue(undefined)

    await store.discard()

    expect(store.preview).toBeNull()
  })

  it('loadSource swallows an error and leaves source null', async () => {
    getWorkoutSourceMock.mockRejectedValue(new Error('network'))
    const store = useActivityFilesStore()

    await store.loadSource('w1')

    expect(store.source).toBeNull()
  })
})
```

**Verify:** `pnpm exec vitest run ui/src/stores/__tests__/activityFiles.spec.ts --no-file-parallelism`
— all 6 cases green.

## Step 7 — `ui/src/components/import/ZoneHistogramBars.vue` (new)

Props `{ zones: ZoneHistogramEntry[] }`. Duplicates `TimeInZoneSection.vue:92–108`'s stacked-bar +
legend markup rather than sharing it — extracting a component would mean editing that file, which Task
19-6 owns. Record the duplication as tech debt in the phase handoff, not here.

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { formatHm } from '@/lib/format'
import type { ZoneHistogramEntry } from '@/types/activityFiles'

const props = defineProps<{ zones: ZoneHistogramEntry[] }>()

const total = computed(() => props.zones.reduce((sum, z) => sum + z.seconds, 0))

const segments = computed(() =>
  props.zones
    .filter((z) => z.seconds > 0)
    .map((z) => ({
      key: `z${z.zoneNumber}`,
      label: `Z${z.zoneNumber}`,
      seconds: z.seconds,
      color: `var(--chart-${Math.min(z.zoneNumber, 5)})`,
    })),
)

const pct = (seconds: number) => `${(seconds / total.value) * 100}%`
</script>

<template>
  <div v-if="total > 0" class="flex flex-col gap-2">
    <div class="flex h-5 w-full overflow-hidden rounded-md">
      <div
        v-for="seg in segments"
        :key="seg.key"
        class="h-full"
        :style="{ width: pct(seg.seconds), background: seg.color }"
        :title="`${seg.label} · ${formatHm(seg.seconds)}`"
      />
    </div>
    <div class="flex flex-wrap gap-x-4 gap-y-1 font-mono text-[11px] text-muted-foreground">
      <span v-for="seg in segments" :key="seg.key" class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" :style="{ background: seg.color }" />
        {{ seg.label }} · {{ formatHm(seg.seconds) }}
      </span>
    </div>
  </div>
</template>
```

The `v-if="total > 0"` root means the component renders nothing (not even an empty shell) when every
bucket is 0, per `Tasks-19-5.md`.

**Verify:** `pnpm run build` green. No dedicated spec file for this component — its rendering is exercised
through `ImportReviewCard.spec.ts` (Step 11), matching `Tasks-19-5.md`'s test-file list, which does not
name a `ZoneHistogramBars.spec.ts`.

## Step 8 — `ui/src/components/import/MatchCandidateList.vue` (new)

Props `{ candidates: MatchCandidate[]; modelValue: string | null }`, emits
`{ 'update:modelValue': [value: string | null] }`. A radio list plus an always-present "No planned
workout" option.

```vue
<script setup lang="ts">
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import type { MatchCandidate } from '@/types/activityFiles'

defineProps<{ candidates: MatchCandidate[]; modelValue: string | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: string | null] }>()

// dayOffset is a signed integer; JS's own negative-number stringification ("-1") is used rather
// than a typographic minus, to keep the label and the value it's derived from in the same alphabet.
function dayLabel(offset: number): string {
  if (offset === 0) return 'Same day'
  return offset > 0 ? `+${offset} day` : `${offset} day`
}

function select(value: string | null) {
  emit('update:modelValue', value)
}
</script>

<template>
  <fieldset class="flex flex-col gap-2">
    <label
      v-for="c in candidates"
      :key="c.plannedWorkoutId"
      class="flex items-center gap-3 rounded-md border border-border p-3 text-sm"
    >
      <input
        type="radio"
        name="match-candidate"
        :checked="modelValue === c.plannedWorkoutId"
        @change="select(c.plannedWorkoutId)"
      />
      <span class="flex min-w-0 flex-1 flex-wrap items-center gap-2">
        <TypePill :kind="sportToPillKind(c.sport)">{{ c.sport }}</TypePill>
        <span class="truncate">{{ c.title }}</span>
        <span class="font-mono text-[11px] text-muted-foreground">{{ c.scheduledDate }}</span>
        <span v-if="c.plannedLoad != null" class="font-mono text-[11px] text-primary-hi">
          {{ c.plannedLoad }} TSS
        </span>
        <span
          class="ml-auto rounded border border-border px-1.5 py-px font-mono text-[10px] uppercase tracking-[0.08em] text-muted-foreground"
        >
          {{ dayLabel(c.dayOffset) }}
        </span>
      </span>
    </label>

    <label class="flex items-center gap-3 rounded-md border border-border p-3 text-sm">
      <input type="radio" name="match-candidate" :checked="modelValue === null" @change="select(null)" />
      No planned workout
    </label>

    <p v-if="candidates.length === 0" class="text-sm text-muted-foreground">
      No planned session within a day of this file.
    </p>
  </fieldset>
</template>
```

**Verify:** `pnpm run build` green.

## Step 9 — `ui/src/components/import/__tests__/MatchCandidateList.spec.ts` (new)

```ts
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import MatchCandidateList from '@/components/import/MatchCandidateList.vue'
import type { MatchCandidate } from '@/types/activityFiles'

function candidate(o: Partial<MatchCandidate> & { plannedWorkoutId: string }): MatchCandidate {
  return {
    trainingPlanId: 'tp1',
    title: 'Threshold ride',
    sport: 'Bike',
    scheduledDate: '2026-07-20',
    plannedLoad: 80,
    dayOffset: 0,
    ...o,
  }
}

describe('MatchCandidateList', () => {
  it('renders one radio per candidate plus the "No planned workout" option', () => {
    const wrapper = mount(MatchCandidateList, {
      props: {
        candidates: [candidate({ plannedWorkoutId: 'pw1' }), candidate({ plannedWorkoutId: 'pw2' })],
        modelValue: null,
      },
    })

    expect(wrapper.findAll('input[type="radio"]')).toHaveLength(3)
    expect(wrapper.text()).toContain('No planned workout')
  })

  it('labels dayOffset as Same day / -1 day / +1 day', () => {
    const wrapper = mount(MatchCandidateList, {
      props: {
        candidates: [
          candidate({ plannedWorkoutId: 'pw1', dayOffset: 0 }),
          candidate({ plannedWorkoutId: 'pw2', dayOffset: -1 }),
          candidate({ plannedWorkoutId: 'pw3', dayOffset: 1 }),
        ],
        modelValue: null,
      },
    })

    expect(wrapper.text()).toContain('Same day')
    expect(wrapper.text()).toContain('-1 day')
    expect(wrapper.text()).toContain('+1 day')
  })

  it('emits update:modelValue with the candidate id on select, and null for the no-match option', async () => {
    const wrapper = mount(MatchCandidateList, {
      props: { candidates: [candidate({ plannedWorkoutId: 'pw1' })], modelValue: null },
    })

    const radios = wrapper.findAll('input[type="radio"]')
    await radios[0].trigger('change')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['pw1'])

    await radios[1].trigger('change')
    expect(wrapper.emitted('update:modelValue')?.[1]).toEqual([null])
  })

  it('renders only the no-match option with a hint when candidates is empty', () => {
    const wrapper = mount(MatchCandidateList, { props: { candidates: [], modelValue: null } })

    expect(wrapper.findAll('input[type="radio"]')).toHaveLength(1)
    expect(wrapper.text()).toContain('No planned session within a day of this file.')
  })
})
```

**Verify:**
`pnpm exec vitest run ui/src/components/import/__tests__/MatchCandidateList.spec.ts --no-file-parallelism`
— all 4 cases green.

## Step 10 — `ui/src/components/import/ImportReviewCard.vue` (new)

No props (reads the store directly); emits `{ committed: [workoutId: string]; cancelled: [] }`. The
`formatDuration`/`formatDistance` helpers are copied **verbatim** from `WorkoutsView.vue:82–93` — do not
import them from `@/lib/format.ts` (whose `formatDistance` rounds differently) and do not extract a
shared module in this task, per `Tasks-19-5.md`.

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Button } from '@/components/ui/button'
import MetricTile from '@/components/common/MetricTile.vue'
import ZoneHistogramBars from '@/components/import/ZoneHistogramBars.vue'
import MatchCandidateList from '@/components/import/MatchCandidateList.vue'
import { useActivityFilesStore } from '@/stores/activityFiles'

const emit = defineEmits<{ committed: [workoutId: string]; cancelled: [] }>()

const store = useActivityFilesStore()
// Only rendered by WorkoutsView while store.preview is set (Step 12) — the non-null assertion
// mirrors the store's own preview! usage (Step 5).
const preview = computed(() => store.preview!)

// Copied from WorkoutsView.vue:82-93 on purpose — see Tasks-19-5.md's Acceptance Criteria #5.
function formatDuration(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = Math.floor(totalSeconds % 60)
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`
}

function formatDistance(meters: number): string {
  return `${(meters / 1000).toFixed(1)} km`
}

function formatBytes(bytes: number): string {
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

async function onConfirm() {
  const workoutId = await store.commit()
  if (workoutId) emit('committed', workoutId)
}

async function onDiscard() {
  await store.discard()
  emit('cancelled')
}
</script>

<template>
  <div class="card-surface p-6">
    <div>
      <h2 class="text-sm font-semibold">{{ preview.fileName }}</h2>
      <p class="font-mono text-[11px] text-muted-foreground">
        {{ preview.format }} · {{ formatBytes(preview.byteSize) }}
      </p>
    </div>

    <div class="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
      <MetricTile label="Load" :value="preview.computedLoad" unit="TSS" />
      <MetricTile
        label="Duration"
        :value="preview.parsed.durationSeconds != null ? formatDuration(preview.parsed.durationSeconds) : null"
      />
      <MetricTile
        label="Distance"
        :value="preview.parsed.distanceMeters != null ? formatDistance(preview.parsed.distanceMeters) : null"
      />
      <MetricTile label="Avg HR" :value="preview.parsed.avgHr" unit="bpm" />
    </div>

    <div class="mt-4">
      <ZoneHistogramBars :zones="preview.zoneSeconds" />
    </div>

    <div class="mt-4">
      <h3 class="eyebrow mb-2">Match to a planned session</h3>
      <MatchCandidateList
        :candidates="preview.matchCandidates"
        :model-value="store.selectedPlannedWorkoutId"
        @update:model-value="store.selectedPlannedWorkoutId = $event"
      />
    </div>

    <p v-if="store.commitError" class="mt-4 rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
      {{ store.commitError }}
    </p>

    <div class="mt-4 flex items-center justify-end gap-3">
      <Button type="button" variant="ghost" size="sm" :disabled="store.committing" @click="onDiscard">
        Discard
      </Button>
      <Button type="button" variant="outline" size="sm" :disabled="store.committing" @click="onConfirm">
        Confirm
      </Button>
    </div>
  </div>
</template>
```

**Verify:** `pnpm run build` green.

## Step 11 — `ui/src/components/import/__tests__/ImportReviewCard.spec.ts` (new)

Mount with `createTestingPinia({ createSpy: vi.fn, initialState: { activityFiles: { preview, ... } } })`,
same pattern `PeriodizationPanel.spec.ts` uses for store-backed components. `useCountUp`'s reduced-motion
stub keeps `MetricTile`'s numeric text synchronous — no `flushPromises` needed for the render assertions.

```ts
import { describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ImportReviewCard from '@/components/import/ImportReviewCard.vue'
import { useActivityFilesStore } from '@/stores/activityFiles'
import type { ActivityFileUploadResponse } from '@/types/activityFiles'

const preview: ActivityFileUploadResponse = {
  id: 'af1',
  fileName: 'ride.fit',
  format: 'Fit',
  byteSize: 2_500_000,
  parsed: {
    sport: 'Bike',
    completedDate: '2026-07-20',
    startTimeUtc: '2026-07-20T10:00:00Z',
    durationSeconds: 3600,
    distanceMeters: 30000,
    avgHr: 150,
    maxHr: 175,
    avgPower: 210,
    avgPace: null,
    sampleCount: 500,
  },
  computedLoad: 80,
  zoneSeconds: [],
  matchCandidates: [],
}

function mountCard(state: Record<string, unknown> = {}) {
  const wrapper = mount(ImportReviewCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: vi.fn,
          initialState: { activityFiles: { preview, selectedPlannedWorkoutId: null, ...state } },
        }),
      ],
    },
    attachTo: document.body,
  })
  return { wrapper, store: useActivityFilesStore() }
}

describe('ImportReviewCard', () => {
  it('renders load, duration, distance and avg HR from the preview', () => {
    const { wrapper } = mountCard()

    expect(wrapper.text()).toContain('80')
    expect(wrapper.text()).toContain('TSS')
    expect(wrapper.text()).toContain('1:00:00')
    expect(wrapper.text()).toContain('30.0 km')
    expect(wrapper.text()).toContain('150')
  })

  it('Confirm emits committed with the workout id returned by the store', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.commit).mockResolvedValue('w1')

    const confirmBtn = wrapper.findAll('button').find((b) => b.text() === 'Confirm')
    await confirmBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('committed')).toEqual([['w1']])
  })

  it('Discard emits cancelled', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.discard).mockResolvedValue(undefined)

    const discardBtn = wrapper.findAll('button').find((b) => b.text() === 'Discard')
    await discardBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('cancelled')).toEqual([[]])
  })

  it('disables both buttons while committing', () => {
    const { wrapper } = mountCard({ committing: true })

    const confirmBtn = wrapper.findAll('button').find((b) => b.text() === 'Confirm')
    const discardBtn = wrapper.findAll('button').find((b) => b.text() === 'Discard')
    expect(confirmBtn!.attributes('disabled')).toBeDefined()
    expect(discardBtn!.attributes('disabled')).toBeDefined()
  })

  it('renders commitError when the store has one', () => {
    const { wrapper } = mountCard({ commitError: "Couldn't save: Bad Request (400)" })

    expect(wrapper.text()).toContain("Couldn't save: Bad Request (400)")
  })
})
```

**Verify:**
`pnpm exec vitest run ui/src/components/import/__tests__/ImportReviewCard.spec.ts --no-file-parallelism`
— all 5 cases green.

## Step 12 — `ui/src/views/WorkoutsView.vue` wiring (edit — additive)

**Edit.** Add imports, the drop-zone state/handlers, and the drop-zone markup above the filter bar. Do
**not** touch the existing filter bar, history list, `apply()`, `selectSport()`, `clearFilters()`, or the
local `formatDuration`/`formatDistance`/`formatDay` helpers.

Script additions (after the existing imports):

```ts
import { useRouter } from 'vue-router'
import ImportReviewCard from '@/components/import/ImportReviewCard.vue'
import { useActivityFilesStore } from '@/stores/activityFiles'
import { ACCEPTED_EXTENSIONS, MAX_UPLOAD_BYTES } from '@/services/activityFiles'
```

After `const store = useTrainingStore()`:

```ts
const router = useRouter()
const activityFilesStore = useActivityFilesStore()

const fileInput = ref<HTMLInputElement | null>(null)
const dragging = ref(false)

function openFilePicker() {
  fileInput.value?.click()
}

// Client-side pre-checks only — the server remains the authority (Task 19-4's size cap + magic-byte
// sniffing). Setting uploadError directly (a ref returned from a Pinia setup store, so external
// assignment writes straight through) avoids a wasted upload for an obviously-bad file.
function handleFile(file: File) {
  const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase()
  if (!ACCEPTED_EXTENSIONS.split(',').includes(ext)) {
    activityFilesStore.uploadError = `Unsupported file type: ${ext || file.name}`
    return
  }
  if (file.size > MAX_UPLOAD_BYTES) {
    activityFilesStore.uploadError = 'File is too large — the limit is 25 MB.'
    return
  }
  void activityFilesStore.upload(file)
}

function onFileInputChange(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) handleFile(file)
}

function onDrop(e: DragEvent) {
  dragging.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file) handleFile(file)
}

function onCommitted(workoutId: string) {
  void router.push(`/workouts/${workoutId}`)
}

function onCancelled() {
  activityFilesStore.reset()
  apply()
}
```

Template — insert immediately above the `<!-- Filter bar -->` comment:

```html
<div
  v-if="!activityFilesStore.preview"
  class="card-surface border-dashed p-5"
  :class="dragging ? 'border-primary' : ''"
  @dragover.prevent="dragging = true"
  @dragleave.prevent="dragging = false"
  @drop.prevent="onDrop"
>
  <div class="flex flex-wrap items-center justify-between gap-3">
    <div>
      <p class="text-sm font-medium">Drop a .fit, .tcx or .gpx file to import it</p>
      <p class="mt-1 text-[12px] text-muted-foreground">or choose a file from your device</p>
    </div>
    <Button
      type="button"
      variant="outline"
      size="sm"
      :disabled="activityFilesStore.uploading"
      @click="openFilePicker"
    >
      {{ activityFilesStore.uploading ? 'Parsing…' : 'Import file' }}
    </Button>
    <input
      ref="fileInput"
      type="file"
      :accept="ACCEPTED_EXTENSIONS"
      class="sr-only"
      @change="onFileInputChange"
    />
  </div>
  <p
    v-if="activityFilesStore.uploadError"
    class="mt-3 rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive"
  >
    {{ activityFilesStore.uploadError }}
  </p>
</div>

<ImportReviewCard v-else @committed="onCommitted" @cancelled="onCancelled" />
```

**Verify:** `pnpm run build` green. Every SFC touched is `<script setup lang="ts">`; the only HTTP-adjacent
call is through `activityFilesStore` (backed by `src/services/`).

## Step 13 — `ui/src/views/__tests__/WorkoutsView.spec.ts` extension

**Edit** (this task owns the file). Extend `mountView` to accept an optional `activityFiles` initial
state (default `{}`), keeping every existing call site (which passes one argument) working unchanged, and
add the four new cases.

```ts
import ImportReviewCard from '@/components/import/ImportReviewCard.vue'
import { useActivityFilesStore } from '@/stores/activityFiles'
import type { ActivityFileUploadResponse } from '@/types/activityFiles'
```

```ts
function mountView(training: Record<string, unknown>, activityFiles: Record<string, unknown> = {}) {
  return mount(WorkoutsView, {
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, initialState: { training, activityFiles } })],
      stubs: { RouterLink: RouterLinkStub, AppSidebar: true },
    },
    attachTo: document.body,
  })
}

function setInputFiles(input: HTMLInputElement, file: File) {
  Object.defineProperty(input, 'files', { value: [file], configurable: true })
}

const preview: ActivityFileUploadResponse = {
  id: 'af1',
  fileName: 'ride.fit',
  format: 'Fit',
  byteSize: 100,
  parsed: {
    sport: 'Bike',
    completedDate: '2026-07-20',
    startTimeUtc: '2026-07-20T10:00:00Z',
    durationSeconds: 3600,
    distanceMeters: 30000,
    avgHr: 150,
    maxHr: 175,
    avgPower: 210,
    avgPace: null,
    sampleCount: 500,
  },
  computedLoad: 80,
  zoneSeconds: [],
  matchCandidates: [],
}
```

New tests, appended inside the existing `describe('WorkoutsView', ...)`:

```ts
it('renders the import drop zone when there is no preview', () => {
  const wrapper = mountView({ workouts: [], workoutsHasMore: false })

  expect(wrapper.text()).toContain('Import file')
  expect(wrapper.findComponent(ImportReviewCard).exists()).toBe(false)

  wrapper.unmount()
})

it('rejects an unsupported extension without calling upload', async () => {
  const wrapper = mountView({ workouts: [], workoutsHasMore: false })
  const store = useActivityFilesStore()
  const input = wrapper.find('input[type="file"]')
  setInputFiles(input.element as HTMLInputElement, new File(['x'], 'ride.exe'))

  await input.trigger('change')

  expect(store.upload).not.toHaveBeenCalled()

  wrapper.unmount()
})

it('rejects a file over the size cap without calling upload', async () => {
  const wrapper = mountView({ workouts: [], workoutsHasMore: false })
  const store = useActivityFilesStore()
  const bigFile = new File([new Uint8Array(1)], 'ride.fit')
  Object.defineProperty(bigFile, 'size', { value: 26 * 1024 * 1024 })
  const input = wrapper.find('input[type="file"]')
  setInputFiles(input.element as HTMLInputElement, bigFile)

  await input.trigger('change')

  expect(store.upload).not.toHaveBeenCalled()

  wrapper.unmount()
})

it('renders ImportReviewCard instead of the drop zone when a preview exists', () => {
  const wrapper = mountView({ workouts: [], workoutsHasMore: false }, { preview })

  expect(wrapper.findComponent(ImportReviewCard).exists()).toBe(true)
  expect(wrapper.text()).not.toContain('Drop a .fit')

  wrapper.unmount()
})
```

Keep the four existing tests (`renders a row per workout...`, `shows the empty state...`,
`reloads page 1 when a sport filter is selected`, `loads the next page when "Load more" is clicked`)
passing unchanged — none of their `mountView({...})` call sites need editing.

**Verify:** `pnpm exec vitest run ui/src/views/__tests__/WorkoutsView.spec.ts --no-file-parallelism`
— all 8 cases green.

## Step 14 — `ui/src/views/WorkoutDetailView.vue` wiring (edit — additive)

**Edit.** Add the store import/instance, call `loadSource` from the existing `load()`, and render the
badge next to the sport pill. Do **not** touch the metric strip, the edit form wiring, the delete flow,
or the planned-vs-actual table.

Script additions (after the existing imports):

```ts
import { useActivityFilesStore } from '@/stores/activityFiles'
```

```ts
const activityFilesStore = useActivityFilesStore()
```

Edit the existing `load()` function — add one line, do not change anything else in it:

```ts
async function load() {
  await store.loadWorkout(id.value)
  await activityFilesStore.loadSource(id.value)
  const w = store.currentWorkout
  if (w?.plannedWorkoutId && w.trainingPlanId) {
    await store.loadStructure(w.trainingPlanId, w.plannedWorkoutId)
  }
}
```

After `const workout = computed(() => store.currentWorkout)`:

```ts
const source = computed(() => activityFilesStore.source)
```

Template — inside the header's `<div class="flex items-center gap-3">` (the block containing `TypePill`
and the date span, ~L156–160), insert the badge between them, transcribed verbatim from
`Tasks-19-5.md`'s Acceptance Criteria #9 (mirrors `TimeInZoneSection.vue:68–72`):

```html
<div class="flex items-center gap-3">
  <TypePill :kind="sportToPillKind(workout.sport)">{{ workout.sport }}</TypePill>
  <span
    v-if="source"
    class="rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-muted-foreground"
    :title="source.fileName"
  >
    from file
  </span>
  <span class="font-mono text-sm text-muted-foreground">{{ formatDay(workout.completedDate) }}</span>
</div>
```

**Verify:** `pnpm run build` green.

## Step 15 — `ui/src/views/__tests__/WorkoutDetailView.spec.ts` extension

**Edit** (this task owns the file). Add an `activityFiles` initial state to `mountView` (default
`{ source: null }`, so the three existing tests — which call `await mountView()` with no arguments —
keep passing unchanged), and two new cases.

```ts
import { useActivityFilesStore } from '@/stores/activityFiles'
```

```ts
async function mountView(activityFiles: Record<string, unknown> = { source: null }) {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push('/workouts/w1')
  await router.isReady()
  const wrapper = mount(WorkoutDetailView, {
    global: {
      plugins: [
        router,
        createTestingPinia({
          createSpy: vi.fn,
          initialState: { training: { currentWorkout: workout, structure }, activityFiles },
        }),
      ],
      stubs: { AppSidebar: true },
    },
    attachTo: document.body,
  })
  return { wrapper, store: useTrainingStore(), router }
}
```

New tests, appended inside the existing `describe('WorkoutDetailView', ...)`:

```ts
it('renders the "from file" badge when the workout has a source file', async () => {
  const { wrapper } = await mountView({
    source: { id: 'af1', fileName: 'ride.fit', format: 'Fit', uploadedAt: '2026-07-20T10:00:00Z' },
  })

  expect(wrapper.text()).toContain('from file')

  wrapper.unmount()
})

it('renders no badge for a manually logged workout', async () => {
  const { wrapper } = await mountView({ source: null })

  expect(wrapper.text()).not.toContain('from file')

  wrapper.unmount()
})
```

Note `useActivityFilesStore` is imported for type/store access but the three existing tests never call
`useActivityFilesStore()` directly — only the new two need it, and only implicitly via `mountView`'s
seeded state.

**Verify:** `pnpm exec vitest run ui/src/views/__tests__/WorkoutDetailView.spec.ts --no-file-parallelism`
— all 5 cases green.

## Step 16 — Full verification + manual smoke + commit

- `pnpm run build` (runs `vue-tsc -b && vite build`) green — a type error anywhere in the new surface
  fails the build, which is the point.
- `pnpm exec vitest run --no-file-parallelism` — full suite green, risen from the **252 / 56 files**
  baseline (Step 0) with **zero failures**. If the known transient worker-fork crash appears with every
  test still reporting passed, re-run once before treating it as real (the repo's
  vitest-worker-crash-transient note).
- `dotnet build api/Bryk.sln` and `dotnet test api/Bryk.sln` — green, and the count is **unchanged** from
  the Step 0 baseline (no backend file in this task's diff).
- **Runtime browser check** (not just the build): start the dev stack — `dotnet run` from `api/Bryk.API`
  and, in a second shell, `pnpm dev` from `ui/` (proxies `/api` to the API per `ui/vite.config.ts`). Open
  `/workouts` with the browser console open and confirm:
  - The dashed drop zone renders above the filter bar with an "Import file" button.
  - Selecting a valid `.fit`/`.tcx`/`.gpx` fixture (or dragging one onto the zone) shows "Parsing…", then
    replaces the drop zone with the review card: file name/format/size, the metric strip, the zone bars
    (or nothing, if the fixture has no HR/power samples), and the match-candidate radio list with "No
    planned workout" always present.
  - Picking an unsupported extension or an oversized file shows the destructive-style message **without**
    a network request in the Network tab.
  - Confirm routes to `/workouts/{newId}` and the new workout's detail page shows the "from file" badge
    (hover shows the source file name as the tooltip); Discard returns to the drop zone and the history
    list is unchanged (no orphan `ActivityFile` — verified indirectly: re-uploading the same fixture
    succeeds rather than hitting a duplicate-commit rejection, since nothing was ever committed).
  - A workout logged by hand (no linked `ActivityFile`) shows no badge on its detail page.
  - The console is clean throughout (no Vue warnings, no unhandled rejections).
- `git diff --stat` — confirm only the expected files changed/added:
  - `ui/src/services/api.ts` (edit)
  - `ui/src/services/__tests__/api.spec.ts` (new)
  - `ui/src/types/activityFiles.ts` (new)
  - `ui/src/services/activityFiles.ts` (new)
  - `ui/src/services/__tests__/activityFiles.spec.ts` (new)
  - `ui/src/stores/activityFiles.ts` (new)
  - `ui/src/stores/__tests__/activityFiles.spec.ts` (new)
  - `ui/src/components/import/ZoneHistogramBars.vue` (new)
  - `ui/src/components/import/MatchCandidateList.vue` (new)
  - `ui/src/components/import/__tests__/MatchCandidateList.spec.ts` (new)
  - `ui/src/components/import/ImportReviewCard.vue` (new)
  - `ui/src/components/import/__tests__/ImportReviewCard.spec.ts` (new)
  - `ui/src/views/WorkoutsView.vue` (edit)
  - `ui/src/views/__tests__/WorkoutsView.spec.ts` (edit)
  - `ui/src/views/WorkoutDetailView.vue` (edit)
  - `ui/src/views/__tests__/WorkoutDetailView.spec.ts` (edit)
  - Nothing under `api/`, no `package.json` change, no `ui/src/router/index.ts`, no
    `ui/src/components/layout/AppSidebar.vue`, no `ui/src/types/training.ts`, and nothing under
    `ui/src/components/analytics/` or `ui/src/types/analytics.ts`. If the diff shows any of these,
    **STOP** — that is scope creep beyond `Tasks-19-5.md`.
- Commit with the message from `Tasks-19-5.md` (no AI co-author trailer — project convention):

```
feat(ui): activity-file upload, import review and "from file" badge

Unblock uploads first: apiFetch hardcoded Content-Type: application/json on
every request, which breaks multipart because the browser must supply the
boundary itself. Guard on body instanceof FormData and leave the JSON default
for everything else - with a regression test, since every future upload
surface depends on it.

The review flow follows the two-step API. WorkoutsView gains a drop zone and
an Import file button (no new route and no new sidebar entry - Workouts
already exists), which POSTs the file and renders the parsed preview: a metric
strip, the five-bucket zone histogram, and a radio list of the planned
sessions within a day of the file's date, same sport, still unlinked, with a
first-class "No planned workout" option. Confirm commits and routes to the new
workout; Discard deletes the stored file so an abandoned import leaves nothing
behind.

Workout detail gains a "from file" badge fed by the reverse lookup rather than
a field on the workout - there is no Workout.SourceFileId and none was added
(ADR-0010 4). The zone bars are duplicated rather than extracted from
TimeInZoneSection, which Task 19-6 is rewriting in the same phase; the
duplication is recorded as tech debt in the handoff.
```
