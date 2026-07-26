import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import GoalsGoalForm from '@/components/goals/GoalsGoalForm.vue'
import { useGoalsStore } from '@/stores/goals'
import type { GoalListItem } from '@/types/goals'

const existingGoal: GoalListItem = {
  id: 'g1',
  type: 'General',
  description: 'Raise bike FTP to 270 W',
  targetDate: '2099-09-13',
  daysRemaining: 49,
  status: 'Upcoming',
}

function mountForm(goal?: GoalListItem) {
  createTestingPinia({ createSpy: vi.fn })
  const wrapper = mount(GoalsGoalForm, {
    props: goal ? { goal } : {},
    attachTo: document.body,
  })
  return { wrapper, store: useGoalsStore() }
}

describe('GoalsGoalForm', () => {
  it('creates a draft goal through the store and asks the parent to drop the draft', async () => {
    const { wrapper, store } = mountForm()

    await wrapper.find('input[name="description"]').setValue('Swim 3x a week')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(store.createGoal).toHaveBeenCalledTimes(1))

    // An empty target date maps to null, and the editor fixes type to General.
    expect(store.createGoal).toHaveBeenCalledWith({
      type: 'General',
      description: 'Swim 3x a week',
      targetDate: null,
    })
    expect(wrapper.emitted('created')).toHaveLength(1)

    wrapper.unmount()
  })

  it('updates an existing goal, flags it saved, and clears the flag on the next edit', async () => {
    const { wrapper, store } = mountForm(existingGoal)

    await wrapper.find('input[name="description"]').setValue('Raise bike FTP to 285 W')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(store.updateGoal).toHaveBeenCalledWith(
        'g1',
        expect.objectContaining({ type: 'General', description: 'Raise bike FTP to 285 W' }),
      ),
    )
    await flushPromises()
    expect(wrapper.text()).toContain('Saved')

    // Editing again makes the form dirty, which retires the flag.
    await wrapper.find('input[name="description"]').setValue('Raise bike FTP to 290 W')
    await flushPromises()
    expect(wrapper.text()).not.toContain('Saved')

    wrapper.unmount()
  })

  it('deletes an existing goal through the store', async () => {
    const { wrapper, store } = mountForm(existingGoal)

    const del = wrapper.findAll('button').find((b) => b.text() === 'Delete')
    await del!.trigger('click')

    await vi.waitFor(() => expect(store.deleteGoal).toHaveBeenCalledWith('g1'))

    wrapper.unmount()
  })

  it('blocks submit when the description is empty', async () => {
    const { wrapper, store } = mountForm()

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Description is required'))
    expect(store.createGoal).not.toHaveBeenCalled()

    wrapper.unmount()
  })

  it('blocks submit when the target date is in the past', async () => {
    const { wrapper, store } = mountForm()

    await wrapper.find('input[name="description"]').setValue('Backdated goal')
    await wrapper.find('input[name="targetDate"]').setValue('2000-01-01')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Target date must be today or later'))
    expect(store.createGoal).not.toHaveBeenCalled()

    wrapper.unmount()
  })
})
