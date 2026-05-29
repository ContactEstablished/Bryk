import { apiFetch } from '@/services/api'
import type { GoalDto } from '@/types/onboarding'
import type { GoalResponse } from '@/types/profile'

export async function createGoal(data: GoalDto): Promise<GoalResponse> {
  const result = await apiFetch<GoalResponse>('/goals', {
    method: 'POST',
    body: JSON.stringify(data),
  })
  if (result === null) {
    throw new Error('Unexpected empty response from POST /goals')
  }
  return result
}

export async function updateGoal(
  id: string,
  data: GoalDto,
): Promise<GoalResponse> {
  const result = await apiFetch<GoalResponse>(`/goals/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
  if (result === null) {
    throw new Error(`Unexpected empty response from PUT /goals/${id}`)
  }
  return result
}

export async function deleteGoal(id: string): Promise<void> {
  await apiFetch<void>(`/goals/${id}`, { method: 'DELETE' })
}
