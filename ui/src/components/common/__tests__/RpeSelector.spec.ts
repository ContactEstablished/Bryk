import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import RpeSelector from '@/components/common/RpeSelector.vue'

describe('RpeSelector', () => {
  it('renders buttons 1 through 10', () => {
    const wrapper = mount(RpeSelector, { props: { modelValue: null } })

    const labels = wrapper.findAll('button').map((b) => b.text())
    expect(labels).toEqual(['1', '2', '3', '4', '5', '6', '7', '8', '9', '10'])

    wrapper.unmount()
  })

  it('emits the tapped value', async () => {
    const wrapper = mount(RpeSelector, { props: { modelValue: null } })

    const seven = wrapper.findAll('button').find((b) => b.text() === '7')
    await seven!.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[7]])

    wrapper.unmount()
  })

  it('marks the selected value as pressed', () => {
    const wrapper = mount(RpeSelector, { props: { modelValue: 4 } })

    const pressed = wrapper.findAll('button[aria-pressed="true"]')
    expect(pressed).toHaveLength(1)
    expect(pressed[0].text()).toBe('4')

    wrapper.unmount()
  })
})
