import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import AppSidebar from '@/components/layout/AppSidebar.vue'

const stubView = { template: '<div />' }
const routes = [
  { path: '/', name: 'home', component: stubView },
  { path: '/zones', name: 'zones', component: stubView },
  { path: '/training', name: 'training', component: stubView },
  { path: '/plans', name: 'plans', component: stubView },
  { path: '/workouts', name: 'workouts', component: stubView },
  { path: '/progress', name: 'progress', component: stubView },
  { path: '/profile', name: 'profile', component: stubView },
]

async function mountSidebar(path = '/', profileName?: string) {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push(path)
  await router.isReady()
  return mount(AppSidebar, {
    global: {
      plugins: [
        router,
        createTestingPinia({
          createSpy: () => () => {},
          initialState: profileName
            ? { profile: { required: { name: profileName, methodology: 'Polarized' } } }
            : undefined,
        }),
      ],
    },
  })
}

describe('AppSidebar', () => {
  it('marks the item matching the current route as active', async () => {
    const wrapper = await mountSidebar('/zones')

    const active = wrapper.findAll('[aria-current="page"]')
    expect(active.length).toBeGreaterThan(0)
    expect(active.every((el) => el.text().includes('Zones'))).toBe(true)

    wrapper.unmount()
  })

  it('renders Workouts and Progress as live links and keeps Goals inert/soon', async () => {
    const wrapper = await mountSidebar()

    const links = wrapper.findAll('a').map((a) => a.text())
    expect(links.some((t) => t.includes('Workouts'))).toBe(true)
    expect(links.some((t) => t.includes('Progress'))).toBe(true)
    expect(links.some((t) => t.includes('Goals'))).toBe(false)
    expect(wrapper.text()).toContain('Goals')
    expect(wrapper.text()).toContain('soon')

    wrapper.unmount()
  })

  it('shows athlete initials and name in the footer', async () => {
    const wrapper = await mountSidebar('/', 'Maya Chen')

    expect(wrapper.text()).toContain('MC')
    expect(wrapper.text()).toContain('Maya Chen')

    wrapper.unmount()
  })
})
