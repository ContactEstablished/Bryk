import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  getRequired,
  getRecommended,
  getGoals,
} from '@/services/profile'
import {
  submitRequired as submitRequiredApi,
  submitRecommended as submitRecommendedApi,
} from '@/services/onboarding'
import {
  createEvent as createEventApi,
  updateEvent as updateEventApi,
  deleteEvent as deleteEventApi,
} from '@/services/events'
import {
  createGoal as createGoalApi,
  updateGoal as updateGoalApi,
  deleteGoal as deleteGoalApi,
} from '@/services/goals'
import type {
  OnboardingRequiredRequest,
  OnboardingRecommendedRequest,
  EventDto,
  GoalDto,
} from '@/types/onboarding'
import type {
  ProfileRequiredResponse,
  ProfileRecommendedResponse,
  ProfileGoalsResponse,
} from '@/types/profile'

export const useProfileStore = defineStore('profile', () => {
  const required = ref<ProfileRequiredResponse | null>(null)
  const recommended = ref<ProfileRecommendedResponse | null>(null)
  const goals = ref<ProfileGoalsResponse | null>(null)

  const loadingRequired = ref(false)
  const loadingRecommended = ref(false)
  const loadingGoals = ref(false)

  const requiredError = ref<ApiError | Error | null>(null)
  const recommendedError = ref<ApiError | Error | null>(null)
  const goalsError = ref<ApiError | Error | null>(null)

  async function loadRequired() {
    loadingRequired.value = true
    requiredError.value = null
    try {
      required.value = await getRequired()
    } catch (e) {
      requiredError.value = e as ApiError | Error
    } finally {
      loadingRequired.value = false
    }
  }

  async function loadRecommended() {
    loadingRecommended.value = true
    recommendedError.value = null
    try {
      recommended.value = await getRecommended()
    } catch (e) {
      recommendedError.value = e as ApiError | Error
    } finally {
      loadingRecommended.value = false
    }
  }

  async function loadGoals() {
    loadingGoals.value = true
    goalsError.value = null
    try {
      goals.value = await getGoals()
    } catch (e) {
      goalsError.value = e as ApiError | Error
    } finally {
      loadingGoals.value = false
    }
  }

  // Required / Recommended writes reuse the onboarding upsert POSTs, then re-load
  // the section so the form reflects server truth. Re-throw so the section component
  // can map field-level validation errors.
  async function saveRequired(payload: OnboardingRequiredRequest) {
    await submitRequiredApi(payload)
    await loadRequired()
  }

  async function saveRecommended(payload: OnboardingRecommendedRequest) {
    await submitRecommendedApi(payload)
    await loadRecommended()
  }

  // Goals section: per-item CRUD against the events / goals endpoints, then a
  // full re-fetch so the lists reflect server truth (new items pick up their id).
  // Re-throw on error for component-level handling.
  async function createEvent(dto: EventDto) {
    await createEventApi(dto)
    await loadGoals()
  }

  async function updateEvent(id: string, dto: EventDto) {
    await updateEventApi(id, dto)
    await loadGoals()
  }

  async function deleteEvent(id: string) {
    await deleteEventApi(id)
    await loadGoals()
  }

  async function createGoal(dto: GoalDto) {
    await createGoalApi(dto)
    await loadGoals()
  }

  async function updateGoal(id: string, dto: GoalDto) {
    await updateGoalApi(id, dto)
    await loadGoals()
  }

  async function deleteGoal(id: string) {
    await deleteGoalApi(id)
    await loadGoals()
  }

  return {
    required,
    recommended,
    goals,
    loadingRequired,
    loadingRecommended,
    loadingGoals,
    requiredError,
    recommendedError,
    goalsError,
    loadRequired,
    loadRecommended,
    loadGoals,
    saveRequired,
    saveRecommended,
    createEvent,
    updateEvent,
    deleteEvent,
    createGoal,
    updateGoal,
    deleteGoal,
  }
})
