import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useGoalsStore } from '@/stores/goals'
import { getEvents, getGoalsList } from '@/services/goals-events'
import type { EventListItem, GoalListItem } from '@/types/goals'

vi.mock('@/services/goals-events', () => ({
  getEvents: vi.fn(),
  getGoalsList: vi.fn(),
}))

const getEventsMock = vi.mocked(getEvents)
const getGoalsListMock = vi.mocked(getGoalsList)

function makeEvent(overrides: Partial<EventListItem> & { id: string }): EventListItem {
  return {
    name: 'Event',
    eventDate: '2099-01-01',
    sport: 'Run',
    triathlonDistance: null,
    customDistanceName: null,
    priority: 'B',
    notes: null,
    linkedPlans: [],
    ...overrides,
  }
}

function makeGoal(overrides: Partial<GoalListItem> & { id: string }): GoalListItem {
  return {
    type: 'General',
    description: 'Goal',
    targetDate: null,
    daysRemaining: null,
    status: 'NoDate',
    ...overrides,
  }
}

// Today as the server sees it (UTC), so the "today is still upcoming" case is exact.
function utcTodayIso(): string {
  const now = new Date()
  return [
    now.getUTCFullYear(),
    String(now.getUTCMonth() + 1).padStart(2, '0'),
    String(now.getUTCDate()).padStart(2, '0'),
  ].join('-')
}

describe('useGoalsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loadAll populates events and goals from both services', async () => {
    getEventsMock.mockResolvedValue([makeEvent({ id: 'e1', name: 'Boston Marathon' })])
    getGoalsListMock.mockResolvedValue([makeGoal({ id: 'g1', description: 'Sub 3' })])

    const store = useGoalsStore()
    await store.loadAll()

    expect(store.events).toHaveLength(1)
    expect(store.events?.[0].name).toBe('Boston Marathon')
    expect(store.goals).toHaveLength(1)
    expect(store.goals?.[0].description).toBe('Sub 3')
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('upcomingEvents excludes past events, keeps today, and sorts priority before proximity', async () => {
    const today = utcTodayIso()
    getEventsMock.mockResolvedValue([
      makeEvent({ id: 'past', name: 'Old Race', priority: 'A', eventDate: '2000-01-01' }),
      makeEvent({ id: 'b', name: 'Local 10k', priority: 'B', eventDate: '2099-02-01' }),
      makeEvent({ id: 'a', name: 'Boston Marathon', priority: 'A', eventDate: '2099-09-01' }),
      makeEvent({ id: 'today', name: 'Club TT', priority: 'C', eventDate: today }),
    ])
    getGoalsListMock.mockResolvedValue([])

    const store = useGoalsStore()
    await store.loadAll()

    const ids = store.upcomingEvents.map((e) => e.id)
    expect(ids).not.toContain('past')
    expect(ids).toContain('today')
    // The A-event leads even though the B-event is sooner.
    expect(ids).toEqual(['a', 'b', 'today'])
  })

  it('upcomingEvents is empty before the first load', () => {
    const store = useGoalsStore()
    expect(store.upcomingEvents).toEqual([])
  })

  it('sets error and clears loading when a service rejects', async () => {
    getEventsMock.mockRejectedValue(new Error('boom'))
    getGoalsListMock.mockResolvedValue([])

    const store = useGoalsStore()
    await store.loadAll()

    expect(store.error).toBeInstanceOf(Error)
    expect(store.error?.message).toBe('boom')
    expect(store.events).toBeNull()
    expect(store.loading).toBe(false)
  })
})
