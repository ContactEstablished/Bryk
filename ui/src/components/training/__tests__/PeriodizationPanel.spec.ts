import { describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import PeriodizationPanel from '@/components/training/PeriodizationPanel.vue'
import LoadChart from '@/components/charts/LoadChart.vue'
import { useTrainingStore } from '@/stores/training'
import { ApiError } from '@/services/api'
import type {
  TrainingPlanResponse,
  WeeklyTargetsResponse,
  WeeklyTargetWeek,
} from '@/types/training'
import type { EventListItem } from '@/types/goals'

function plan(overrides: Partial<TrainingPlanResponse> = {}): TrainingPlanResponse {
  return {
    id: 'p1',
    name: 'Spring Base',
    methodology: 'Polarized',
    startDate: '2026-06-08',
    endDate: '2026-08-03',
    eventId: null,
    buildWeeks: 3,
    recoveryWeeks: 1,
    recoveryWeekPercentage: 60,
    plannedWorkouts: [],
    ...overrides,
  }
}

function week(overrides: Partial<WeeklyTargetWeek> = {}): WeeklyTargetWeek {
  return {
    weekStart: '2026-06-08',
    targetLoad: 200,
    isRecoveryWeek: false,
    isTaperWeek: false,
    plannedLoad: 0,
    actualLoad: 0,
    ...overrides,
  }
}

function targets(weeks: WeeklyTargetWeek[]): WeeklyTargetsResponse {
  return {
    planId: 'p1',
    startDate: '2026-06-08',
    endDate: '2026-08-03',
    baseline: weeks.length > 0 ? 200 : null,
    baselineSource: weeks.length > 0 ? 'TrailingActual' : 'None',
    weeks,
  }
}

const seededEvents: EventListItem[] = [
  {
    id: 'e1',
    name: 'Indian Wells 70.3',
    eventDate: '2026-07-20',
    sport: 'Triathlon',
    triathlonDistance: 'HalfIronman',
    customDistanceName: null,
    priority: 'A',
    notes: null,
    linkedPlans: [],
  },
]

function mountPanel(
  planOverrides: Partial<TrainingPlanResponse> = {},
  weeklyTargets: WeeklyTargetsResponse | null = targets([week()]),
  events: EventListItem[] | null = seededEvents,
) {
  const p = plan(planOverrides)
  const wrapper = mount(PeriodizationPanel, {
    props: { plan: p },
    global: {
      plugins: [
        createTestingPinia({
          createSpy: vi.fn,
          initialState: {
            training: { currentPlan: p, weeklyTargets, loadingTargets: false },
            goals: { events },
          },
        }),
      ],
    },
  })
  return { wrapper, store: useTrainingStore() }
}

describe('PeriodizationPanel', () => {
  it('renders the plan metadata summary and the cadence line', () => {
    const { wrapper } = mountPanel()

    expect(wrapper.text()).toContain('Spring Base')
    expect(wrapper.text()).toContain('Polarized')
    expect(wrapper.text()).toContain('Jun 8, 2026')
    expect(wrapper.text()).toContain('Aug 3, 2026')
    expect(wrapper.text()).toContain('3 build : 1 recovery')
    expect(wrapper.text()).toContain('60% recovery volume')
  })

  it('renders "No cadence set" when the periodization fields are null', () => {
    const { wrapper } = mountPanel({
      buildWeeks: null,
      recoveryWeeks: null,
      recoveryWeekPercentage: null,
    })

    expect(wrapper.text()).toContain('No cadence set')
  })

  it('renders the linked event name when eventId matches a loaded event, else "No target event"', () => {
    const linked = mountPanel({ eventId: 'e1' })
    expect(linked.wrapper.text()).toContain('Indian Wells 70.3')

    const unlinked = mountPanel({ eventId: null })
    expect(unlinked.wrapper.text()).toContain('No target event')
  })

  it('passes targets to LoadChart on the planned channel with a null band', () => {
    const weeks = [
      week({ weekStart: '2026-06-08', targetLoad: 200, actualLoad: 174.2 }),
      week({ weekStart: '2026-06-15', targetLoad: 214, actualLoad: 0 }),
    ]
    const { wrapper } = mountPanel({}, targets(weeks))

    const chart = wrapper.findComponent(LoadChart)
    expect(chart.exists()).toBe(true)
    expect(chart.props('weeks')).toEqual([
      { weekStart: '2026-06-08', plannedLoad: 200, actualLoad: 174.2, rollingAverage: 200 },
      { weekStart: '2026-06-15', plannedLoad: 214, actualLoad: 0, rollingAverage: 214 },
    ])
    expect(chart.props('optimalBand')).toBeNull()
  })

  it('renders the honest empty state and no chart when baselineSource is None', () => {
    const { wrapper } = mountPanel({}, targets([]))

    expect(wrapper.text()).toContain('No targets yet')
    expect(wrapper.findComponent(LoadChart).exists()).toBe(false)
  })

  it('badges the recovery and taper weeks in the week strip', () => {
    const weeks = [
      week({ weekStart: '2026-06-08' }),
      week({ weekStart: '2026-06-15' }),
      week({ weekStart: '2026-06-22' }),
      week({ weekStart: '2026-06-29', isRecoveryWeek: true }),
      week({ weekStart: '2026-07-06', isTaperWeek: true }),
      week({ weekStart: '2026-07-13', isTaperWeek: true }),
    ]
    const { wrapper } = mountPanel({}, targets(weeks))

    const text = wrapper.text()
    expect(text.match(/Recovery/g)?.length).toBe(1)
    expect(text.match(/Taper/g)?.length).toBe(2)
  })

  it('submits mapped metadata through the store', async () => {
    const { wrapper, store } = mountPanel()

    await wrapper.findAll('button').find((b) => b.text() === 'Edit')!.trigger('click')
    await flushPromises()

    await wrapper.find('input[type="text"], input:not([type])').setValue('Renamed')
    const numberInputs = wrapper.findAll('input[type="number"]')
    await numberInputs[2].setValue('60')

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(store.updatePlan).toHaveBeenCalledWith(
        'p1',
        expect.objectContaining({
          name: 'Renamed',
          recoveryWeekPercentage: 60,
          eventId: null,
        }),
      ),
    )
  })

  it("surfaces the server's plan-window rejection", async () => {
    const { wrapper, store } = mountPanel()
    vi.mocked(store.updatePlan).mockRejectedValue(
      new ApiError(400, 'Bad Request', {
        status: 400,
        error: 'One or more validation errors occurred.',
        errors: ['PlanWindow: 2 planned workout(s) fall outside the requested window.'],
      }),
    )

    await wrapper.findAll('button').find((b) => b.text() === 'Edit')!.trigger('click')
    await flushPromises()

    await wrapper.find('form').trigger('submit')
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain('PlanWindow: 2 planned workout(s) fall outside the requested window.'),
    )

    // The form stays open so the athlete can correct the window.
    expect(wrapper.find('form').exists()).toBe(true)
  })

  it('rejects a recovery percentage below 30 without calling the store', async () => {
    const { wrapper, store } = mountPanel()

    await wrapper.findAll('button').find((b) => b.text() === 'Edit')!.trigger('click')
    await flushPromises()

    const numberInputs = wrapper.findAll('input[type="number"]')
    await numberInputs[2].setValue('20')

    await wrapper.find('form').trigger('submit')

    // vee-validate resolves the zod schema across several microtask turns before the message paints.
    await vi.waitFor(() => expect(wrapper.text()).toContain('Must be at least 30'))
    expect(store.updatePlan).not.toHaveBeenCalled()
  })
})
