import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ProfileRequiredSection from '@/components/profile/ProfileRequiredSection.vue'
import type { ProfileRequiredResponse } from '@/types/profile'

const seededRequired: ProfileRequiredResponse = {
  name: 'Alice',
  gender: 'Male',
  dateOfBirth: '1990-05-15',
  heightCm: 180,
  weightKg: 75,
  yearsTraining: 5,
  typicalWeeklyHours: 8,
  methodology: 'Polarized',
}

function mountSection(required?: ProfileRequiredResponse) {
  return mount(ProfileRequiredSection, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: required ? { profile: { required } } : undefined,
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('ProfileRequiredSection', () => {
  it('renders the section heading', () => {
    const wrapper = mountSection()
    expect(wrapper.text()).toContain('Required Information')
    wrapper.unmount()
  })

  it('shows the loading state before data loads', () => {
    const wrapper = mountSection()
    expect(wrapper.text()).toContain('Loading…')
    wrapper.unmount()
  })

  it('renders the form once data is present', () => {
    const wrapper = mountSection(seededRequired)
    expect(wrapper.find('button[type="submit"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Name')
    wrapper.unmount()
  })
})
