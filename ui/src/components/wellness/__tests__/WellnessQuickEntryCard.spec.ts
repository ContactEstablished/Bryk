import { describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WellnessQuickEntryCard from '@/components/wellness/WellnessQuickEntryCard.vue'
import ScaleSelector from '@/components/common/ScaleSelector.vue'
import { useWellnessStore } from '@/stores/wellness'
import { ApiError } from '@/services/api'
import type { WellnessEntryResponse } from '@/types/wellness'

const entry: WellnessEntryResponse = {
  id: 'w1',
  date: '2026-07-26',
  sleepHours: 7.5,
  sleepQuality: 4,
  restingHr: 48,
  weightKg: 72.4,
  soreness: 3,
  hrvMs: 88,
  notes: null,
}

function mountCard(wellness: Record<string, unknown> = { today: null }) {
  const wrapper = mount(WellnessQuickEntryCard, {
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, initialState: { wellness } })],
    },
    attachTo: document.body,
  })
  return { wrapper, store: useWellnessStore() }
}

async function openForm(wrapper: VueWrapper) {
  const btn = wrapper
    .findAll('button')
    .find((b) => b.text() === 'Log today' || b.text() === 'Edit')
  await btn!.trigger('click')
}

// Every submit assertion below polls with vi.waitFor rather than counting flushPromises ticks. A
// submit over a REFINED zod schema re-validates the whole object and each refine adds a microtask
// hop, so a fixed tick budget is inherently racy here - a fixed 6 flushes proved flaky across runs.
// vi.waitFor is the repo precedent (LogWorkoutForm.spec.ts:35, GoalsGoalForm.spec.ts:33).

describe('WellnessQuickEntryCard', () => {
  it('renders the collapsed prompt when today has no entry', () => {
    const { wrapper } = mountCard()

    expect(wrapper.text()).toContain('No wellness logged today.')
    expect(wrapper.findAll('button').some((b) => b.text() === 'Log today')).toBe(true)
    expect(wrapper.find('input[name="sleepHours"]').exists()).toBe(false)

    wrapper.unmount()
  })

  it("renders today's values in the collapsed summary when an entry exists", () => {
    const { wrapper } = mountCard({ today: entry })

    expect(wrapper.text()).toContain('7.5')
    expect(wrapper.text()).toContain('48')
    expect(wrapper.text()).toContain('72.4')

    wrapper.unmount()
  })

  it('expands to the form when the button is clicked', async () => {
    const { wrapper } = mountCard()

    await openForm(wrapper)

    expect(wrapper.find('input[name="sleepHours"]').exists()).toBe(true)

    wrapper.unmount()
  })

  // Proves the max prop is wired rather than defaulted: 5 for sleep quality, 10 for soreness.
  it('renders a 5-button sleep-quality scale and a 10-button soreness scale', async () => {
    const { wrapper } = mountCard()

    await openForm(wrapper)

    const scales = wrapper.findAllComponents(ScaleSelector)
    expect(scales).toHaveLength(2)
    expect(scales[0].findAll('button')).toHaveLength(5)
    expect(scales[1].findAll('button')).toHaveLength(10)

    wrapper.unmount()
  })

  it('submits the entered metrics through the store', async () => {
    const { wrapper, store } = mountCard()

    await openForm(wrapper)
    await wrapper.find('input[name="sleepHours"]').setValue('7.5')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(store.saveToday).toHaveBeenCalledTimes(1))
    expect(store.saveToday).toHaveBeenCalledWith({
      sleepHours: 7.5,
      sleepQuality: null,
      restingHr: null,
      weightKg: null,
      soreness: null,
      hrvMs: null,
      notes: null,
    })

    wrapper.unmount()
  })

  it('does not submit when every metric is blank', async () => {
    const { wrapper, store } = mountCard()

    await openForm(wrapper)
    await wrapper.find('input[name="notes"]').setValue('felt rough')
    await wrapper.find('form').trigger('submit')

    // Wait for the refine's message to land, then assert the write never happened.
    await vi.waitFor(() => expect(wrapper.text()).toContain('Enter at least one metric'))
    expect(store.saveToday).not.toHaveBeenCalled()

    wrapper.unmount()
  })

  it('maps a field-prefixed server error onto its field', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.saveToday).mockRejectedValue(
      new ApiError(400, 'Bad Request', {
        errors: ['RestingHr: Resting HR must be between 25 and 120 bpm.'],
      }),
    )

    await openForm(wrapper)
    await wrapper.find('input[name="sleepHours"]').setValue('7.5')
    await wrapper.find('form').trigger('submit')

    // The rejection path adds hops beyond a valid submit's: the store call rejects, the catch maps
    // the message onto the field, and only then does vee-validate re-render. Poll, per the
    // LogWorkoutForm.spec.ts:35 / GoalsGoalForm.spec.ts:33 precedent.
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain('Resting HR must be between 25 and 120 bpm.'),
    )

    wrapper.unmount()
  })

  it('renders an unmapped server message in the form-level error', async () => {
    const { wrapper, store } = mountCard()
    vi.mocked(store.saveToday).mockRejectedValue(
      new ApiError(400, 'Bad Request', { errors: ['Entry: At least one metric is required.'] }),
    )

    await openForm(wrapper)
    await wrapper.find('input[name="sleepHours"]').setValue('7.5')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain('Entry: At least one metric is required.'),
    )

    wrapper.unmount()
  })
})
