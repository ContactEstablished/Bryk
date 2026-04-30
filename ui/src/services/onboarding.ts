import { apiFetch } from '@/services/api'
import type {
  OnboardingRequiredRequest,
  OnboardingRecommendedRequest,
  OnboardingGoalsRequest,
  OnboardingStatusResponse,
} from '@/types/onboarding'

export async function getStatus(): Promise<OnboardingStatusResponse> {
  const result = await apiFetch<OnboardingStatusResponse>('/onboarding/status')
  if (result === null) {
    throw new Error('Unexpected empty response from /onboarding/status')
  }
  return result
}

export async function submitRequired(
  data: OnboardingRequiredRequest,
): Promise<void> {
  await apiFetch<void>('/onboarding/required', {
    method: 'POST',
    body: JSON.stringify(data),
  })
}

export async function submitRecommended(
  data: OnboardingRecommendedRequest,
): Promise<void> {
  await apiFetch<void>('/onboarding/recommended', {
    method: 'POST',
    body: JSON.stringify(data),
  })
}

export async function submitGoals(
  data: OnboardingGoalsRequest,
): Promise<void> {
  await apiFetch<void>('/onboarding/goals', {
    method: 'POST',
    body: JSON.stringify(data),
  })
}
