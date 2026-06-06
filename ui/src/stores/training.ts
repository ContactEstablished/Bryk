import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import { getThisWeek } from '@/services/training'
import type { ThisWeekResponse } from '@/types/training'

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

  return {
    thisWeek,
    loadingThisWeek,
    thisWeekError,
    loadThisWeek,
  }
})
