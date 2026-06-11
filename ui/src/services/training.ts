import { apiFetch } from '@/services/api'
import type {
  ThisWeekResponse,
  TrainingPlanRequest,
  TrainingPlanResponse,
  PlannedWorkoutResponse,
  WorkoutStructureRequest,
  WorkoutResponse,
  LogWorkoutRequest,
  UpdateWorkoutRequest,
  PlannedSport,
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

// Executed-workout capture (Task 11-4).
export async function logWorkout(req: LogWorkoutRequest): Promise<WorkoutResponse> {
  const result = await apiFetch<WorkoutResponse>('/workouts', {
    method: 'POST',
    body: JSON.stringify(req),
  })
  if (result === null) {
    throw new Error('Unexpected empty response from POST /workouts')
  }
  return result
}

export async function getRecentWorkouts(take = 10): Promise<WorkoutResponse[]> {
  return (await apiFetch<WorkoutResponse[]>(`/workouts?take=${take}`)) ?? []
}

// Filtered/paged workout history (Task 13-2). All params optional; absent params are omitted.
export interface WorkoutListParams {
  from?: string
  to?: string
  sport?: PlannedSport
  skip?: number
  take?: number
}

export async function getWorkouts(params: WorkoutListParams = {}): Promise<WorkoutResponse[]> {
  const qs = new URLSearchParams()
  if (params.from) qs.set('from', params.from)
  if (params.to) qs.set('to', params.to)
  if (params.sport) qs.set('sport', params.sport)
  if (params.skip != null) qs.set('skip', String(params.skip))
  if (params.take != null) qs.set('take', String(params.take))
  const query = qs.toString()
  return (await apiFetch<WorkoutResponse[]>(`/workouts${query ? `?${query}` : ''}`)) ?? []
}

export async function getWorkout(id: string): Promise<WorkoutResponse> {
  const result = await apiFetch<WorkoutResponse>(`/workouts/${id}`)
  if (result === null) {
    throw new Error('Unexpected empty response from GET /workouts/{id}')
  }
  return result
}

// Replace-style edit (Task 13-1): recomputes load server-side; returns the saved workout.
export async function updateWorkout(id: string, req: UpdateWorkoutRequest): Promise<WorkoutResponse> {
  const result = await apiFetch<WorkoutResponse>(`/workouts/${id}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  })
  if (result === null) {
    throw new Error('Unexpected empty response from PUT /workouts/{id}')
  }
  return result
}

// Hard delete (Task 13-1): 204, no body.
export async function deleteWorkout(id: string): Promise<void> {
  await apiFetch<void>(`/workouts/${id}`, { method: 'DELETE' })
}
