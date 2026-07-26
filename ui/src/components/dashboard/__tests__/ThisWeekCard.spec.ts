import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ThisWeekCard from '@/components/dashboard/ThisWeekCard.vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import type { PlannedWorkoutResponse, ThisWeekResponse } from '@/types/training'

function makeWorkout(
  overrides: Partial<PlannedWorkoutResponse> & { id: string },
): PlannedWorkoutResponse {
  return {
    trainingPlanId: 'plan-1',
    sport: 'Run',
    scheduledDate: '2099-01-06',
    title: 'Session',
    description: null,
    plannedDurationMinutes: null,
    plannedLoad: null,
    ...overrides,
  }
}

function mountCard(thisWeek: ThisWeekResponse | null) {
  return mount(ThisWeekCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: { training: { thisWeek } },
        }),
      ],
      stubs: { RouterLink: RouterLinkStub },
    },
    attachTo: document.body,
  })
}

describe('ThisWeekCard', () => {
  it('renders this week\'s planned workouts (titles and sports)', () => {
    const thisWeek: ThisWeekResponse = {
      weekStart: '2099-01-05',
      weekEnd: '2099-01-11',
      plannedWorkouts: [
        makeWorkout({ id: '1', title: 'Easy Run', sport: 'Run', scheduledDate: '2099-01-06', plannedDurationMinutes: 45 }),
        makeWorkout({ id: '2', title: 'Squat Session', sport: 'Strength', scheduledDate: '2099-01-08' }),
      ],
    }
    const wrapper = mountCard(thisWeek)

    expect(wrapper.text()).toContain('Easy Run')
    expect(wrapper.text()).toContain('Run')
    expect(wrapper.text()).toContain('Squat Session')
    expect(wrapper.text()).toContain('Strength')

    wrapper.unmount()
  })

  it('renders the static empty state with no link when there are no sessions', () => {
    const thisWeek: ThisWeekResponse = {
      weekStart: '2099-01-05',
      weekEnd: '2099-01-11',
      plannedWorkouts: [],
    }
    const wrapper = mountCard(thisWeek)

    expect(wrapper.text()).toContain('No sessions planned this week.')
    expect(wrapper.findComponent(RouterLinkStub).exists()).toBe(false)

    wrapper.unmount()
  })

  it('shows Loading… before data is present', () => {
    const wrapper = mountCard(null)

    expect(wrapper.text()).toContain('Loading…')

    wrapper.unmount()
  })

  // ── Phase 18: target vs actual ─────────────

  it('renders the target-vs-actual bar when a target is present', () => {
    const thisWeek: ThisWeekResponse = {
      weekStart: '2099-01-05',
      weekEnd: '2099-01-11',
      targetLoad: 300,
      actualLoad: 240,
      plannedWorkouts: [],
    }
    const wrapper = mountCard(thisWeek)

    expect(wrapper.text()).toContain('240 / 300 TSS')
    expect(wrapper.text()).toContain('-60 TSS')

    const bar = wrapper.find('[role="progressbar"]')
    expect(bar.exists()).toBe(true)
    expect(bar.attributes('aria-valuenow')).toBe('80')

    const fill = bar.find('div')
    expect(fill.classes()).toContain('bg-good')
    expect(fill.attributes('style')).toContain('width: 80%')

    wrapper.unmount()
  })

  it('flips the bar state when the athlete falls behind', () => {
    const thisWeek: ThisWeekResponse = {
      weekStart: '2099-01-05',
      weekEnd: '2099-01-11',
      targetLoad: 300,
      actualLoad: 100,
      plannedWorkouts: [],
    }
    const wrapper = mountCard(thisWeek)

    const fill = wrapper.find('[role="progressbar"]').find('div')
    expect(fill.classes()).toContain('bg-bad')
    expect(wrapper.findComponent(DeltaChip).props('dir')).toBe('down')

    wrapper.unmount()
  })

  it('renders no bar at all when targetLoad is null', () => {
    const thisWeek: ThisWeekResponse = {
      weekStart: '2099-01-05',
      weekEnd: '2099-01-11',
      plannedWorkouts: [makeWorkout({ id: '1', title: 'Easy Run' })],
    }
    const wrapper = mountCard(thisWeek)

    expect(wrapper.find('[role="progressbar"]').exists()).toBe(false)
    expect(wrapper.findComponent(DeltaChip).exists()).toBe(false)

    wrapper.unmount()
  })
})
