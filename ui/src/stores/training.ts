import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  getThisWeek,
  createPlan as createPlanApi,
  getPlans as getPlansApi,
  getPlan as getPlanApi,
  updatePlan as updatePlanApi,
  getWeeklyTargets as getWeeklyTargetsApi,
  getStructure as getStructureApi,
  saveStructure as saveStructureApi,
  logWorkout as logWorkoutApi,
  getRecentWorkouts as getRecentWorkoutsApi,
  getWorkouts as getWorkoutsApi,
  getWorkout as getWorkoutApi,
  updateWorkout as updateWorkoutApi,
  deleteWorkout as deleteWorkoutApi,
} from '@/services/training'
import type { PlannedSport, UpdateWorkoutRequest } from '@/types/training'
import type {
  ThisWeekResponse,
  TrainingPlanRequest,
  TrainingPlanResponse,
  TrainingPlanUpdateRequest,
  WeeklyTargetsResponse,
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

  // ── Plan browser (Task 13-5) ──
  const plans = ref<TrainingPlanResponse[] | null>(null)
  const loadingPlans = ref(false)
  const plansError = ref<ApiError | Error | null>(null)

  async function loadPlans() {
    loadingPlans.value = true
    plansError.value = null
    try {
      plans.value = await getPlansApi()
    } catch (e) {
      plansError.value = e as ApiError | Error
    } finally {
      loadingPlans.value = false
    }
  }

  const currentPlan = ref<TrainingPlanResponse | null>(null)
  const loadingPlan = ref(false)
  const planError = ref<ApiError | Error | null>(null)

  async function loadPlan(id: string) {
    loadingPlan.value = true
    planError.value = null
    currentPlan.value = null
    try {
      currentPlan.value = await getPlanApi(id)
    } catch (e) {
      planError.value = e as ApiError | Error
    } finally {
      loadingPlan.value = false
    }
  }

  // ── Periodization (Task 18-4): compute-on-read targets + the plan-metadata write ──
  const weeklyTargets = ref<WeeklyTargetsResponse | null>(null)
  const loadingTargets = ref(false)
  const targetsError = ref<ApiError | Error | null>(null)

  async function loadWeeklyTargets(id: string) {
    loadingTargets.value = true
    targetsError.value = null
    weeklyTargets.value = null
    try {
      weeklyTargets.value = await getWeeklyTargetsApi(id)
    } catch (e) {
      targetsError.value = e as ApiError | Error
    } finally {
      loadingTargets.value = false
    }
  }

  // The PUT returns server truth including the plan's untouched planned workouts, so it can replace
  // currentPlan outright — no second loadPlan. A window/cadence/event change reshapes the whole ramp,
  // so targets are re-read after. Re-throws so the form can map the 400 (mirrors updateWorkout).
  async function updatePlan(id: string, req: TrainingPlanUpdateRequest): Promise<TrainingPlanResponse> {
    const updated = await updatePlanApi(id, req)
    currentPlan.value = updated
    await loadWeeklyTargets(id)
    return updated
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

  // ── Single workout detail (Task 13-4) ──
  const currentWorkout = ref<WorkoutResponse | null>(null)
  const loadingCurrent = ref(false)
  const currentError = ref<ApiError | Error | null>(null)

  async function loadWorkout(id: string) {
    loadingCurrent.value = true
    currentError.value = null
    currentWorkout.value = null
    try {
      currentWorkout.value = await getWorkoutApi(id)
    } catch (e) {
      currentError.value = e as ApiError | Error
    } finally {
      loadingCurrent.value = false
    }
  }

  // Refresh the surfaces that show a workout after it changes (dashboard feed + history list).
  async function refreshWorkoutSurfaces() {
    await loadRecentWorkouts()
    if (workouts.value) await loadWorkouts(workoutsFilter.value)
  }

  // Edit a workout (replace-style); the PUT returns server truth (recomputed load). Re-throws for the form.
  async function updateWorkout(id: string, req: UpdateWorkoutRequest): Promise<WorkoutResponse> {
    const updated = await updateWorkoutApi(id, req)
    currentWorkout.value = updated
    await refreshWorkoutSurfaces()
    return updated
  }

  // Hard-delete a workout, then refresh the lists. Re-throws for the confirm handler.
  async function deleteWorkout(id: string): Promise<void> {
    await deleteWorkoutApi(id)
    if (currentWorkout.value?.id === id) currentWorkout.value = null
    await refreshWorkoutSurfaces()
  }

  return {
    thisWeek,
    loadingThisWeek,
    thisWeekError,
    loadThisWeek,
    createPlan,
    plans,
    loadingPlans,
    plansError,
    loadPlans,
    currentPlan,
    loadingPlan,
    planError,
    loadPlan,
    weeklyTargets,
    loadingTargets,
    targetsError,
    loadWeeklyTargets,
    updatePlan,
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
    currentWorkout,
    loadingCurrent,
    currentError,
    loadWorkout,
    updateWorkout,
    deleteWorkout,
  }
})
