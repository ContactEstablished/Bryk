import { apiFetch } from '@/services/api'
import type {
  ThisWeekResponse,
  TrainingPlanRequest,
  TrainingPlanResponse,
  PlannedWorkoutResponse,
  WorkoutStructureRequest,
} from '@/types/training'

export async function getThisWeek(): Promise<ThisWeekResponse> {
  const result = await apiFetch<ThisWeekResponse>('/training/this-week')
  if (result === null) {
    throw new Error('Unexpected empty response from /training/this-week')
  }
  return result
}

// Creates a plan and its planned workouts in one POST — TrainingPlanRequest carries the
// planned workouts inline (Task 9-3), so authoring is a single atomic call.
export async function createPlan(req: TrainingPlanRequest): Promise<TrainingPlanResponse> {
  const result = await apiFetch<TrainingPlanResponse>('/trainingplans', {
    method: 'POST',
    body: JSON.stringify(req),
  })
  if (result === null) {
    throw new Error('Unexpected empty response from POST /trainingplans')
  }
  return result
}

// Structured-workout read/write (Task 10-4) — blocks + steps through the planned-workout aggregate.
export async function getStructure(
  planId: string,
  plannedWorkoutId: string,
): Promise<PlannedWorkoutResponse> {
  const result = await apiFetch<PlannedWorkoutResponse>(
    `/trainingplans/${planId}/plannedworkouts/${plannedWorkoutId}/structure`,
  )
  if (result === null) {
    throw new Error('Unexpected empty response from GET structure')
  }
  return result
}

export async function saveStructure(
  planId: string,
  plannedWorkoutId: string,
  request: WorkoutStructureRequest,
): Promise<PlannedWorkoutResponse> {
  const result = await apiFetch<PlannedWorkoutResponse>(
    `/trainingplans/${planId}/plannedworkouts/${plannedWorkoutId}/structure`,
    { method: 'PUT', body: JSON.stringify(request) },
  )
  if (result === null) {
    throw new Error('Unexpected empty response from PUT structure')
  }
  return result
}
