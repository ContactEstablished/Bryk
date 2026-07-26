import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import HrvCard from '@/components/dashboard/HrvCard.vue'
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

function mountCard(summary?: WellnessSummaryResponse, loadingSummary = false) {
  return mount(HrvCard, {
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

describe('HrvCard', () => {
  it('renders the 7-day average in ms', () => {
    const wrapper = mountCard(
      makeSummary({ hrvMs: metric({ average: 88.2, daysWithData: 5 }), hasAnyEntries: true }),
    )

    expect(wrapper.text()).toContain('88')
    expect(wrapper.text()).toContain('ms')
    expect(wrapper.text()).toContain('5 days logged')

    wrapper.unmount()
  })

  it('renders a DeltaChip because up is good for HRV', () => {
    const wrapper = mountCard(
      makeSummary({
        hrvMs: metric({ average: 88, priorAverage: 83, delta: 5, daysWithData: 6 }),
        days: [day('2026-07-25', { hrvMs: 86 }), day('2026-07-26', { hrvMs: 90 })],
        hasAnyEntries: true,
      }),
    )

    const chip = wrapper.findComponent(DeltaChip)
    expect(chip.exists()).toBe(true)
    expect(chip.text()).toContain('+5')

    wrapper.unmount()
  })

  it('renders the prompt when nothing is logged', () => {
    const wrapper = mountCard(makeSummary({ hasAnyEntries: false }))

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('Log HRV to see a trend')
    // 0-entry athlete: no sparkline, no fabricated zero.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })
})
