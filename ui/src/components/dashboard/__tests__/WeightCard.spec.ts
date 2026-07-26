import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import WeightCard from '@/components/dashboard/WeightCard.vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import Sparkline from '@/components/common/Sparkline.vue'
import type { ProfileRequiredResponse } from '@/types/profile'
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

// A distinctive profile weight, seeded ONLY so the last spec can prove the tile ignores it.
const profileRequired: ProfileRequiredResponse = {
  name: 'Test Athlete',
  gender: 'Female',
  dateOfBirth: '1992-06-15',
  heightCm: 170,
  weightKg: 81.7,
  yearsTraining: 4,
  typicalWeeklyHours: 9,
  methodology: 'Polarized',
}

function mountCard(
  summary?: WellnessSummaryResponse,
  loadingSummary = false,
  required?: ProfileRequiredResponse,
) {
  return mount(WeightCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: {
            wellness: { summary: summary ?? null, loadingSummary },
            ...(required ? { profile: { required } } : {}),
          },
        }),
      ],
    },
    attachTo: document.body,
  })
}

describe('WeightCard', () => {
  it('renders the 7-day average in kg', () => {
    const wrapper = mountCard(
      makeSummary({
        weightKg: metric({ average: 72.43, daysWithData: 5 }),
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.text()).toContain('72.4')
    expect(wrapper.text()).toContain('kg')

    wrapper.unmount()
  })

  it('renders the change in the footer, not as a DeltaChip', () => {
    // ADR-0011 §5: losing weight is good news, so this tile passes no `delta` prop.
    const wrapper = mountCard(
      makeSummary({
        weightKg: metric({ average: 72.4, priorAverage: 73.0, delta: -0.6, daysWithData: 6 }),
        days: [day('2026-07-25', { weightKg: 72.6 }), day('2026-07-26', { weightKg: 72.2 })],
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(DeltaChip).exists()).toBe(false)
    expect(wrapper.text()).toContain('-0.6 kg vs prior 7d')

    wrapper.unmount()
  })

  it('renders the prompt and no value when nothing is logged', () => {
    const wrapper = mountCard(makeSummary({ hasAnyEntries: false }), false, profileRequired)

    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('Log weight to see a trend')
    // The deliberate asymmetry with Resting HR: no fallback to Athlete.WeightKg.
    expect(wrapper.text()).not.toContain('81.7')
    // 0-entry athlete: no sparkline, no fabricated number.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })
})
