import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import GoalsView from '@/views/GoalsView.vue'
import GoalsEventCard from '@/components/goals/GoalsEventCard.vue'
import GoalsEventForm from '@/components/goals/GoalsEventForm.vue'
import GoalsGoalCard from '@/components/goals/GoalsGoalCard.vue'
import GoalsGoalForm from '@/components/goals/GoalsGoalForm.vue'
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
          createSpy: vi.fn,
          initialState: { goals: state },
        }),
      ],
    },
  })
}

async function clickButton(wrapper: VueWrapper, label: string) {
  const btn = wrapper.findAll('button').find((b) => b.text() === label)
  if (!btn) throw new Error(`Button "${label}" not found`)
  await btn.trigger('click')
  await flushPromises()
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

  it('"Add Event" reveals a draft event form', async () => {
    const wrapper = await mountView({ events: [], goals: [], loading: false, error: null })
    expect(wrapper.findAllComponents(GoalsEventForm)).toHaveLength(0)

    await clickButton(wrapper, 'Add Event')

    expect(wrapper.findAllComponents(GoalsEventForm)).toHaveLength(1)

    wrapper.unmount()
  })

  it('"Add Goal" reveals a draft goal form', async () => {
    const wrapper = await mountView({ events: [], goals: [], loading: false, error: null })
    expect(wrapper.findAllComponents(GoalsGoalForm)).toHaveLength(0)

    await clickButton(wrapper, 'Add Goal')

    expect(wrapper.findAllComponents(GoalsGoalForm)).toHaveLength(1)

    wrapper.unmount()
  })

  it('drops the draft form once the create succeeds', async () => {
    const wrapper = await mountView({ events: [], goals: [], loading: false, error: null })
    await clickButton(wrapper, 'Add Event')

    const form = wrapper.findComponent(GoalsEventForm)
    await form.find('input[name="name"]').setValue('Spring Half')
    await form.find('input[name="eventDate"]').setValue('2099-04-12')
    await form.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.findAllComponents(GoalsEventForm)).toHaveLength(0))

    wrapper.unmount()
  })

  it('drops the draft form when the draft is discarded', async () => {
    const wrapper = await mountView({ events: [], goals: [], loading: false, error: null })
    await clickButton(wrapper, 'Add Goal')

    await clickButton(wrapper, 'Remove')

    expect(wrapper.findAllComponents(GoalsGoalForm)).toHaveLength(0)

    wrapper.unmount()
  })

  it('"Edit" reveals the form for an existing row and hides it again', async () => {
    const wrapper = await mountView({
      events: [makeEvent({ id: 'e1', name: 'Boston Marathon' })],
      goals: [],
      loading: false,
      error: null,
    })
    expect(wrapper.findAllComponents(GoalsEventForm)).toHaveLength(0)

    await clickButton(wrapper, 'Edit')
    expect(wrapper.findAllComponents(GoalsEventForm)).toHaveLength(1)

    await clickButton(wrapper, 'Close')
    expect(wrapper.findAllComponents(GoalsEventForm)).toHaveLength(0)

    wrapper.unmount()
  })
})
