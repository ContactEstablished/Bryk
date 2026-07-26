import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import SleepCard from '@/components/dashboard/SleepCard.vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import Sparkline from '@/components/common/Sparkline.vue'
import type {
  WellnessDailyPoint,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

function metric(over: Partial<WellnessMetricSummary> = {}): WellnessMetricSummary {
  return { average: null, priorAverage: null, delta: null, daysWithData: 0, ...over }
}

function day(date: string, over: Partial<WellnessDailyPoint> = {}): WellnessDailyPoint {
  return {
    date,
    sleepHours: null,
    sleepQuality: null,
    restingHr: null,
    weightKg: null,
    soreness: null,
    hrvMs: null,
    ...over,
  }
}

function makeSummary(over: Partial<WellnessSummaryResponse> = {}): WellnessSummaryResponse {
  return {
    to: '2026-07-26',
    from: '2026-07-20',
    priorFrom: '2026-07-13',
    sleepHours: metric(),
    sleepQuality: metric(),
    restingHr: metric(),
    weightKg: metric(),
    soreness: metric(),
    hrvMs: metric(),
    days: [],
    hasAnyEntries: false,
    ...over,
  }
}

// Pass `undefined` for summary to leave the store unfetched.
function mountCard(summary?: WellnessSummaryResponse, loadingSummary = false) {
  return mount(SleepCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: { wellness: { summary: summary ?? null, loadingSummary } },
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('SleepCard', () => {
  it('renders the 7-day average with the h unit', () => {
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.46, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.text()).toContain('7.5')
    expect(wrapper.text()).toContain('h')
    expect(wrapper.text()).toContain('6 nights logged')

    wrapper.unmount()
  })

  it('renders a DeltaChip for sleep hours', () => {
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.5, delta: 0.4, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    const chip = wrapper.findComponent(DeltaChip)
    expect(chip.exists()).toBe(true)
    expect(chip.text()).toContain('+0.4')

    wrapper.unmount()
  })

  it('renders a sparkline when at least two nights are logged', () => {
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.5, daysWithData: 2 }),
        days: [day('2026-07-25', { sleepHours: 7 }), day('2026-07-26', { sleepHours: 8 })],
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(Sparkline).exists()).toBe(true)

    wrapper.unmount()
  })

  it('renders no sparkline with a single night', () => {
    // MetricTile.vue:80 / Sparkline.vue:45 — fewer than two points renders nothing. The 1-entry athlete
    // gets a number and no line: never a padded series, never a flat baseline.
    const wrapper = mountCard(
      makeSummary({
        sleepHours: metric({ average: 7.5, daysWithData: 1 }),
        days: [day('2026-07-26', { sleepHours: 7.5 })],
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.text()).toContain('7.5')
    expect(wrapper.text()).toContain('1 night logged')
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })

  it('renders an em dash and the prompt when nothing is logged', () => {
    const wrapper = mountCard(makeSummary({ hasAnyEntries: false }))

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('Log sleep to see your 7-day average')
    // The 0-entry athlete: no sparkline, no fabricated zero.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)
    expect(wrapper.text()).not.toContain('0 nights')

    wrapper.unmount()
  })

  it('shows the loading state before the summary arrives', () => {
    const wrapper = mountCard(undefined, true)

    expect(wrapper.text()).toContain('Loading…')

    wrapper.unmount()
  })
})
