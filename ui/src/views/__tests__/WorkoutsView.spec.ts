import { describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WorkoutsView from '@/views/WorkoutsView.vue'
import { useTrainingStore } from '@/stores/training'
import type { WorkoutResponse } from '@/types/training'

function makeWorkout(o: Partial<WorkoutResponse> & { id: string }): WorkoutResponse {
  return {
    plannedWorkoutId: null,
    trainingPlanId: null,
    sport: 'Run',
    completedDate: '2026-06-10',
    actualDurationSeconds: 1800,
    actualDistanceMeters: null,
    avgHr: null,
    maxHr: null,
    computedLoad: null,
    loadOverride: null,
    effectiveLoad: null,
    isLoadOverride: false,
    rpe: null,
    notes: null,
    stepResults: [],
    ...o,
  }
}

function mountView(initial: Record<string, unknown>) {
  return mount(WorkoutsView, {
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, initialState: { training: initial } })],
      stubs: { RouterLink: RouterLinkStub, AppSidebar: true },
    },
    attachTo: document.body,
  })
}

describe('WorkoutsView', () => {
  it('renders a row per workout with sport and key stats', () => {
    const wrapper = mountView({
      workouts: [
        makeWorkout({ id: 'a', sport: 'Bike', effectiveLoad: 80 }),
        makeWorkout({ id: 'b', sport: 'Run' }),
      ],
      workoutsHasMore: false,
    })

    expect(wrapper.text()).toContain('Bike')
    expect(wrapper.text()).toContain('Run')
    expect(wrapper.text()).toContain('80 TSS')

    wrapper.unmount()
  })

  it('shows the empty state when no workouts match', () => {
    const wrapper = mountView({ workouts: [], workoutsHasMore: false })

    expect(wrapper.text()).toContain('No workouts match these filters.')

    wrapper.unmount()
  })

  it('reloads page 1 when a sport filter is selected', async () => {
    const wrapper = mountView({ workouts: [], workoutsHasMore: false })
    const store = useTrainingStore()

    const bikeBtn = wrapper.findAll('button').find((b) => b.text().includes('Bike'))
    expect(bikeBtn).toBeTruthy()
    await bikeBtn!.trigger('click')

    expect(store.loadWorkouts).toHaveBeenCalledWith(
      expect.objectContaining({ sport: 'Bike' }),
    )

    wrapper.unmount()
  })

  it('loads the next page when "Load more" is clicked', async () => {
    const wrapper = mountView({ workouts: [makeWorkout({ id: 'a' })], workoutsHasMore: true })
    const store = useTrainingStore()

    const moreBtn = wrapper.findAll('button').find((b) => b.text() === 'Load more')
    expect(moreBtn).toBeTruthy()
    await moreBtn!.trigger('click')

    expect(store.loadMoreWorkouts).toHaveBeenCalled()

    wrapper.unmount()
  })
})
