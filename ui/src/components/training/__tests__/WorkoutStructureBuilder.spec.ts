import { describe, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WorkoutStructureBuilder from '@/components/training/WorkoutStructureBuilder.vue'
import type { PlannedSport } from '@/types/training'
import type { ZonesResponse } from '@/types/zones'

function mountBuilder(sport: PlannedSport, initialState?: Record<string, unknown>) {
  return mount(WorkoutStructureBuilder, {
    props: { planId: 'p1', plannedWorkoutId: 'w1', sport, title: 'Test' },
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, initialState })],
    },
    attachTo: document.body,
  })
}

function clickButton(wrapper: VueWrapper, label: string) {
  const btn = wrapper.findAll('button').find((b) => b.text() === label)
  if (!btn) throw new Error(`Button "${label}" not found`)
  return btn.trigger('click')
}

describe('WorkoutStructureBuilder', () => {
  it('adds a block then a step', async () => {
    const wrapper = mountBuilder('Run')
    await flushPromises()
    expect(wrapper.text()).toContain('No blocks yet')

    await clickButton(wrapper, 'Add block')
    expect(wrapper.text()).toContain('Block 1')
    expect(wrapper.text()).toContain('No steps yet')

    await clickButton(wrapper, 'Add step')
    expect(wrapper.text()).toContain('Step 1')
    expect(wrapper.text()).toContain('Intent')
    wrapper.unmount()
  })

  it('renders sets/reps for a strength step and no cardio/zone fields', async () => {
    const wrapper = mountBuilder('Strength')
    await flushPromises()
    await clickButton(wrapper, 'Add block')
    await clickButton(wrapper, 'Add step')

    expect(wrapper.text()).toContain('Sets')
    expect(wrapper.text()).toContain('Reps')
    expect(wrapper.text()).not.toContain('Power low')
    expect(wrapper.text()).not.toContain('Pick a zone')
    wrapper.unmount()
  })

  it('shows the zone picker on a cardio step once zones are loaded', async () => {
    const zones: ZonesResponse = {
      sports: [
        {
          sport: 'Bike',
          metric: 'Power',
          zones: [
            { zoneNumber: 1, lowerBound: 0, upperBound: 137.5, isOverride: false },
            { zoneNumber: 2, lowerBound: 137.5, upperBound: 187.5, isOverride: false },
          ],
        },
      ],
    }
    const wrapper = mountBuilder('Bike', { zones: { zones } })
    await flushPromises()
    await clickButton(wrapper, 'Add block')
    await clickButton(wrapper, 'Add step')

    expect(wrapper.text()).toContain('Zone (pre-fills the range)')
    expect(wrapper.text()).toContain('Power low')
    wrapper.unmount()
  })
})
