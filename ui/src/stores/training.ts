import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import { getThisWeek, createPlan as createPlanApi } from '@/services/training'
import type {
  ThisWeekResponse,
  TrainingPlanRequest,
  TrainingPlanResponse,
} from '@/types/training'

export const useTrainingStore = defineStore('training', () => {
  const thisWeek = ref<ThisWeekResponse | null>(null)
  const loadingThisWeek = ref(false)
  const thisWeekError = ref<ApiError | Error | null>(null)

  async function loadThisWeek() {
    loadingThisWeek.value = true
    thisWeekError.value = null
    try {
      thisWeek.value = await getThisWeek()
    } catch (e) {
      thisWeekError.value = e as ApiError | Error
    } finally {
      loadingThisWeek.value = false
    }
  }

  // Create a plan (with its planned workouts), then re-fetch This Week so the dashboard
  // reflects any sessions that fall in the current week. Re-throws so the form can map
  // validation errors (mirrors the saveRequired -> re-load pattern in stores/profile.ts).
  async function createPlan(req: TrainingPlanRequest): Promise<TrainingPlanResponse> {
    const created = await createPlanApi(req)
    await loadThisWeek()
    return created
  }

  return {
    thisWeek,
    loadingThisWeek,
    thisWeekError,
    loadThisWeek,
    createPlan,
  }
})
