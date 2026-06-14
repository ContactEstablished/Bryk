import { describe, expect, it } from 'vitest'
import { buildLoadGeometry } from '@/lib/charts/load'
import type { WeeklyLoadWeek } from '@/types/analytics'

function week(i: number, planned: number, actual: number, rolling = actual): WeeklyLoadWeek {
  return { weekStart: `2026-05-${String(i + 1).padStart(2, '0')}`, plannedLoad: planned, actualLoad: actual, rollingAverage: rolling }
}

describe('buildLoadGeometry', () => {
  it('sizes planned and actual bar heights in proportion to their values', () => {
    const weeks = [week(0, 200, 100)]
    const g = buildLoadGeometry(weeks, null)

    // planned (200) is twice actual (100) → twice the height.
    expect(g.bars[0].plannedRect.height).toBeCloseTo(2 * g.bars[0].actualRect.height, 5)
  })

  it('flags only the last week as current', () => {
    const weeks = [week(0, 100, 90), week(1, 110, 95), week(2, 120, 130)]
    const g = buildLoadGeometry(weeks, null)

    expect(g.bars.map((b) => b.weekLabel.isCurrent)).toEqual([false, false, true])
    expect(g.bars[2].weekLabel.text).toContain('NOW')
  })

  it('produces a band rect when a band is given and none when null', () => {
    const weeks = [week(0, 100, 100, 100)]

    expect(buildLoadGeometry(weeks, { lower: 80, upper: 130 }).bandRect).not.toBeNull()
    expect(buildLoadGeometry(weeks, null).bandRect).toBeNull()
  })

  it('builds a trend path following rollingAverage', () => {
    const weeks = [week(0, 0, 0, 50), week(1, 0, 0, 60)]
    const g = buildLoadGeometry(weeks, null)

    expect(g.trendPath.startsWith('M')).toBe(true)
    expect(g.trendDots).toHaveLength(2)
    // higher rollingAverage → smaller y (SVG y grows downward).
    expect(g.trendDots[1].y).toBeLessThan(g.trendDots[0].y)
  })

  it('guards an empty series (no NaN)', () => {
    const g = buildLoadGeometry([], null)

    expect(g.maxV).toBe(1)
    expect(g.bars).toHaveLength(0)
    expect(g.trendPath).toBe('')
    expect(g.yTicks.every((t) => Number.isFinite(t.y))).toBe(true)
  })
})
