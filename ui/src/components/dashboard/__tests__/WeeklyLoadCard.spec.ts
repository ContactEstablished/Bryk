import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WeeklyLoadCard from '@/components/dashboard/WeeklyLoadCard.vue'
import type { ThisWeekResponse } from '@/types/training'

function thisWeek(weeklyLoad: number): ThisWeekResponse {
  return { weekStart: '2026-06-08', weekEnd: '2026-06-14', weeklyLoad, plannedWorkouts: [] }
}

function mountCard(tw?: ThisWeekResponse) {
  return mount(WeeklyLoadCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: tw ? { training: { thisWeek: tw } } : undefined,
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
})
