import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  getThisWeek,
  createPlan as createPlanApi,
  getStructure as getStructureApi,
  saveStructure as saveStructureApi,
} from '@/services/training'
import type {
  ThisWeekResponse,
  TrainingPlanRequest,
  TrainingPlanResponse,
  PlannedWorkoutResponse,
  WorkoutStructureRequest,
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

  // ── Structured-workout payload (Task 10-5) ──
  const structure = ref<PlannedWorkoutResponse | null>(null)
  const loadingStructure = ref(false)
  const structureError = ref<ApiError | Error | null>(null)

  async function loadStructure(planId: string, plannedWorkoutId: string) {
    loadingStructure.value = true
    structureError.value = null
    structure.value = null
    try {
      structure.value = await getStructureApi(planId, plannedWorkoutId)
    } catch (e) {
      structureError.value = e as ApiError | Error
    } finally {
      loadingStructure.value = false
    }
  }

  // Replace a planned workout's blocks/steps; the PUT returns the saved structure, so the
  // builder gets server truth (ids assigned) without a second round-trip. Re-throws for the form.
  async function saveStructure(
    planId: string,
    plannedWorkoutId: string,
    request: WorkoutStructureRequest,
  ): Promise<PlannedWorkoutResponse> {
    const updated = await saveStructureApi(planId, plannedWorkoutId, request)
    structure.value = updated
    return updated
  }

  return {
    thisWeek,
    loadingThisWeek,
    thisWeekError,
    loadThisWeek,
    createPlan,
    structure,
    loadingStructure,
    structureError,
    loadStructure,
    saveStructure,
  }
})
