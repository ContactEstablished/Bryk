import { describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import LogWorkoutForm from '@/components/training/LogWorkoutForm.vue'
import { useTrainingStore } from '@/stores/training'

function mountForm() {
  createTestingPinia({ createSpy: vi.fn })
  const wrapper = mount(LogWorkoutForm, { attachTo: document.body })
  return { wrapper, store: useTrainingStore() }
}

function clickButton(wrapper: VueWrapper, label: string) {
  const btn = wrapper.findAll('button').find((b) => b.text() === label)
  if (!btn) throw new Error(`Button "${label}" not found`)
  return btn.trigger('click')
}

describe('LogWorkoutForm', () => {
  it('adds a per-step actuals row', async () => {
    const { wrapper } = mountForm()
    expect(wrapper.text()).toContain('No per-step actuals')

    await clickButton(wrapper, 'Add step result')
    expect(wrapper.text()).toContain('Step result 1')

    wrapper.unmount()
  })

  it('submitting a valid session logs through the store', async () => {
    const { wrapper, store } = mountForm()

    // Date defaults to today and all other fields are optional, so the form is valid as-is.
    await wrapper.find('button[type="submit"]').trigger('click')
    await vi.waitFor(() => expect(store.logWorkout).toHaveBeenCalledTimes(1))

    wrapper.unmount()
  })
})
