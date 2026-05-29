import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ProfileRequiredSection from '@/components/profile/ProfileRequiredSection.vue'

function mountSection() {
  return mount(ProfileRequiredSection, {
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {} })],
    },
    attachTo: document.body,
  })
}

describe('ProfileRequiredSection', () => {
  it('renders the section heading', () => {
    const wrapper = mountSection()
    expect(wrapper.text()).toContain('Required Information')
    wrapper.unmount()
  })

  it('shows at least one validation error when submitting empty', async () => {
    const wrapper = mountSection()
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
