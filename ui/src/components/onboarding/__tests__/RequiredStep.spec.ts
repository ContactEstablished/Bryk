import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import RequiredStep from '@/components/onboarding/RequiredStep.vue'

function mountStep() {
  return mount(RequiredStep, {
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {} })],
    },
    attachTo: document.body,
  })
}

describe('RequiredStep', () => {
  it('renders the form heading', () => {
    const wrapper = mountStep()
    expect(wrapper.text()).toContain('Required Information')
    wrapper.unmount()
  })

  it('shows at least one validation error when submitting empty', async () => {
    const wrapper = mountStep()
    const submitButton = wrapper.find('button[type="submit"]')
    expect(submitButton.exists()).toBe(true)

    await submitButton.trigger('click')
    await flushPromises()
    await flushPromises()

    const messages = wrapper.findAll('[data-slot="form-message"]')
    const errorTexts = messages
      .map((m) => m.text().trim())
      .filter((t) => t.length > 0)
    expect(errorTexts.length).toBeGreaterThan(0)
    wrapper.unmount()
  })
})
