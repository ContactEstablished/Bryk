import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import GoalsEventCard from '@/components/goals/GoalsEventCard.vue'
import ProgressRing from '@/components/common/ProgressRing.vue'
import type { EventListItem } from '@/types/goals'

function makeEvent(overrides: Partial<EventListItem> = {}): EventListItem {
  return {
    id: 'e1',
    name: 'Boston Marathon',
    eventDate: '2099-09-01',
    sport: 'Run',
    triathlonDistance: null,
    customDistanceName: null,
    priority: 'A',
    notes: null,
    linkedPlans: [],
    ...overrides,
  }
}

function mountCard(event: EventListItem) {
  return mount(GoalsEventCard, {
    props: { event },
    global: { stubs: { RouterLink: RouterLinkStub } },
  })
}

describe('GoalsEventCard', () => {
  it('renders the name, sport pill, priority badge and countdown ring', () => {
    const wrapper = mountCard(makeEvent())

    expect(wrapper.text()).toContain('Boston Marathon')
    expect(wrapper.text()).toContain('Run')
    expect(wrapper.findComponent(ProgressRing).exists()).toBe(true)
    expect(wrapper.text()).toContain('weeks')

    const badge = wrapper.get('[aria-label="Priority A"]')
    expect(badge.text()).toBe('A')

    wrapper.unmount()
  })

  it('renders notes inline when present', () => {
    const wrapper = mountCard(makeEvent({ notes: 'Goal: sub-3, negative split' }))

    expect(wrapper.text()).toContain('Goal: sub-3, negative split')

    wrapper.unmount()
  })

  it('renders a linked-plan chip pointing at the plan browser', () => {
    const wrapper = mountCard(
      makeEvent({ linkedPlans: [{ id: 'plan-1', name: 'Marathon Build' }] }),
    )

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links).toHaveLength(1)
    expect(links[0].props('to')).toBe('/plans/plan-1')
    expect(links[0].text()).toContain('Marathon Build')

    wrapper.unmount()
  })

  it('renders no chip when the event has no linked plans', () => {
    const wrapper = mountCard(makeEvent({ linkedPlans: [] }))

    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(0)

    wrapper.unmount()
  })

  it('swaps the ring centre for a headline on race day', () => {
    const now = new Date()
    const today = [
      now.getUTCFullYear(),
      String(now.getUTCMonth() + 1).padStart(2, '0'),
      String(now.getUTCDate()).padStart(2, '0'),
    ].join('-')
    const wrapper = mountCard(makeEvent({ eventDate: today }))

    expect(wrapper.text()).toContain('Today')
    expect(wrapper.text()).not.toContain('weeks')

    wrapper.unmount()
  })

  it('mutes the badge for a C-priority event', () => {
    const wrapper = mountCard(makeEvent({ priority: 'C' }))

    const badge = wrapper.get('[aria-label="Priority C"]')
    expect(badge.text()).toBe('C')
    expect(badge.classes()).toContain('text-faint')

    wrapper.unmount()
  })
})
