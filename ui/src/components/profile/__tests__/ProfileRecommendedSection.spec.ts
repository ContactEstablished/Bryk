import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ProfileRecommendedSection from '@/components/profile/ProfileRecommendedSection.vue'
import type { ProfileRecommendedResponse } from '@/types/profile'

const seededRecommended: ProfileRecommendedResponse = {
  restingHr: null,
  maxHr: null,
  sportThresholds: [],
}

function mountSection(recommended?: ProfileRecommendedResponse) {
  return mount(ProfileRecommendedSection, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: recommended ? { profile: { recommended } } : undefined,
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('ProfileRecommendedSection', () => {
  it('shows the loading state before data loads', () => {
    const wrapper = mountSection()
    expect(wrapper.text()).toContain('Loading…')
    wrapper.unmount()
  })

  // The fixed initialValues sport rows remain the crash backstop for the
  // sportThresholds[index] template access; once data is present the form renders them.
  it('renders the Bike/Run/Swim rows once data loads', () => {
    const wrapper = mountSection(seededRecommended)
    expect(wrapper.text()).toContain('Bike')
    expect(wrapper.text()).toContain('Run')
    expect(wrapper.text()).toContain('Swim')
    wrapper.unmount()
  })
})
