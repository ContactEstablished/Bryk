import { describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WorkoutsView from '@/views/WorkoutsView.vue'
import { useTrainingStore } from '@/stores/training'
import { useActivityFilesStore } from '@/stores/activityFiles'
import { MAX_UPLOAD_BYTES } from '@/services/activityFiles'
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

function mountView(initial: Record<string, unknown>, activityFiles: Record<string, unknown> = {}) {
  return mount(WorkoutsView, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: vi.fn,
          initialState: { training: initial, activityFiles },
        }),
      ],
      stubs: { RouterLink: RouterLinkStub, AppSidebar: true },
      mocks: { $router: { push: vi.fn() } },
    },
    attachTo: document.body,
  })
}

const previewFixture = {
  id: 'f1',
  fileName: 'ride.tcx',
  format: 'Tcx' as const,
  byteSize: 2048,
  parsed: {
    sport: 'Bike' as const,
    completedDate: '2026-06-02',
    startTimeUtc: '2026-06-02T06:00:00Z',
    durationSeconds: 3600,
    distanceMeters: 30000,
    avgHr: 141,
    maxHr: 150,
    avgPower: 210,
    avgPace: null,
    sampleCount: 4,
  },
  computedLoad: 110.25,
  zoneSeconds: [{ zoneNumber: 3, seconds: 1200 }],
  matchCandidates: [],
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

  it('renders the import drop zone when there is no preview', () => {
    const wrapper = mountView({ workouts: [], workoutsHasMore: false }, { preview: null })

    expect(wrapper.text()).toContain('Import file')
    expect(wrapper.find('input[type="file"]').exists()).toBe(true)

    wrapper.unmount()
  })

  it('rejects an unsupported extension without calling upload', async () => {
    const wrapper = mountView({ workouts: [], workoutsHasMore: false }, { preview: null })
    const importStore = useActivityFilesStore()

    const input = wrapper.find('input[type="file"]')
    Object.defineProperty(input.element, 'files', {
      value: [new File(['x'], 'ride.csv')],
      configurable: true,
    })
    await input.trigger('change')

    expect(importStore.upload).not.toHaveBeenCalled()
    expect(importStore.uploadError).toContain('.fit')

    wrapper.unmount()
  })

  it('rejects a file over the size cap without calling upload', async () => {
    const wrapper = mountView({ workouts: [], workoutsHasMore: false }, { preview: null })
    const importStore = useActivityFilesStore()

    const tooBig = new File(['x'], 'ride.tcx')
    Object.defineProperty(tooBig, 'size', { value: MAX_UPLOAD_BYTES + 1 })
    const input = wrapper.find('input[type="file"]')
    Object.defineProperty(input.element, 'files', { value: [tooBig], configurable: true })
    await input.trigger('change')

    expect(importStore.upload).not.toHaveBeenCalled()
    expect(importStore.uploadError).toContain('25 MB')

    wrapper.unmount()
  })

  it('renders ImportReviewCard instead of the drop zone when a preview exists', () => {
    const wrapper = mountView(
      { workouts: [], workoutsHasMore: false },
      { preview: previewFixture },
    )

    expect(wrapper.text()).toContain('Review import')
    expect(wrapper.text()).not.toContain('Import file')

    wrapper.unmount()
  })
})
