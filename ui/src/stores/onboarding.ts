import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  getStatus,
  submitRequired as submitRequiredApi,
  submitRecommended as submitRecommendedApi,
  submitGoals as submitGoalsApi,
} from '@/services/onboarding'
import type {
  OnboardingGoalsRequest,
  OnboardingRecommendedRequest,
  OnboardingRequiredRequest,
  OnboardingStatusResponse,
} from '@/types/onboarding'

export type OnboardingStep = 'required' | 'recommended' | 'goals' | 'done'

export const useOnboardingStore = defineStore('onboarding', () => {
  const status = ref<OnboardingStatusResponse | null>(null)
  const loadingStatus = ref(false)
  const submitting = ref(false)
  const error = ref<ApiError | Error | null>(null)

  const requiredComplete = computed(() => status.value?.requiredComplete ?? false)
  const recommendedComplete = computed(() => status.value?.recommendedComplete ?? false)
  const goalsComplete = computed(() => status.value?.goalsComplete ?? false)

  const nextIncompleteStep = computed<OnboardingStep>(() => {
    if (!requiredComplete.value) return 'required'
    if (!recommendedComplete.value) return 'recommended'
    if (!goalsComplete.value) return 'goals'
    return 'done'
  })

  function clearError() {
    error.value = null
  }

  async function loadStatus() {
    loadingStatus.value = true
    error.value = null
    try {
      status.value = await getStatus()
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loadingStatus.value = false
    }
  }

  async function submitRequired(data: OnboardingRequiredRequest) {
    submitting.value = true
    error.value = null
    try {
      await submitRequiredApi(data)
      status.value = await getStatus()
    } catch (e) {
      error.value = e as ApiError | Error
      throw e
    } finally {
      submitting.value = false
    }
  }

  async function submitRecommended(data: OnboardingRecommendedRequest) {
    submitting.value = true
    error.value = null
    try {
      await submitRecommendedApi(data)
      status.value = await getStatus()
    } catch (e) {
      error.value = e as ApiError | Error
      throw e
    } finally {
      submitting.value = false
    }
  }

  async function submitGoals(data: OnboardingGoalsRequest) {
    submitting.value = true
    error.value = null
    try {
      await submitGoalsApi(data)
      status.value = await getStatus()
    } catch (e) {
      error.value = e as ApiError | Error
      throw e
    } finally {
      submitting.value = false
    }
  }

  return {
    status,
    loadingStatus,
    submitting,
    error,
    requiredComplete,
    recommendedComplete,
    goalsComplete,
    nextIncompleteStep,
    clearError,
    loadStatus,
    submitRequired,
    submitRecommended,
    submitGoals,
  }
})
