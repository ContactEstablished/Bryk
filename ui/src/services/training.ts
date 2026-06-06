import { apiFetch } from '@/services/api'
import type { ThisWeekResponse } from '@/types/training'

export async function getThisWeek(): Promise<ThisWeekResponse> {
  const result = await apiFetch<ThisWeekResponse>('/training/this-week')
  if (result === null) {
    throw new Error('Unexpected empty response from /training/this-week')
  }
  return result
}
