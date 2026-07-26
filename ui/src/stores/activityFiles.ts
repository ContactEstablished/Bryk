import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  commitActivityFile,
  discardActivityFile,
  getWorkoutSource,
  uploadActivityFile,
} from '@/services/activityFiles'
import type { ActivityFileSource, ActivityFileUploadResponse } from '@/types/activityFiles'

// The middleware's error shape: { status, error, errors[], traceId }.
function firstError(err: unknown, fallback: string): string {
  if (err instanceof ApiError) {
    const body = err.body as { errors?: string[] } | null
    return body?.errors?.[0] ?? fallback
  }
  return fallback
}

export const useActivityFilesStore = defineStore('activityFiles', () => {
  const preview = ref<ActivityFileUploadResponse | null>(null)
  const uploading = ref(false)
  const uploadError = ref<string | null>(null)
  const committing = ref(false)
  const commitError = ref<string | null>(null)
  const selectedPlannedWorkoutId = ref<string | null>(null)
  const source = ref<ActivityFileSource | null>(null)

  async function upload(file: File) {
    uploadError.value = null
    commitError.value = null
    uploading.value = true
    try {
      const result = await uploadActivityFile(file)
      preview.value = result
      // Preselect only when the answer is unambiguous: exactly one candidate on the file's own day.
      const sameDay = result.matchCandidates.filter((c) => c.dayOffset === 0)
      selectedPlannedWorkoutId.value = sameDay.length === 1 ? sameDay[0].plannedWorkoutId : null
    } catch (err) {
      // Does not re-throw — the drop zone renders the message.
      uploadError.value = firstError(err, "Couldn't read that file.")
    } finally {
      uploading.value = false
    }
  }

  async function commit(): Promise<string | null> {
    if (!preview.value) return null
    commitError.value = null
    committing.value = true
    try {
      const result = await commitActivityFile(preview.value.id, selectedPlannedWorkoutId.value)
      preview.value = null
      selectedPlannedWorkoutId.value = null
      return result.workoutId
    } catch (err) {
      commitError.value = firstError(err, "Couldn't save that import.")
      return null
    } finally {
      committing.value = false
    }
  }

  async function discard() {
    if (!preview.value) return
    try {
      await discardActivityFile(preview.value.id)
    } finally {
      reset()
    }
  }

  function reset() {
    preview.value = null
    uploadError.value = null
    commitError.value = null
    selectedPlannedWorkoutId.value = null
  }

  // A missing badge must never break the detail page, so failures collapse to null.
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
