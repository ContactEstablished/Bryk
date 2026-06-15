import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import ProgressView from '@/views/ProgressView.vue'
import LoadChartSection from '@/components/charts/LoadChartSection.vue'

const stub = { template: '<div />' }

async function mountView(path = '/progress') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: stub },
      { path: '/progress', name: 'progress', component: ProgressView },
    ],
  })
  await router.push(path)
  await router.isReady()
  const wrapper = mount(ProgressView, {
    global: {
      plugins: [router, createTestingPinia({ createSpy: () => () => {} })],
    },
  })
  return { wrapper, router }
}

describe('ProgressView', () => {
  it('renders all four section headings', async () => {
    const { wrapper } = await mountView()
    const text = wrapper.text()

    expect(text).toContain('Weekly Training Load')
    expect(text).toContain('Performance Manager')
    expect(text).toContain('Time in Zone')
    expect(text).toContain('Personal Records')
  })

  it('shows "—" headline tiles for a fresh athlete (no current summary)', async () => {
    const { wrapper } = await mountView()

    expect(wrapper.text()).toContain('Fitness · CTL')
    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('Need 28 days') // ACWR honesty footer
  })

  it('defaults invalid query toggles and writes valid ones back', async () => {
    const { wrapper, router } = await mountView('/progress?pmc=bogus&weeks=999')

    // invalid values fall back to defaults (3m / 8 weeks); the view does not crash.
    expect(wrapper.text()).toContain('Performance Manager')

    // changing the weeks toggle writes ?weeks= back to the URL.
    wrapper.findComponent(LoadChartSection).vm.$emit('update:modelValue', 12)
    await flushPromises()
    expect(router.currentRoute.value.query.weeks).toBe('12')
  })
})
