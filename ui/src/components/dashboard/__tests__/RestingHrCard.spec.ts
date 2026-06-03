import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import RestingHrCard from '@/components/dashboard/RestingHrCard.vue'
import type { ProfileRecommendedResponse } from '@/types/profile'

function makeRecommended(restingHr: number | null): ProfileRecommendedResponse {
  return { restingHr, maxHr: null, sportThresholds: [] }
}

// Pass `undefined` to leave `recommended` null (unfetched → loading state).
function mountCard(recommended?: ProfileRecommendedResponse) {
  return mount(RestingHrCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: recommended ? { profile: { recommended } } : undefined,
        }),
      ],
      stubs: { RouterLink: RouterLinkStub },
    },
    attachTo: document.body,
  })
}

describe('RestingHrCard', () => {
  it('renders the bpm value when restingHr is set', () => {
    const wrapper = mountCard(makeRecommended(48))

    expect(wrapper.text()).toContain('48')
    expect(wrapper.text()).toContain('bpm')
    // The empty-state link must not render when a value is present.
    expect(wrapper.findComponent(RouterLinkStub).exists()).toBe(false)

    wrapper.unmount()
  })

  it('renders the empty state with a /profile link when restingHr is null', () => {
    const wrapper = mountCard(makeRecommended(null))

    expect(wrapper.text()).toContain('—')
    const link = wrapper.findComponent(RouterLinkStub)
    expect(link.exists()).toBe(true)
    expect(link.props('to')).toBe('/profile')
    expect(link.text()).toBe('Set in profile')

    wrapper.unmount()
  })

  it('shows the loading state before recommended is fetched', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Loading…')
    // Neither value nor empty-state link while still loading.
    expect(wrapper.findComponent(RouterLinkStub).exists()).toBe(false)

    wrapper.unmount()
  })
})
