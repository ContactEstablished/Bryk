import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import MatchCandidateList from '@/components/import/MatchCandidateList.vue'
import type { MatchCandidate } from '@/types/activityFiles'

function candidate(o: Partial<MatchCandidate> & { plannedWorkoutId: string }): MatchCandidate {
  return {
    trainingPlanId: 'tp1',
    title: 'Easy 30',
    sport: 'Run',
    scheduledDate: '2026-06-01',
    plannedLoad: 50,
    dayOffset: 0,
    ...o,
  }
}

function mountList(candidates: MatchCandidate[], modelValue: string | null = null) {
  return mount(MatchCandidateList, { props: { candidates, modelValue } })
}

describe('MatchCandidateList', () => {
  it('renders one radio per candidate plus the "No planned workout" option', () => {
    const wrapper = mountList([
      candidate({ plannedWorkoutId: 'pw1' }),
      candidate({ plannedWorkoutId: 'pw2', title: 'Tempo' }),
    ])

    expect(wrapper.findAll('input[type="radio"]')).toHaveLength(3)
    expect(wrapper.text()).toContain('No planned workout')
  })

  it('labels dayOffset as Same day / −1 day / +1 day', () => {
    const wrapper = mountList([
      candidate({ plannedWorkoutId: 'pw1', dayOffset: 0 }),
      candidate({ plannedWorkoutId: 'pw2', dayOffset: -1 }),
      candidate({ plannedWorkoutId: 'pw3', dayOffset: 1 }),
    ])

    expect(wrapper.text()).toContain('Same day')
    expect(wrapper.text()).toContain('−1 day')
    expect(wrapper.text()).toContain('+1 day')
  })

  it('emits update:modelValue with the candidate id on select, and null for the no-match option', async () => {
    const wrapper = mountList([candidate({ plannedWorkoutId: 'pw1' })])
    const radios = wrapper.findAll('input[type="radio"]')

    await radios[0].trigger('change')
    await radios[1].trigger('change')

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted?.[0]).toEqual(['pw1'])
    expect(emitted?.[1]).toEqual([null])
  })

  it('renders only the no-match option with a hint when candidates is empty', () => {
    const wrapper = mountList([])

    expect(wrapper.findAll('input[type="radio"]')).toHaveLength(1)
    expect(wrapper.text()).toContain('No planned session within a day of this file.')
  })
})
