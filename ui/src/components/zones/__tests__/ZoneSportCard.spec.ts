import { describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ZoneSportCard from '@/components/zones/ZoneSportCard.vue'
import { useZonesStore } from '@/stores/zones'
import type { SportZonesResponse } from '@/types/zones'

function bikeZones(isOverride = false): SportZonesResponse {
  return {
    sport: 'Bike',
    metric: 'Power',
    zones: [
      { zoneNumber: 1, lowerBound: 0, upperBound: 137.5, isOverride },
      { zoneNumber: 2, lowerBound: 137.5, upperBound: 187.5, isOverride },
      { zoneNumber: 3, lowerBound: 187.5, upperBound: 225, isOverride },
    ],
  }
}

function mountCard(sport: SportZonesResponse) {
  createTestingPinia({ createSpy: vi.fn })
  const wrapper = mount(ZoneSportCard, {
    props: { sport },
    attachTo: document.body,
  })
  return { wrapper, store: useZonesStore() }
}

describe('ZoneSportCard', () => {
  it('renders a row per computed zone with the sport and metric', () => {
    const { wrapper } = mountCard(bikeZones())
    expect(wrapper.text()).toContain('Bike')
    expect(wrapper.text()).toContain('watts')
    expect(wrapper.text()).toContain('Z1')
    expect(wrapper.text()).toContain('Z3')
    // Two number inputs (lower + upper) per zone.
    expect(wrapper.findAll('input[type="number"]')).toHaveLength(6)
    wrapper.unmount()
  })

  it('submitting saves the sport overrides through the store', async () => {
    const { wrapper, store } = mountCard(bikeZones())

    await wrapper.find('button[type="submit"]').trigger('click')
    for (let i = 0; i < 6; i++) await flushPromises()

    expect(store.saveZones).toHaveBeenCalledTimes(1)
    expect(store.saveZones).toHaveBeenCalledWith('Bike', expect.objectContaining({ zones: expect.any(Array) }))
    wrapper.unmount()
  })

  it('reset is enabled only when overridden and calls the store', async () => {
    const { wrapper, store } = mountCard(bikeZones(true))
    const resetButton = wrapper.findAll('button').find((b) => b.text().includes('Reset'))!

    expect(resetButton.attributes('disabled')).toBeUndefined()
    await resetButton.trigger('click')
    await flushPromises()

    expect(store.resetZones).toHaveBeenCalledWith('Bike')
    wrapper.unmount()
  })
})
