import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import AppShell from '@/components/layout/AppShell.vue'

function mountShell(props: Record<string, unknown> = {}, slots: Record<string, string> = {}) {
  return mount(AppShell, {
    props: { title: 'Dashboard', ...props },
    slots: { default: '<p>Body content</p>', ...slots },
    global: {
      stubs: { AppSidebar: true },
    },
  })
}

describe('AppShell', () => {
  it('renders the title, subtitle, and content slot', () => {
    const wrapper = mountShell({ subtitle: 'Thu, Jun 11 · Week 24' })

    expect(wrapper.find('h1').text()).toBe('Dashboard')
    expect(wrapper.text()).toContain('Thu, Jun 11 · Week 24')
    expect(wrapper.text()).toContain('Body content')

    wrapper.unmount()
  })

  it('omits the subtitle line when not provided', () => {
    const wrapper = mountShell()

    expect(wrapper.find('h1').text()).toBe('Dashboard')
    expect(wrapper.find('p').exists()).toBe(true) // slot content only
    expect(wrapper.findAll('p')).toHaveLength(1)

    wrapper.unmount()
  })

  it('renders the actions slot in the topbar', () => {
    const wrapper = mountShell({}, { actions: '<button>Log Workout</button>' })

    const actionButton = wrapper.findAll('button').find((b) => b.text() === 'Log Workout')
    expect(actionButton).toBeTruthy()

    wrapper.unmount()
  })
})
