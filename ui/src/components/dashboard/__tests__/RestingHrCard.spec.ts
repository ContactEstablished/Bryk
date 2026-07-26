import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import RestingHrCard from '@/components/dashboard/RestingHrCard.vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import Sparkline from '@/components/common/Sparkline.vue'
import type { ProfileRecommendedResponse } from '@/types/profile'
import type {
  WellnessDailyPoint,
  WellnessMetricSummary,
  WellnessSummaryResponse,
} from '@/types/wellness'

function makeRecommended(restingHr: number | null): ProfileRecommendedResponse {
  return { restingHr, maxHr: null, sportThresholds: [] }
}

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

// Pass `undefined` to leave `recommended` null (unfetched → loading state).
function mountCard(recommended?: ProfileRecommendedResponse, summary?: WellnessSummaryResponse) {
  const initialState: Record<string, unknown> = {}
  if (recommended) initialState.profile = { recommended }
  if (summary) initialState.wellness = { summary }

  return mount(RestingHrCard, {
    global: {
      plugins: [
        createTestingPinia({
          createSpy: () => () => {},
          initialState: Object.keys(initialState).length > 0 ? initialState : undefined,
        }),
      ],
      stubs: { RouterLink: RouterLinkStub },
    },
    attachTo: document.body,
  })
}

describe('RestingHrCard', () => {
  it('renders the bpm value when restingHr is set', () => {
    const wrapper = mountCard(makeRecommended(48))

    expect(wrapper.text()).toContain('48')
    expect(wrapper.text()).toContain('bpm')
    // The empty-state link must not render when a value is present.
    expect(wrapper.findComponent(RouterLinkStub).exists()).toBe(false)

    wrapper.unmount()
  })

  it('renders the empty state with a /profile link when restingHr is null', () => {
    const wrapper = mountCard(makeRecommended(null))

    expect(wrapper.text()).toContain('—')
    const link = wrapper.findComponent(RouterLinkStub)
    expect(link.exists()).toBe(true)
    expect(link.props('to')).toBe('/profile')
    expect(link.text()).toBe('Set in profile')

    wrapper.unmount()
  })

  it('shows the loading state before recommended is fetched', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Loading…')
    // Neither value nor empty-state link while still loading.
    expect(wrapper.findComponent(RouterLinkStub).exists()).toBe(false)

    wrapper.unmount()
  })

  it('prefers the wellness 7-day average over the profile value', () => {
    const wrapper = mountCard(
      makeRecommended(55),
      makeSummary({ restingHr: metric({ average: 48.4, daysWithData: 5 }), hasAnyEntries: true }),
    )

    expect(wrapper.text()).toContain('48')
    expect(wrapper.text()).not.toContain('55')

    wrapper.unmount()
  })

  it('falls back to the profile value when the athlete has no wellness entries', () => {
    const wrapper = mountCard(makeRecommended(55), makeSummary({ hasAnyEntries: false }))

    expect(wrapper.text()).toContain('55')
    expect(wrapper.text()).toContain('From profile · log RHR to see a trend')
    // Nothing logged: no sparkline, and no fabricated trend line in the footer.
    expect(wrapper.findComponent(Sparkline).exists()).toBe(false)

    wrapper.unmount()
  })

  it('renders the 7-day change in the footer and never as a DeltaChip', () => {
    // THE INVERTED-METRIC GUARD (ADR-0011 §5). A -2 bpm drop is good news; DeltaChip would colour a
    // `down` direction red, so this tile must not pass the `delta` prop at all.
    const wrapper = mountCard(
      makeRecommended(55),
      makeSummary({
        restingHr: metric({ average: 48, priorAverage: 50, delta: -2, daysWithData: 6 }),
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(DeltaChip).exists()).toBe(false)
    expect(wrapper.text()).toContain('-2 bpm vs prior 7d')

    wrapper.unmount()
  })

  it('renders a sparkline when at least two days carry a resting HR', () => {
    const wrapper = mountCard(
      makeRecommended(55),
      makeSummary({
        restingHr: metric({ average: 48, daysWithData: 2 }),
        days: [day('2026-07-25', { restingHr: 49 }), day('2026-07-26', { restingHr: 47 })],
        hasAnyEntries: true,
      }),
    )

    expect(wrapper.findComponent(Sparkline).exists()).toBe(true)

    wrapper.unmount()
  })
})
