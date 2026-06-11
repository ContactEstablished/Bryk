import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import Sparkline from '@/components/common/Sparkline.vue'

describe('Sparkline', () => {
  it('renders an area fill, line, and end dot for a series', () => {
    const wrapper = mount(Sparkline, { props: { data: [10, 25, 18, 40] } })

    expect(wrapper.find('svg').exists()).toBe(true)
    expect(wrapper.findAll('path')).toHaveLength(2)
    expect(wrapper.find('circle').exists()).toBe(true)

    wrapper.unmount()
  })

  it('renders nothing for fewer than two points', () => {
    const wrapper = mount(Sparkline, { props: { data: [42] } })

    expect(wrapper.find('svg').exists()).toBe(false)

    wrapper.unmount()
  })
})
