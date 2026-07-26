import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import PlanDetailView from '@/views/PlanDetailView.vue'
import PeriodizationPanel from '@/components/training/PeriodizationPanel.vue'
import type { TrainingPlanResponse } from '@/types/training'

const stubView = { template: '<div />' }
const routes = [
  { path: '/plans', name: 'plans', component: stubView },
  { path: '/plans/:id', name: 'plan-detail', component: stubView },
]

const plan: TrainingPlanResponse = {
  id: 'p1',
  name: 'Spring Base',
  methodology: 'Polarized',
  startDate: '2026-06-08',
  endDate: '2026-08-03',
  eventId: null,
  buildWeeks: null,
  recoveryWeeks: null,
  recoveryWeekPercentage: null,
  plannedWorkouts: [
    {
      id: 'pw1',
      trainingPlanId: 'p1',
      sport: 'Bike',
      scheduledDate: '2026-06-09',
      title: 'Threshold 4x8',
      description: null,
      plannedDurationMinutes: 70,
      plannedLoad: 72,
      effectiveLoad: 72,
      isLoadOverride: true,
    },
  ],
}

async function mountView() {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push('/plans/p1')
  await router.isReady()
  const wrapper = mount(PlanDetailView, {
    global: {
      plugins: [
        router,
        createTestingPinia({
          createSpy: vi.fn,
          initialState: { training: { currentPlan: plan, weeklyTargets: null }, goals: { events: [] } },
        }),
      ],
      stubs: { AppSidebar: true },
    },
    attachTo: document.body,
  })
  return { wrapper }
}

describe('PlanDetailView', () => {
  it('renders the plan header and its planned workouts', async () => {
    const { wrapper } = await mountView()

    expect(wrapper.text()).toContain('Spring Base')
    expect(wrapper.text()).toContain('Threshold 4x8')
    expect(wrapper.text()).toContain('Bike')

    wrapper.unmount()
  })

  it('renders the periodization panel instead of the read-only header', async () => {
    const { wrapper } = await mountView()

    expect(wrapper.findComponent(PeriodizationPanel).exists()).toBe(true)
    expect(wrapper.text()).toContain('Spring Base')

    wrapper.unmount()
  })

  it('reopens the structure builder for a planned workout', async () => {
    const { wrapper } = await mountView()

    const editBtn = wrapper.findAll('button').find((b) => b.text() === 'Edit structure')
    expect(editBtn).toBeTruthy()
    await editBtn!.trigger('click')

    expect(wrapper.text()).toContain('Build workout')

    wrapper.unmount()
  })
})
