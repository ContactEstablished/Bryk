import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import { getCalendarFeed } from '@/services/calendar'
import type { CalendarFeedResponse } from '@/types/calendar'

export const useCalendarStore = defineStore('calendar', () => {
  const feed = ref<CalendarFeedResponse | null>(null)
  const loading = ref(false)
  const error = ref<ApiError | Error | null>(null)

  async function loadFeed(from?: string, to?: string) {
    loading.value = true
    error.value = null
    try {
      feed.value = await getCalendarFeed(from, to)
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loading.value = false
    }
  }

  return { feed, loading, error, loadFeed }
})
