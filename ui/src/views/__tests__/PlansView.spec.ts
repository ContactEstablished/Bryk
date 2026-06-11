import { describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import PlansView from '@/views/PlansView.vue'
import type { TrainingPlanResponse } from '@/types/training'

function makePlan(o: Partial<TrainingPlanResponse> & { id: string; name: string }): TrainingPlanResponse {
  return {
    methodology: 'Polarized',
    startDate: '2026-06-08',
    endDate: '2026-08-03',
    eventId: null,
    buildWeeks: null,
    recoveryWeeks: null,
    recoveryWeekPercentage: null,
    plannedWorkouts: [],
    ...o,
  }
}

function mountView(plans: TrainingPlanResponse[] | null) {
  return mount(PlansView, {
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, initialState: { training: { plans } } })],
      stubs: { RouterLink: RouterLinkStub, AppSidebar: true },
    },
    attachTo: document.body,
  })
}

describe('PlansView', () => {
  it('lists the athlete plans with a New plan affordance', () => {
    const wrapper = mountView([
      makePlan({ id: 'p1', name: 'Spring Base' }),
      makePlan({ id: 'p2', name: 'Race Build' }),
    ])

    expect(wrapper.text()).toContain('Spring Base')
    expect(wrapper.text()).toContain('Race Build')
    expect(wrapper.text()).toContain('New plan')

    wrapper.unmount()
  })

  it('shows the empty state when there are no plans', () => {
    const wrapper = mountView([])

    expect(wrapper.text()).toContain('No plans yet.')

    wrapper.unmount()
  })
})
