import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ProfileRecommendedSection from '@/components/profile/ProfileRecommendedSection.vue'

function mountSection() {
  return mount(ProfileRecommendedSection, {
    global: {
      plugins: [createTestingPinia({ createSpy: () => () => {} })],
    },
    attachTo: document.body,
  })
}

describe('ProfileRecommendedSection', () => {
  // Regression guard: the form must render its fixed sport rows before data loads.
  // Without initialValues, the sportThresholds[index] template access threw on mount.
  it('mounts and renders the HR + Bike/Run/Swim rows before data loads', () => {
    const wrapper = mountSection()
    expect(wrapper.text()).toContain('Recommended Profile')
    expect(wrapper.text()).toContain('Bike')
    expect(wrapper.text()).toContain('Run')
    expect(wrapper.text()).toContain('Swim')
    wrapper.unmount()
  })
})
