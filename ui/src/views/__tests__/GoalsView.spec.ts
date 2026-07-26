import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import GoalsView from '@/views/GoalsView.vue'
import GoalsEventCard from '@/components/goals/GoalsEventCard.vue'
import GoalsGoalCard from '@/components/goals/GoalsGoalCard.vue'
import type { EventListItem, GoalListItem } from '@/types/goals'

const stub = { template: '<div />' }

function makeEvent(overrides: Partial<EventListItem> & { id: string }): EventListItem {
  return {
    name: 'Event',
    eventDate: '2099-09-01',
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

async function mountView(state: Record<string, unknown>) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: stub },
      { path: '/goals', name: 'goals', component: GoalsView },
      { path: '/plans/:id', name: 'plan-detail', component: stub },
    ],
  })
  await router.push('/goals')
  await router.isReady()
  return mount(GoalsView, {
    global: {
      plugins: [
        router,
        createTestingPinia({
          createSpy: () => () => {},
          initialState: { goals: state },
        }),
      ],
    },
  })
}

describe('GoalsView', () => {
  it('renders a card per event and per goal, splitting past events out', async () => {
    const wrapper = await mountView({
      events: [
        makeEvent({ id: 'e1', name: 'Boston Marathon', priority: 'A' }),
        makeEvent({ id: 'e2', name: 'Old Race', eventDate: '2000-01-01' }),
      ],
      goals: [makeGoal({ id: 'g1', description: 'Swim 3x a week' })],
      loading: false,
      error: null,
    })

    const text = wrapper.text()
    expect(text).toContain('Boston Marathon')
    expect(text).toContain('Swim 3x a week')
    expect(text).toContain('Past events')
    expect(text).toContain('Old Race')

    expect(wrapper.findAllComponents(GoalsEventCard)).toHaveLength(2)
    expect(wrapper.findAllComponents(GoalsGoalCard)).toHaveLength(1)

    wrapper.unmount()
  })

  it('renders the empty state for a fresh athlete', async () => {
    const wrapper = await mountView({ events: [], goals: [], loading: false, error: null })

    expect(wrapper.text()).toContain('Nothing on the calendar yet')
    expect(wrapper.text()).toContain('Add your first event or goal')
    expect(wrapper.findAllComponents(GoalsEventCard)).toHaveLength(0)

    wrapper.unmount()
  })

  it('renders the error banner with a retry affordance', async () => {
    const wrapper = await mountView({
      events: null,
      goals: null,
      loading: false,
      error: new Error('network down'),
    })

    const text = wrapper.text()
    expect(text).toContain('Could not load goals')
    expect(text).toContain('network down')
    expect(text).toContain('Retry')

    wrapper.unmount()
  })

  it('stubs the add affordances until the forms land', async () => {
    const wrapper = await mountView({ events: [], goals: [], loading: false, error: null })

    const buttons = wrapper.findAll('button')
    expect(buttons.length).toBeGreaterThan(0)
    expect(buttons.every((b) => b.attributes('disabled') !== undefined)).toBe(true)

    wrapper.unmount()
  })
})
