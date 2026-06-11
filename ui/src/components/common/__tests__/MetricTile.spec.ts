import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import MetricTile from '@/components/common/MetricTile.vue'

describe('MetricTile', () => {
  it('renders label, value, unit, and delta', () => {
    const wrapper = mount(MetricTile, {
      props: {
        label: 'Weekly Load',
        value: 275,
        unit: 'TSS',
        delta: { text: '+15.7%', dir: 'up' as const },
      },
    })

    expect(wrapper.text()).toContain('Weekly Load')
    expect(wrapper.text()).toContain('275')
    expect(wrapper.text()).toContain('TSS')
    expect(wrapper.text()).toContain('+15.7%')

    wrapper.unmount()
  })

  it('renders string values verbatim', () => {
    const wrapper = mount(MetricTile, { props: { label: 'Sleep Avg', value: '7h 24m' } })

    expect(wrapper.text()).toContain('7h 24m')

    wrapper.unmount()
  })

  it('shows the loading state', () => {
    const wrapper = mount(MetricTile, { props: { label: 'Weekly Load', loading: true } })

    expect(wrapper.text()).toContain('Loading…')

    wrapper.unmount()
  })

  it('shows a dimmed dash and soon chip in placeholder mode', () => {
    const wrapper = mount(MetricTile, { props: { label: 'Form (TSB)', placeholder: true } })

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('soon')

    wrapper.unmount()
  })

  it('renders the footer slot when no sparkline is shown', () => {
    const wrapper = mount(MetricTile, {
      props: { label: 'Resting HR', value: null },
      slots: { footer: '<a href="/profile">Set in profile</a>' },
    })

    expect(wrapper.text()).toContain('Set in profile')

    wrapper.unmount()
  })
})
