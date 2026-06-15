import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import PeaksSection from '@/components/analytics/PeaksSection.vue'
import type { PeakRecord, PeaksResponse } from '@/types/analytics'

function mountWith(peaks: PeaksResponse | null) {
  return mount(PeaksSection, {
    props: { sport: '' },
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {}, initialState: { analytics: { peaks } } })],
    },
  })
}

const recent: PeakRecord = {
  kind: 'Load',
  sport: 'Bike',
  value: 130,
  achievedDate: '2026-06-10',
  achievedWorkoutId: 'a',
  isRecent: true,
  previousValue: 100,
}

const oldPace: PeakRecord = {
  kind: 'Pace',
  sport: 'Run',
  value: 300, // 5:00/km
  achievedDate: '2026-01-01',
  achievedWorkoutId: 'b',
  isRecent: false,
  previousValue: 330,
}

describe('PeaksSection', () => {
  it('shows a delta improvement chip for a recent record with a previous best', () => {
    const wrapper = mountWith({ records: [recent] })
    expect(wrapper.text()).toContain('+30') // 130 − 100
  })

  it('formats pace as m:ss with a /km unit and shows no chip for a non-recent record', () => {
    const wrapper = mountWith({ records: [oldPace] })
    expect(wrapper.text()).toContain('5:00')
    expect(wrapper.text()).toContain('/km')
    // not recent → no improvement chip (the previous-best delta only shows for recent records).
    expect(wrapper.text()).not.toContain('−0:30')
  })

  it('renders an empty state when there are no records', () => {
    const wrapper = mountWith({ records: [] })
    expect(wrapper.text()).toContain('No records yet')
  })
})
