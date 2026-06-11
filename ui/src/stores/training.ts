import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  getThisWeek,
  createPlan as createPlanApi,
  getStructure as getStructureApi,
  saveStructure as saveStructureApi,
  logWorkout as logWorkoutApi,
  getRecentWorkouts as getRecentWorkoutsApi,
  getWorkouts as getWorkoutsApi,
} from '@/services/training'
import type { PlannedSport } from '@/types/training'
import type {
  ThisWeekResponse,
  TrainingPlanRequest,
  TrainingPlanResponse,
  PlannedWorkoutResponse,
  WorkoutStructureRequest,
  WorkoutResponse,
  LogWorkoutRequest,
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

  // ── Executed workouts (Task 11-5) ──
  const recentWorkouts = ref<WorkoutResponse[] | null>(null)
  const loadingRecent = ref(false)
  const recentError = ref<ApiError | Error | null>(null)

  async function loadRecentWorkouts() {
    loadingRecent.value = true
    recentError.value = null
    try {
      recentWorkouts.value = await getRecentWorkoutsApi()
    } catch (e) {
      recentError.value = e as ApiError | Error
    } finally {
      loadingRecent.value = false
    }
  }

  // Log a completed workout, then re-fetch the recent list so Recent Activity reflects it.
  // Re-throws so the form can map validation errors.
  async function logWorkout(req: LogWorkoutRequest): Promise<WorkoutResponse> {
    const created = await logWorkoutApi(req)
    await loadRecentWorkouts()
    return created
  }

  // ── Workout history list (Task 13-3) — filtered + paged via the 13-2 endpoint ──
  const WORKOUTS_PAGE_SIZE = 20

  interface WorkoutFilter {
    sport: PlannedSport | null
    from: string | null
    to: string | null
  }

  const workouts = ref<WorkoutResponse[] | null>(null)
  const workoutsFilter = ref<WorkoutFilter>({ sport: null, from: null, to: null })
  const loadingWorkouts = ref(false)
  const workoutsError = ref<ApiError | Error | null>(null)
  const workoutsHasMore = ref(false)

  function filterParams(filter: WorkoutFilter) {
    return {
      sport: filter.sport ?? undefined,
      from: filter.from ?? undefined,
      to: filter.to ?? undefined,
    }
  }

  // Apply a filter set and load the first page (replaces the current list).
  async function loadWorkouts(filter: WorkoutFilter) {
    workoutsFilter.value = filter
    loadingWorkouts.value = true
    workoutsError.value = null
    try {
      const page = await getWorkoutsApi({ ...filterParams(filter), skip: 0, take: WORKOUTS_PAGE_SIZE })
      workouts.value = page
      workoutsHasMore.value = page.length === WORKOUTS_PAGE_SIZE
    } catch (e) {
      workoutsError.value = e as ApiError | Error
    } finally {
      loadingWorkouts.value = false
    }
  }

  // Append the next page for the active filter ("load more").
  async function loadMoreWorkouts() {
    if (!workouts.value || !workoutsHasMore.value || loadingWorkouts.value) return
    loadingWorkouts.value = true
    workoutsError.value = null
    try {
      const page = await getWorkoutsApi({
        ...filterParams(workoutsFilter.value),
        skip: workouts.value.length,
        take: WORKOUTS_PAGE_SIZE,
      })
      workouts.value = [...workouts.value, ...page]
      workoutsHasMore.value = page.length === WORKOUTS_PAGE_SIZE
    } catch (e) {
      workoutsError.value = e as ApiError | Error
    } finally {
      loadingWorkouts.value = false
    }
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
    recentWorkouts,
    loadingRecent,
    recentError,
    loadRecentWorkouts,
    logWorkout,
    workouts,
    workoutsFilter,
    loadingWorkouts,
    workoutsError,
    workoutsHasMore,
    loadWorkouts,
    loadMoreWorkouts,
  }
})
