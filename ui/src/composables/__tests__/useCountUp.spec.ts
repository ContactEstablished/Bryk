import { describe, expect, it } from 'vitest'
import { defineComponent, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { useCountUp } from '@/composables/useCountUp'

// The test-setup matchMedia stub reports prefers-reduced-motion, so the
// composable snaps to its target synchronously — no rAF ticks to await.
const Host = defineComponent({
  props: {
    target: { type: Number, default: null },
  },
  setup(props) {
    const display = useCountUp(() => props.target)
    return { display }
  },
  template: '<span>{{ display }}</span>',
})

describe('useCountUp', () => {
  it('snaps to the target under reduced motion', () => {
    const wrapper = mount(Host, { props: { target: 275 } })

    expect(wrapper.text()).toBe('275')

    wrapper.unmount()
  })

  it('renders empty for a null target', () => {
    const wrapper = mount(Host)

    expect(wrapper.text()).toBe('')

    wrapper.unmount()
  })

  it('tracks target changes', async () => {
    const wrapper = mount(Host, { props: { target: 10 } })

    await wrapper.setProps({ target: 42 })
    await nextTick()
    expect(wrapper.text()).toBe('42')

    wrapper.unmount()
  })
})
