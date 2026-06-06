import { apiFetch } from '@/services/api'
import type {
  ThisWeekResponse,
  TrainingPlanRequest,
  TrainingPlanResponse,
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
