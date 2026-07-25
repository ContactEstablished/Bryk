import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import PrimaryGoalCard from '@/components/dashboard/PrimaryGoalCard.vue'
import ProgressRing from '@/components/common/ProgressRing.vue'
import type { EventResponse, ProfileGoalsResponse } from '@/types/profile'

function makeEvent(overrides: Partial<EventResponse> & { id: string }): EventResponse {
  return {
    name: 'Event',
    eventDate: '2099-01-01',
    sport: 'Run',
    triathlonDistance: null,
    customDistanceName: null,
    priority: 'B',
    notes: null,
    ...overrides,
  }
}

function mountCard(events: EventResponse[]) {
  const goals: ProfileGoalsResponse = { events, goals: [] }
  return mount(PrimaryGoalCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: { profile: { goals } },
        }),
      ],
      stubs: { RouterLink: RouterLinkStub },
    },
    attachTo: document.body,
  })
}

describe('PrimaryGoalCard', () => {
  it('picks the highest-priority upcoming event (priority trumps proximity)', () => {
    const wrapper = mountCard([
      makeEvent({ id: 'b', name: 'Local 10k', priority: 'B', eventDate: '2099-02-01' }),
      makeEvent({ id: 'a', name: 'Boston Marathon', priority: 'A', eventDate: '2099-09-01' }),
    ])

    // The A-event wins even though the B-event is sooner.
    expect(wrapper.text()).toContain('Boston Marathon')
    expect(wrapper.text()).not.toContain('Local 10k')

    wrapper.unmount()
  })

  it('excludes past events', () => {
    const wrapper = mountCard([
      makeEvent({ id: 'past', name: 'Old Race', priority: 'A', eventDate: '2000-01-01' }),
      makeEvent({ id: 'future', name: 'Next Race', priority: 'C', eventDate: '2099-05-01' }),
    ])

    // The past A-event is excluded; the future C-event surfaces.
    expect(wrapper.text()).toContain('Next Race')
    expect(wrapper.text()).not.toContain('Old Race')

    wrapper.unmount()
  })

  it('renders a ProgressRing with the animated week count for a future event', () => {
    const wrapper = mountCard([
      makeEvent({ id: 'a', name: 'Boston Marathon', priority: 'A', eventDate: '2099-09-01' }),
    ])

    expect(wrapper.findComponent(ProgressRing).exists()).toBe(true)
    expect(wrapper.text()).toContain('weeks to go')
    expect(wrapper.text()).toContain('days')

    wrapper.unmount()
  })

  it('renders "Today" through the ring centre for a same-day event', () => {
    const now = new Date()
    const iso = [
      now.getUTCFullYear(),
      String(now.getUTCMonth() + 1).padStart(2, '0'),
      String(now.getUTCDate()).padStart(2, '0'),
    ].join('-')
    const wrapper = mountCard([makeEvent({ id: 'race', name: 'Race Day', priority: 'A', eventDate: iso })])

    expect(wrapper.findComponent(ProgressRing).exists()).toBe(true)
    expect(wrapper.text()).toContain('Today')
    expect(wrapper.text()).not.toContain('weeks to go')

    wrapper.unmount()
  })

  it('renders the empty state with a Set-a-goal link when there are no upcoming events', () => {
    const wrapper = mountCard([])

    expect(wrapper.text()).toContain('No upcoming events.')
    const link = wrapper.findComponent(RouterLinkStub)
    expect(link.exists()).toBe(true)
    expect(link.props('to')).toBe('/profile')
    expect(link.text()).toBe('Set a goal')

    wrapper.unmount()
  })
})
