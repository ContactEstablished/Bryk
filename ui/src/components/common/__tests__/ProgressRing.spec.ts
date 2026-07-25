import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ProgressRing from '@/components/common/ProgressRing.vue'
import { buildRingGeometry } from '@/lib/progressRing'

describe('ProgressRing', () => {
  it('renders the track, ticks, and progress arc with the expected dashoffset', () => {
    const wrapper = mount(ProgressRing, { props: { fraction: 0.5 } })

    const expected = buildRingGeometry(0.5, { size: 160 })

    const circles = wrapper.findAll('circle')
    expect(circles.length).toBeGreaterThanOrEqual(2) // track + arc
    expect(wrapper.findAll('line')).toHaveLength(expected.ticks.length)

    const arc = circles.find((c) => c.classes().includes('progress-ring-arc'))
    expect(arc).toBeTruthy()
    expect(Number(arc!.attributes('stroke-dashoffset'))).toBeCloseTo(expected.dashOffset, 5)
    expect(Number(arc!.attributes('stroke-dasharray'))).toBeCloseTo(expected.circumference, 5)

    wrapper.unmount()
  })

  it('renders centerValue and centerLabel by default', () => {
    const wrapper = mount(ProgressRing, {
      props: { fraction: 0.3, centerValue: 12, centerLabel: 'weeks to go' },
    })

    expect(wrapper.text()).toContain('12')
    expect(wrapper.text()).toContain('weeks to go')

    wrapper.unmount()
  })

  it('renders the #center slot override instead of the default', () => {
    const wrapper = mount(ProgressRing, {
      props: { fraction: 0.3, centerValue: 12, centerLabel: 'weeks to go' },
      slots: { center: '<span>Tomorrow</span>' },
    })

    expect(wrapper.text()).toContain('Tomorrow')
    expect(wrapper.text()).not.toContain('weeks to go')

    wrapper.unmount()
  })

  it('drops the draw-in class when animate is false', () => {
    const wrapper = mount(ProgressRing, { props: { fraction: 0.5, animate: false } })

    const arc = wrapper.findAll('circle').find((c) => c.classes().includes('progress-ring-arc'))
    expect(arc!.classes()).not.toContain('progress-ring-arc--animate')

    wrapper.unmount()
  })

  it('honours a custom size', () => {
    const wrapper = mount(ProgressRing, { props: { fraction: 1, size: 96 } })

    const expected = buildRingGeometry(1, { size: 96 })
    const arc = wrapper.findAll('circle').find((c) => c.classes().includes('progress-ring-arc'))
    expect(Number(arc!.attributes('r'))).toBeCloseTo(expected.radius, 5)
    expect(wrapper.find('svg').attributes('viewBox')).toBe('0 0 96 96')

    wrapper.unmount()
  })
})
