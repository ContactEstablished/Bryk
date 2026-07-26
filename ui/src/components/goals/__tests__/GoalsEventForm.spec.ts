import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import GoalsEventForm from '@/components/goals/GoalsEventForm.vue'
import { useGoalsStore } from '@/stores/goals'
import { ApiError } from '@/services/api'
import type { EventListItem } from '@/types/goals'

const existingEvent: EventListItem = {
  id: 'e1',
  name: 'Boston Marathon',
  eventDate: '2099-09-01',
  sport: 'Run',
  triathlonDistance: null,
  customDistanceName: null,
  priority: 'A',
  notes: 'Goal race',
  linkedPlans: [{ id: 'plan-1', name: 'Marathon Build' }],
}

function mountForm(event?: EventListItem) {
  createTestingPinia({ createSpy: vi.fn })
  const wrapper = mount(GoalsEventForm, {
    props: event ? { event } : {},
    attachTo: document.body,
  })
  return { wrapper, store: useGoalsStore() }
}

describe('GoalsEventForm', () => {
  it('creates a draft event through the store and asks the parent to drop the draft', async () => {
    const { wrapper, store } = mountForm()

    await wrapper.find('input[name="name"]').setValue('Spring Half')
    await wrapper.find('input[name="eventDate"]').setValue('2099-04-12')
    await wrapper.find('input[name="notes"]').setValue('Tune-up race')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(store.createEvent).toHaveBeenCalledTimes(1))

    // priority defaults to B; sport is untouched, so the triathlon fields stay null.
    expect(store.createEvent).toHaveBeenCalledWith({
      name: 'Spring Half',
      eventDate: '2099-04-12',
      sport: null,
      triathlonDistance: null,
      customDistanceName: null,
      priority: 'B',
      notes: 'Tune-up race',
    })
    expect(wrapper.emitted('created')).toHaveLength(1)

    wrapper.unmount()
  })

  it('updates an existing event and shows the saved flag', async () => {
    const { wrapper, store } = mountForm(existingEvent)

    await wrapper.find('input[name="name"]').setValue('Boston Marathon 2099')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(store.updateEvent).toHaveBeenCalledWith(
        'e1',
        expect.objectContaining({ name: 'Boston Marathon 2099', priority: 'A' }),
      ),
    )
    await flushPromises()

    expect(wrapper.text()).toContain('Saved')
    // The plan link is display-only — the form never sends it back.
    expect(store.updateEvent).not.toHaveBeenCalledWith('e1', expect.objectContaining({ linkedPlans: expect.anything() }))

    wrapper.unmount()
  })

  it('deletes an existing event through the store', async () => {
    const { wrapper, store } = mountForm(existingEvent)

    const del = wrapper.findAll('button').find((b) => b.text() === 'Delete')
    await del!.trigger('click')

    await vi.waitFor(() => expect(store.deleteEvent).toHaveBeenCalledWith('e1'))

    wrapper.unmount()
  })

  it('surfaces a 404 as a "no longer exists" message', async () => {
    const { wrapper, store } = mountForm(existingEvent)
    vi.mocked(store.updateEvent).mockRejectedValue(new ApiError(404, 'Not Found', null))

    await wrapper.find('input[name="name"]').setValue('Renamed')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain('This event no longer exists — it may have been removed.'),
    )

    wrapper.unmount()
  })

  it('blocks submit when the name is empty', async () => {
    const { wrapper, store } = mountForm()

    await wrapper.find('input[name="eventDate"]').setValue('2099-04-12')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Event name is required'))
    expect(store.createEvent).not.toHaveBeenCalled()

    wrapper.unmount()
  })

  it('blocks submit when the event date is in the past', async () => {
    const { wrapper, store } = mountForm()

    await wrapper.find('input[name="name"]').setValue('Last Year’s Race')
    await wrapper.find('input[name="eventDate"]').setValue('2000-01-01')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Event date must be today or later'))
    expect(store.createEvent).not.toHaveBeenCalled()

    wrapper.unmount()
  })
})
