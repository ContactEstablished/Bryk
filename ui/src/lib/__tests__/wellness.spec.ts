import { describe, expect, it } from 'vitest'
import { invertedChange, metricSeries, upIsGoodDelta } from '@/lib/wellness'
import type { WellnessDailyPoint } from '@/types/wellness'

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

describe('metricSeries', () => {
  it('returns only the non-null values, in day order', () => {
    const days = [
      day('2026-07-24', { sleepHours: 7 }),
      day('2026-07-25'),
      day('2026-07-26', { sleepHours: 8 }),
    ]

    expect(metricSeries(days, 'sleepHours')).toEqual([7, 8])
  })

  it('returns an empty array when no day carries the metric', () => {
    expect(metricSeries([day('2026-07-26', { restingHr: 48 })], 'sleepHours')).toEqual([])
  })
})

describe('upIsGoodDelta — ADR-0011 §5, sleep hours and HRV only', () => {
  it('labels a positive delta with a leading + and dir up', () => {
    expect(upIsGoodDelta(0.4)).toEqual({ text: '+0.4', dir: 'up' })
  })

  it('maps a negative delta to dir down', () => {
    expect(upIsGoodDelta(-0.4)).toEqual({ text: '-0.4', dir: 'down' })
  })

  it('maps zero to flat', () => {
    expect(upIsGoodDelta(0)).toEqual({ text: '0.0', dir: 'flat' })
  })

  it('returns null for a null delta', () => {
    expect(upIsGoodDelta(null)).toBeNull()
    expect(upIsGoodDelta(undefined)).toBeNull()
  })
})

describe('invertedChange — ADR-0011 §5, resting HR / weight / soreness', () => {
  // THE ADR-0011 §5 GUARD. If anyone later routes these through DeltaChip, the colours invert
  // (down → text-bad) and this test is the tripwire.
  it('colours a drop as good and a rise as bad', () => {
    expect(invertedChange(-2, 'bpm', 0)).toEqual({
      text: '-2 bpm vs prior 7d',
      className: 'text-good',
    })
    expect(invertedChange(2, 'bpm', 0)).toEqual({
      text: '+2 bpm vs prior 7d',
      className: 'text-bad',
    })
  })

  it('returns null for a null delta', () => {
    expect(invertedChange(null, 'kg', 1)).toBeNull()
  })
})
