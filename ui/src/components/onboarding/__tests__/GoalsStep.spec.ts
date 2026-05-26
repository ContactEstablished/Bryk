import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import GoalsStep from '@/components/onboarding/GoalsStep.vue'

function mountStep() {
  return mount(GoalsStep, {
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {} })],
    },
    attachTo: document.body,
  })
}

describe('GoalsStep', () => {
  it('renders heading and the empty-state messages', () => {
    const wrapper = mountStep()
    expect(wrapper.text()).toContain('Set Your Goals')
    expect(wrapper.text()).toContain('No events added yet.')
    expect(wrapper.text()).toContain('No goals added yet.')
    wrapper.unmount()
  })

  it('adds an event row when Add Event is clicked', async () => {
    const wrapper = mountStep()

    const addEventButton = wrapper
      .findAll('button')
      .find((b) => b.text().trim() === 'Add Event')
    expect(addEventButton).toBeDefined()
    await addEventButton!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Event 1')
    expect(wrapper.text()).toContain('Name')
    expect(wrapper.text()).toContain('Date')
    wrapper.unmount()
  })
})
