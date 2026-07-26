import { apiFetch } from './api'
import type {
  ActivityFileCommitResponse,
  ActivityFileSource,
  ActivityFileUploadResponse,
} from '@/types/activityFiles'

export const ACCEPTED_EXTENSIONS = '.fit,.tcx,.gpx'

// Mirrors the server cap so the UI can refuse an oversized file before spending an upload; the server
// remains the authority (its validator returns the 400 that actually counts).
export const MAX_UPLOAD_BYTES = 25 * 1024 * 1024

// Multipart upload (Task 19-5). Deliberately sets NO headers: apiFetch omits Content-Type for a
// FormData body so the browser can supply its own multipart boundary. The part name 'file' matches the
// controller's IFormFile? file parameter.
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

export async function discardActivityFile(id: string): Promise<void> {
  await apiFetch<void>(`/activityfiles/${id}`, { method: 'DELETE' })
}

// Diverges from every other service here on purpose: a null body is the NORMAL answer for a manually
// logged workout (the endpoint returns 200 with null rather than 404, ADR-0010 §4), so this must return
// null instead of throwing the "Unexpected empty response" error the others use.
export async function getWorkoutSource(workoutId: string): Promise<ActivityFileSource | null> {
  return await apiFetch<ActivityFileSource>(`/activityfiles/by-workout/${workoutId}`)
}
