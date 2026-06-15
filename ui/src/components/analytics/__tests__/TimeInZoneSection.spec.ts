import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import TimeInZoneSection from '@/components/analytics/TimeInZoneSection.vue'
import type { TimeInZoneResponse } from '@/types/analytics'

function mountWith(timeInZone: TimeInZoneResponse | null) {
  return mount(TimeInZoneSection, {
    props: { modelValue: '' },
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {}, initialState: { analytics: { timeInZone } } })],
    },
  })
}

const sample: TimeInZoneResponse = {
  zones: [
    { zoneNumber: 1, seconds: 600 },
    { zoneNumber: 2, seconds: 0 },
    { zoneNumber: 3, seconds: 1800 },
    { zoneNumber: 4, seconds: 480 },
    { zoneNumber: 5, seconds: 0 },
  ],
  methodBreakdown: { structureSeconds: 1080, sessionAvgSeconds: 1800, unclassifiedSeconds: 0 },
  totalSeconds: 2880,
}

describe('TimeInZoneSection', () => {
  it('always renders the "estimated" badge', () => {
    const wrapper = mountWith(sample)
    expect(wrapper.text().toLowerCase()).toContain('estimated')
  })

  it('renders one stacked segment per non-zero zone, sized by its share', () => {
    const wrapper = mountWith(sample)
    const segments = wrapper.findAll('.h-5 > div')

    // zones 1, 3, 4 are positive (zone 2 & 5 are zero), no unclassified remainder → 3 segments.
    expect(segments).toHaveLength(3)
    // zone 1 = 600 / 2880 ≈ 20.83%.
    expect(segments[0].attributes('style')).toContain('20.83')
  })

  it('renders "—" / empty hint when there is no classifiable training', () => {
    const wrapper = mountWith({
      zones: [{ zoneNumber: 1, seconds: 0 }],
      methodBreakdown: { structureSeconds: 0, sessionAvgSeconds: 0, unclassifiedSeconds: 0 },
      totalSeconds: 0,
    })
    expect(wrapper.find('.h-5').exists()).toBe(false)
    expect(wrapper.text()).toContain('—')
  })
})
