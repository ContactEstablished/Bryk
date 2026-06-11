import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import WorkoutDetailView from '@/views/WorkoutDetailView.vue'
import { useTrainingStore } from '@/stores/training'

const stubView = { template: '<div />' }
const routes = [
  { path: '/workouts', name: 'workouts', component: stubView },
  { path: '/workouts/:id', name: 'workout-detail', component: stubView },
]

const workout = {
  id: 'w1',
  plannedWorkoutId: 'pw1',
  trainingPlanId: 'tp1',
  sport: 'Bike',
  completedDate: '2026-06-10',
  actualDurationSeconds: 3600,
  actualDistanceMeters: 30000,
  avgHr: 150,
  maxHr: 175,
  computedLoad: 80,
  loadOverride: null,
  effectiveLoad: 80,
  isLoadOverride: false,
  rpe: 6,
  notes: 'Felt strong',
  stepResults: [
    {
      id: 'r1',
      workoutStepId: 's1',
      orderIndex: 0,
      actualDurationSeconds: 600,
      actualDistanceMeters: null,
      avgPower: 210,
      avgHr: 155,
      avgPace: null,
      rpe: 6,
    },
  ],
}

const structure = {
  id: 'pw1',
  trainingPlanId: 'tp1',
  sport: 'Bike',
  scheduledDate: '2026-06-10',
  title: 'Threshold',
  description: null,
  plannedDurationMinutes: null,
  plannedLoad: null,
  blocks: [
    {
      id: 'b1',
      orderIndex: 0,
      repeats: 1,
      steps: [
        {
          id: 's1',
          orderIndex: 0,
          intent: 'Work',
          title: 'Interval',
          durationSeconds: 600,
          distanceMeters: null,
          targetZone: 4,
          targetPowerLow: 200,
          targetPowerHigh: 220,
          targetHrLow: null,
          targetHrHigh: null,
          targetPaceLow: null,
          targetPaceHigh: null,
          sets: null,
          reps: null,
          loadKg: null,
          rpe: null,
        },
      ],
    },
  ],
}

async function mountView() {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push('/workouts/w1')
  await router.isReady()
  const wrapper = mount(WorkoutDetailView, {
    global: {
      plugins: [
        router,
        createTestingPinia({
          createSpy: vi.fn,
          initialState: { training: { currentWorkout: workout, structure } },
        }),
      ],
      stubs: { AppSidebar: true },
    },
    attachTo: document.body,
  })
  return { wrapper, store: useTrainingStore(), router }
}

describe('WorkoutDetailView', () => {
  it('renders the metric strip, planned-vs-actual (incl. AvgPower) and notes', async () => {
    const { wrapper } = await mountView()

    expect(wrapper.text()).toContain('Bike')
    expect(wrapper.text()).toContain('Load')
    expect(wrapper.text()).toContain('80') // effective load
    expect(wrapper.text()).toContain('Planned vs. actual')
    expect(wrapper.text()).toContain('Interval') // planned step label
    expect(wrapper.text()).toContain('210') // AvgPower, surfaced for the first time
    expect(wrapper.text()).toContain('Felt strong') // Notes

    wrapper.unmount()
  })

  it('reveals the edit form in edit mode when Edit is clicked', async () => {
    const { wrapper } = await mountView()

    const editBtn = wrapper.findAll('button').find((b) => b.text() === 'Edit')
    expect(editBtn).toBeTruthy()
    await editBtn!.trigger('click')

    expect(wrapper.text()).toContain('Save changes')

    wrapper.unmount()
  })

  it('deletes after confirming', async () => {
    const { wrapper, store } = await mountView()

    const deleteBtn = wrapper.findAll('button').find((b) => b.text() === 'Delete')
    await deleteBtn!.trigger('click')

    const confirmBtn = wrapper.findAll('button').find((b) => b.text() === 'Confirm')
    expect(confirmBtn).toBeTruthy()
    await confirmBtn!.trigger('click')

    expect(store.deleteWorkout).toHaveBeenCalledWith('w1')

    wrapper.unmount()
  })
})
