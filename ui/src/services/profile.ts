import { apiFetch } from '@/services/api'
import type {
  ProfileRequiredResponse,
  ProfileRecommendedResponse,
  ProfileGoalsResponse,
} from '@/types/profile'

export async function getRequired(): Promise<ProfileRequiredResponse> {
  const result = await apiFetch<ProfileRequiredResponse>('/profile/required')
  if (result === null) {
    throw new Error('Unexpected empty response from /profile/required')
  }
  return result
}

export async function getRecommended(): Promise<ProfileRecommendedResponse> {
  const result = await apiFetch<ProfileRecommendedResponse>('/profile/recommended')
  if (result === null) {
    throw new Error('Unexpected empty response from /profile/recommended')
  }
  return result
}

export async function getGoals(): Promise<ProfileGoalsResponse> {
  const result = await apiFetch<ProfileGoalsResponse>('/profile/goals')
  if (result === null) {
    throw new Error('Unexpected empty response from /profile/goals')
  }
  return result
}
