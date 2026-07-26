import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useGoalsStore } from '@/stores/goals'
import { getEvents, getGoalsList } from '@/services/goals-events'
import { createEvent, updateEvent, deleteEvent } from '@/services/events'
import { createGoal, updateGoal, deleteGoal } from '@/services/goals'
import type { EventDto, GoalDto } from '@/types/onboarding'
import type { EventListItem, GoalListItem } from '@/types/goals'

vi.mock('@/services/goals-events', () => ({
  getEvents: vi.fn(),
  getGoalsList: vi.fn(),
}))

vi.mock('@/services/events', () => ({
  createEvent: vi.fn(),
  updateEvent: vi.fn(),
  deleteEvent: vi.fn(),
}))

vi.mock('@/services/goals', () => ({
  createGoal: vi.fn(),
  updateGoal: vi.fn(),
  deleteGoal: vi.fn(),
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

describe('useGoalsStore CRUD actions', () => {
  const eventDto: EventDto = {
    name: 'Boston Marathon',
    eventDate: '2099-09-01',
    sport: 'Run',
    triathlonDistance: null,
    customDistanceName: null,
    priority: 'A',
    notes: null,
  }

  const goalDto: GoalDto = { type: 'General', description: 'Sub 3', targetDate: null }

  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    getEventsMock.mockResolvedValue([])
    getGoalsListMock.mockResolvedValue([])
  })

  it('createEvent posts the dto then re-fetches both lists', async () => {
    const store = useGoalsStore()
    await store.createEvent(eventDto)

    expect(createEvent).toHaveBeenCalledWith(eventDto)
    expect(getEventsMock).toHaveBeenCalledTimes(1)
    expect(getGoalsListMock).toHaveBeenCalledTimes(1)
  })

  it('updateEvent puts by id then re-fetches', async () => {
    const store = useGoalsStore()
    await store.updateEvent('e1', eventDto)

    expect(updateEvent).toHaveBeenCalledWith('e1', eventDto)
    expect(getEventsMock).toHaveBeenCalledTimes(1)
  })

  it('deleteEvent deletes by id then re-fetches', async () => {
    const store = useGoalsStore()
    await store.deleteEvent('e1')

    expect(deleteEvent).toHaveBeenCalledWith('e1')
    expect(getEventsMock).toHaveBeenCalledTimes(1)
  })

  it('createGoal posts the dto then re-fetches both lists', async () => {
    const store = useGoalsStore()
    await store.createGoal(goalDto)

    expect(createGoal).toHaveBeenCalledWith(goalDto)
    expect(getEventsMock).toHaveBeenCalledTimes(1)
    expect(getGoalsListMock).toHaveBeenCalledTimes(1)
  })

  it('updateGoal puts by id then re-fetches', async () => {
    const store = useGoalsStore()
    await store.updateGoal('g1', goalDto)

    expect(updateGoal).toHaveBeenCalledWith('g1', goalDto)
    expect(getGoalsListMock).toHaveBeenCalledTimes(1)
  })

  it('deleteGoal deletes by id then re-fetches', async () => {
    const store = useGoalsStore()
    await store.deleteGoal('g1')

    expect(deleteGoal).toHaveBeenCalledWith('g1')
    expect(getGoalsListMock).toHaveBeenCalledTimes(1)
  })

  it('re-throws a write failure instead of swallowing it into error state', async () => {
    vi.mocked(createEvent).mockRejectedValue(new Error('409'))
    const store = useGoalsStore()

    await expect(store.createEvent(eventDto)).rejects.toThrow('409')
    // The failed write must not trigger a re-fetch.
    expect(getEventsMock).not.toHaveBeenCalled()
  })
})
