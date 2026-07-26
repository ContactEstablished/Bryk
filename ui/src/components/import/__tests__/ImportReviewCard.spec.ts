import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ImportReviewCard from '@/components/import/ImportReviewCard.vue'
import { useActivityFilesStore } from '@/stores/activityFiles'
import type { ActivityFileUploadResponse } from '@/types/activityFiles'

const preview: ActivityFileUploadResponse = {
  id: 'f1',
  fileName: 'ride.tcx',
  format: 'Tcx',
  byteSize: 2048,
  parsed: {
    sport: 'Bike',
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
  zoneSeconds: [
    { zoneNumber: 1, seconds: 0 },
    { zoneNumber: 2, seconds: 0 },
    { zoneNumber: 3, seconds: 1200 },
    { zoneNumber: 4, seconds: 600 },
    { zoneNumber: 5, seconds: 0 },
  ],
  matchCandidates: [],
}

function mountCard(initial: Record<string, unknown> = {}) {
  const wrapper = mount(ImportReviewCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: vi.fn,
          initialState: { activityFiles: { preview, ...initial } },
        }),
      ],
    },
  })
  return { wrapper, store: useActivityFilesStore() }
}

describe('ImportReviewCard', () => {
  it('renders load, duration, distance and avg HR from the preview', () => {
    const { wrapper } = mountCard()

    // MetricTile's count-up renders a numeric value as a rounded integer — the app-wide convention for
    // load display, shared with workout detail. The exact 110.25 is pinned server-side instead.
    expect(wrapper.text()).toContain('110')
    expect(wrapper.text()).toContain('1:00:00')
    expect(wrapper.text()).toContain('30.0 km')
    expect(wrapper.text()).toContain('141')
    expect(wrapper.text()).toContain('ride.tcx')
  })

  it('Confirm emits committed with the workout id returned by the store', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.commit).mockResolvedValue('w9')

    await wrapper.findAll('button')[0].trigger('click')
    await Promise.resolve()

    expect(wrapper.emitted('committed')?.[0]).toEqual(['w9'])
  })

  it('Discard emits cancelled', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.discard).mockResolvedValue(undefined)

    await wrapper.findAll('button')[1].trigger('click')
    await Promise.resolve()

    expect(wrapper.emitted('cancelled')).toBeTruthy()
  })

  it('disables both buttons while committing', () => {
    const { wrapper } = mountCard({ committing: true })

    const buttons = wrapper.findAll('button')
    expect(buttons[0].attributes('disabled')).toBeDefined()
    expect(buttons[1].attributes('disabled')).toBeDefined()
  })

  it('renders commitError when the store has one', () => {
    const { wrapper } = mountCard({ commitError: 'This activity file has already been committed.' })

    expect(wrapper.text()).toContain('already been committed')
  })
})
