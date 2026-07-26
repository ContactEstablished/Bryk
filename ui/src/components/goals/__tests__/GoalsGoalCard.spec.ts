import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import GoalsGoalCard from '@/components/goals/GoalsGoalCard.vue'
import type { GoalListItem } from '@/types/goals'

function makeGoal(overrides: Partial<GoalListItem> = {}): GoalListItem {
  return {
    id: 'g1',
    type: 'General',
    description: 'Run a sub-3 marathon',
    targetDate: '2099-09-01',
    daysRemaining: 30,
    status: 'Upcoming',
    ...overrides,
  }
}

function mountCard(goal: GoalListItem) {
  return mount(GoalsGoalCard, { props: { goal } })
}

describe('GoalsGoalCard', () => {
  it('renders the description and type pill', () => {
    const wrapper = mountCard(makeGoal())

    expect(wrapper.text()).toContain('Run a sub-3 marathon')
    expect(wrapper.text()).toContain('General')

    wrapper.unmount()
  })

  it('renders the Overdue pill with a past-tense day count', () => {
    const wrapper = mountCard(makeGoal({ status: 'Overdue', daysRemaining: -3 }))

    expect(wrapper.text()).toContain('Overdue')
    expect(wrapper.text()).toContain('3 days ago')

    wrapper.unmount()
  })

  it('renders the Due soon pill with a future day count', () => {
    const wrapper = mountCard(makeGoal({ status: 'DueSoon', daysRemaining: 5 }))

    expect(wrapper.text()).toContain('Due soon')
    expect(wrapper.text()).toContain('in 5 days')

    wrapper.unmount()
  })

  it('renders "today" when the goal is due today', () => {
    const wrapper = mountCard(makeGoal({ status: 'DueSoon', daysRemaining: 0 }))

    expect(wrapper.text()).toContain('today')
    expect(wrapper.text()).not.toContain('in 0 days')

    wrapper.unmount()
  })

  it('renders the Upcoming pill for a distant goal', () => {
    const wrapper = mountCard(makeGoal({ status: 'Upcoming', daysRemaining: 30 }))

    expect(wrapper.text()).toContain('Upcoming')
    expect(wrapper.text()).toContain('in 30 days')

    wrapper.unmount()
  })

  it('renders the No date pill and no day count for an undated goal', () => {
    const wrapper = mountCard(
      makeGoal({ status: 'NoDate', daysRemaining: null, targetDate: null }),
    )

    const text = wrapper.text()
    expect(text).toContain('No date')
    expect(text).toContain('No target date')
    expect(text).not.toContain('days ago')
    expect(text).not.toMatch(/in \d+ day/)

    wrapper.unmount()
  })
})
