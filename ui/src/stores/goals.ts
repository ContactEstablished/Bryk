import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError } from '@/services/api'
import { getEvents, getGoalsList } from '@/services/goals-events'
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
import type { EventDto, GoalDto } from '@/types/onboarding'
import type { EventListItem, GoalListItem } from '@/types/goals'

// Today as YYYY-MM-DD in UTC, so date-string comparisons match the server's DateOnly
// semantics. Mirrors stores/profile.ts's local helper.
function utcTodayIso(): string {
  const now = new Date()
  const yyyy = now.getUTCFullYear()
  const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(now.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

// The /goals page's own store. Deliberately separate from stores/profile.ts, which composes
// its Goals section from /profile/goals — this one reads the first-class /events and /goals
// endpoints (which carry linked plans, days-remaining and status). The overlap is intentional.
export const useGoalsStore = defineStore('goals', () => {
  const events = ref<EventListItem[] | null>(null)
  const goals = ref<GoalListItem[] | null>(null)
  const loading = ref(false)
  const error = ref<ApiError | Error | null>(null)

  async function loadAll() {
    loading.value = true
    error.value = null
    try {
      const [ev, gl] = await Promise.all([getEvents(), getGoalsList()])
      events.value = ev
      goals.value = gl
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loading.value = false
    }
  }

  // Upcoming events (today inclusive), priority A<B<C then soonest date — the same ordering
  // as stores/profile.ts's primaryEvent, kept as the full filtered list rather than one pick.
  const upcomingEvents = computed<EventListItem[]>(() => {
    const all = events.value
    if (!all) return []
    const today = utcTodayIso()
    return all
      .filter((e) => e.eventDate >= today)
      .sort((a, b) =>
        a.priority !== b.priority
          ? a.priority.localeCompare(b.priority)
          : a.eventDate.localeCompare(b.eventDate),
      )
  })

  // Per-item CRUD against the existing events / goals write endpoints, then a full re-fetch so the
  // lists reflect server truth (new rows pick up their id, and every row its computed
  // daysRemaining / status / linkedPlans). Errors propagate for the form to map.
  async function createEvent(dto: EventDto) {
    await createEventApi(dto)
    await loadAll()
  }

  async function updateEvent(id: string, dto: EventDto) {
    await updateEventApi(id, dto)
    await loadAll()
  }

  async function deleteEvent(id: string) {
    await deleteEventApi(id)
    await loadAll()
  }

  async function createGoal(dto: GoalDto) {
    await createGoalApi(dto)
    await loadAll()
  }

  async function updateGoal(id: string, dto: GoalDto) {
    await updateGoalApi(id, dto)
    await loadAll()
  }

  async function deleteGoal(id: string) {
    await deleteGoalApi(id)
    await loadAll()
  }

  return {
    events,
    goals,
    loading,
    error,
    loadAll,
    upcomingEvents,
    createEvent,
    updateEvent,
    deleteEvent,
    createGoal,
    updateGoal,
    deleteGoal,
  }
})
