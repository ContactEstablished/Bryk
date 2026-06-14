import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PMCChart from '@/components/charts/PMCChart.vue'
import type { PmcPoint } from '@/types/analytics'

function point(i: number, ctl: number, atl: number, load = 10): PmcPoint {
  return { date: `2026-06-${String(i + 1).padStart(2, '0')}`, load, ctl, atl, tsb: ctl - atl }
}

describe('PMCChart', () => {
  it('renders CTL + ATL paths and a load bar per day for >= 2 points', () => {
    const series = [point(0, 10, 8), point(1, 12, 9), point(2, 14, 10)]
    const wrapper = mount(PMCChart, { props: { series } })

    expect(wrapper.find('svg').exists()).toBe(true)
    expect(wrapper.findAll('path').length).toBeGreaterThanOrEqual(3) // ctl fill, ctl line, atl line
    expect(wrapper.findAll('line').length).toBeGreaterThanOrEqual(series.length) // load bars + axis
    expect(wrapper.findAll('circle').length).toBe(2) // ctl + atl end markers
  })

  it('renders an empty state for fewer than 2 points', () => {
    const wrapper = mount(PMCChart, { props: { series: [point(0, 10, 8)] } })

    expect(wrapper.find('svg').exists()).toBe(false)
    expect(wrapper.text()).toContain('Not enough history')
  })
})
