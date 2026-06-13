import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAnalyticsStore } from '@/stores/analytics'
import type { PmcPoint } from '@/types/analytics'

function point(date: string, tsb: number): PmcPoint {
  return { date, load: 0, ctl: 0, atl: 0, tsb }
}

describe('useAnalyticsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('current passes through pmc.current', () => {
    const store = useAnalyticsStore()
    expect(store.current).toBeNull()

    store.pmc = { series: [], current: { date: '2026-06-12', ctl: 50, atl: 40, tsb: 10, acwr: 1.1 } }
    expect(store.current?.tsb).toBe(10)
    expect(store.current?.acwr).toBe(1.1)
  })

  it('tsbDeltaVs7d computes a signed delta from the 8th-from-last contiguous day', () => {
    const store = useAnalyticsStore()
    const dates = [
      '2026-06-05', '2026-06-06', '2026-06-07', '2026-06-08',
      '2026-06-09', '2026-06-10', '2026-06-11', '2026-06-12',
    ]
    const tsbs = [5, 6, 7, 8, 9, 10, 11, 12]
    store.pmc = {
      series: dates.map((d, i) => point(d, tsbs[i])),
      current: { date: '2026-06-12', ctl: 0, atl: 0, tsb: 12, acwr: null },
    }
    // last tsb 12, 7-days-ago (index 0) tsb 5 → +7.0, dir up
    expect(store.tsbDeltaVs7d).toEqual({ text: '+7.0', dir: 'up' })
  })

  it('tsbDeltaVs7d is null when fewer than 8 days are in series', () => {
    const store = useAnalyticsStore()
    store.pmc = {
      series: [point('2026-06-11', 5), point('2026-06-12', 8)],
      current: { date: '2026-06-12', ctl: 0, atl: 0, tsb: 8, acwr: null },
    }
    expect(store.tsbDeltaVs7d).toBeNull()
  })

  it('tsbDeltaVs7d is null for a fresh athlete (no current)', () => {
    const store = useAnalyticsStore()
    store.pmc = { series: [], current: null }
    expect(store.tsbDeltaVs7d).toBeNull()
  })
})
