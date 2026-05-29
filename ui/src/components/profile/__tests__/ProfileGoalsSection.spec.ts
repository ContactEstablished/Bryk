import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ProfileGoalsSection from '@/components/profile/ProfileGoalsSection.vue'
import ProfileEventCard from '@/components/profile/ProfileEventCard.vue'
import ProfileGoalCard from '@/components/profile/ProfileGoalCard.vue'
import type { ProfileGoalsResponse } from '@/types/profile'

const seededGoals: ProfileGoalsResponse = {
  events: [
    {
      id: 'e1',
      name: 'Spring Marathon',
      eventDate: '2099-09-01',
      sport: 'Run',
      triathlonDistance: null,
      customDistanceName: null,
      priority: 'A',
      notes: null,
    },
  ],
  goals: [
    { id: 'g1', type: 'General', description: 'Sub 3 hours', targetDate: '2099-09-01' },
  ],
}

function mountSection() {
  return mount(ProfileGoalsSection, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: { profile: { goals: seededGoals } },
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('ProfileGoalsSection', () => {
  it('renders a card for each existing event and goal, seeded from the store', () => {
    const wrapper = mountSection()

    const eventCards = wrapper.findAllComponents(ProfileEventCard)
    const goalCards = wrapper.findAllComponents(ProfileGoalCard)
    expect(eventCards).toHaveLength(1)
    expect(goalCards).toHaveLength(1)

    // The seeded values flow into each card's form.
    expect((eventCards[0].find('input').element as HTMLInputElement).value).toBe('Spring Marathon')
    expect((goalCards[0].find('input').element as HTMLInputElement).value).toBe('Sub 3 hours')

    wrapper.unmount()
  })

  it('"Add Event" appends a draft event card', async () => {
    const wrapper = mountSection()
    expect(wrapper.findAllComponents(ProfileEventCard)).toHaveLength(1)

    const addEvent = wrapper.findAll('button').find((b) => b.text() === 'Add Event')
    expect(addEvent).toBeTruthy()
    await addEvent!.trigger('click')
    await flushPromises()

    expect(wrapper.findAllComponents(ProfileEventCard)).toHaveLength(2)

    wrapper.unmount()
  })
})
