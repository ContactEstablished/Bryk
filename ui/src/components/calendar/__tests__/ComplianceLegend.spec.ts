import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ComplianceLegend from '@/components/calendar/ComplianceLegend.vue'

describe('ComplianceLegend', () => {
  it('renders 5 entries with correct labels', () => {
    const wrapper = mount(ComplianceLegend)

    const text = wrapper.text()
    expect(text).toContain('On target')
    expect(text).toContain('Under/over')
    expect(text).toContain('Missed')
    expect(text).toContain('Scheduled')
    expect(text).toContain('Unplanned')

    wrapper.unmount()
  })

  it('renders colored dots with the correct Tailwind classes', () => {
    const wrapper = mount(ComplianceLegend)

    const html = wrapper.html()
    expect(html).toContain('bg-emerald-500')
    expect(html).toContain('bg-amber-400')
    expect(html).toContain('bg-rose-500')
    expect(html).toContain('bg-slate-400')

    wrapper.unmount()
  })

  it('does not render dots for the unplanned entry', () => {
    const wrapper = mount(ComplianceLegend)

    // The unplanned entry uses a bordered tag, not a dot.
    const dots = wrapper.findAll('.rounded-full')
    expect(dots.length).toBe(4) // Green, Yellow, Red, Grey only

    wrapper.unmount()
  })

  it('hides labels in compact mode', () => {
    const wrapper = mount(ComplianceLegend, { props: { compact: true } })

    const text = wrapper.text()
    // Only the unplanned tag text remains (no labels)
    expect(text).toContain('Unplanned')
    expect(text).not.toContain('On target')

    wrapper.unmount()
  })
})
