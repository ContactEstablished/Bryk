import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import LoadChart from '@/components/charts/LoadChart.vue'
import type { WeeklyLoadWeek } from '@/types/analytics'

function week(i: number, planned: number, actual: number): WeeklyLoadWeek {
  return { weekStart: `2026-05-${String(i + 1).padStart(2, '0')}`, plannedLoad: planned, actualLoad: actual, rollingAverage: actual }
}

describe('LoadChart', () => {
  const weeks = [week(0, 300, 280), week(1, 320, 340), week(2, 300, 290)]

  it('renders a planned + actual rect per week and the trend path', () => {
    const wrapper = mount(LoadChart, { props: { weeks, optimalBand: null } })

    // 2 rects per week (planned hatch + actual fill), no band rect here.
    expect(wrapper.findAll('rect').length).toBe(weeks.length * 2)
    expect(wrapper.findAll('path').length).toBeGreaterThanOrEqual(1) // trend path
    expect(wrapper.findAll('circle').length).toBe(weeks.length) // trend dots
  })

  it('renders the optimal-band rect only when a band is provided', () => {
    const withBand = mount(LoadChart, { props: { weeks, optimalBand: { lower: 250, upper: 380 } } })
    const without = mount(LoadChart, { props: { weeks, optimalBand: null } })

    // band adds one rect (+ 2 per week) → 7 vs 6.
    expect(withBand.findAll('rect').length).toBe(weeks.length * 2 + 1)
    expect(without.findAll('rect').length).toBe(weeks.length * 2)
    expect(withBand.text()).toContain('OPTIMAL BAND')
  })

  it('renders an empty state for no weeks', () => {
    const wrapper = mount(LoadChart, { props: { weeks: [], optimalBand: null } })

    expect(wrapper.find('svg').exists()).toBe(false)
    expect(wrapper.text()).toContain('No weekly load yet')
  })
})
