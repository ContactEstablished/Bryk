import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import TrainingView from '@/views/TrainingView.vue'

function mountView() {
  return mount(TrainingView, {
    global: {
      plugins: [
        createTestingPinia({ createSpy: () => () => {} }),
      ],
      // AppSidebar needs a live router (useRoute); stub it — the form under test doesn't.
      stubs: { RouterLink: RouterLinkStub, AppSidebar: true },
    },
    attachTo: document.body,
  })
}

describe('TrainingView', () => {
  it('renders the create-plan form', () => {
    const wrapper = mountView()

    expect(wrapper.text()).toContain('Create Training Plan')
    expect(wrapper.find('input').exists()).toBe(true)
    const submit = wrapper.findAll('button').find((b) => b.text() === 'Create Plan')
    expect(submit).toBeTruthy()

    wrapper.unmount()
  })

  it('appends a planned-workout draft row when Add is clicked', async () => {
    const wrapper = mountView()

    expect(wrapper.text()).toContain('No planned workouts added yet.')

    const addButton = wrapper.findAll('button').find((b) => b.text() === 'Add Planned Workout')
    expect(addButton).toBeTruthy()
    await addButton!.trigger('click')

    expect(wrapper.text()).toContain('Workout 1')

    wrapper.unmount()
  })
})
