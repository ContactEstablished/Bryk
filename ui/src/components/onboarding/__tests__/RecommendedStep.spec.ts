import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import RecommendedStep from '@/components/onboarding/RecommendedStep.vue'

function mountStep() {
  return mount(RecommendedStep, {
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {} })],
    },
    attachTo: document.body,
  })
}

describe('RecommendedStep', () => {
  it('renders heading and the three sport rows', () => {
    const wrapper = mountStep()
    expect(wrapper.text()).toContain('Recommended Profile')
    expect(wrapper.text()).toContain('Bike')
    expect(wrapper.text()).toContain('Run')
    expect(wrapper.text()).toContain('Swim')
    wrapper.unmount()
  })

  it('reveals threshold sub-fields after activating a sport checkbox', async () => {
    const wrapper = mountStep()
    // Before activation, no threshold input labelled FTP exists.
    expect(wrapper.text()).not.toContain('FTP (watts)')

    const bikeCheckbox = wrapper.find('button[role="checkbox"]')
    expect(bikeCheckbox.exists()).toBe(true)
    await bikeCheckbox.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('FTP (watts)')
    wrapper.unmount()
  })
})
