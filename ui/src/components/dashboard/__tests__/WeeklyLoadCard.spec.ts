import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WeeklyLoadCard from '@/components/dashboard/WeeklyLoadCard.vue'
import type { ThisWeekResponse } from '@/types/training'
import type { PmcResponse } from '@/types/analytics'

function thisWeek(weeklyLoad: number): ThisWeekResponse {
  return { weekStart: '2026-06-08', weekEnd: '2026-06-14', weeklyLoad, plannedWorkouts: [] }
}

function pmcWith(acwr: number | null): PmcResponse {
  return { series: [], current: { date: '2026-06-12', ctl: 0, atl: 0, tsb: 0, acwr } }
}

function mountCard(tw?: ThisWeekResponse, pmc?: PmcResponse) {
  return mount(WeeklyLoadCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: {
            ...(tw ? { training: { thisWeek: tw } } : {}),
            ...(pmc ? { analytics: { pmc } } : {}),
          },
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('WeeklyLoadCard', () => {
  it('renders the weekly load total when loaded', () => {
    const wrapper = mountCard(thisWeek(275))

    expect(wrapper.text()).toContain('275')
    expect(wrapper.text()).toContain('TSS')

    wrapper.unmount()
  })

  it('shows the loading state before this week is fetched', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Loading…')

    wrapper.unmount()
  })

  it('shows an in-band ACWR with the good style', () => {
    const wrapper = mountCard(thisWeek(275), pmcWith(1.1))

    expect(wrapper.text()).toContain('ACWR 1.10')
    expect(wrapper.html()).toContain('text-good')

    wrapper.unmount()
  })

  it('flags an out-of-band ACWR with the warning style', () => {
    const wrapper = mountCard(thisWeek(275), pmcWith(1.6))

    expect(wrapper.text()).toContain('ACWR 1.60')
    expect(wrapper.html()).toContain('text-bad')

    wrapper.unmount()
  })

  it('renders an em dash for ACWR under 28 days of history', () => {
    const wrapper = mountCard(thisWeek(275), pmcWith(null))

    expect(wrapper.text()).toContain('ACWR —')

    wrapper.unmount()
  })
})
