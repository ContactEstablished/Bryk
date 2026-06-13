import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import FormCard from '@/components/dashboard/FormCard.vue'
import type { PmcResponse } from '@/types/analytics'

function mountCard(pmc?: PmcResponse) {
  return mount(FormCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: pmc ? { analytics: { pmc } } : undefined,
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('FormCard', () => {
  it('renders a signed TSB and its interpretation when current is present', () => {
    const wrapper = mountCard({
      series: [],
      current: { date: '2026-06-12', ctl: 60, atl: 45, tsb: 15, acwr: 1.0 },
    })

    expect(wrapper.text()).toContain('+15')
    expect(wrapper.text()).toContain('Fresh')

    wrapper.unmount()
  })

  it('labels a negative TSB as Fatigued', () => {
    const wrapper = mountCard({
      series: [],
      current: { date: '2026-06-12', ctl: 30, atl: 55, tsb: -25, acwr: null },
    })

    expect(wrapper.text()).toContain('-25')
    expect(wrapper.text()).toContain('Fatigued')

    wrapper.unmount()
  })

  it('renders an em dash and no band label for a fresh athlete (current null)', () => {
    const wrapper = mountCard({ series: [], current: null })

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).not.toContain('Fresh')
    expect(wrapper.text()).not.toContain('Neutral')
    expect(wrapper.text()).toContain('Log a workout')

    wrapper.unmount()
  })
})
