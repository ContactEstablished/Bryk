import { describe, expect, it } from 'vitest'
import { buildPmcGeometry, PMC_DIMS } from '@/lib/charts/pmc'
import type { PmcPoint } from '@/types/analytics'

function point(i: number, ctl: number, atl: number, load = 0): PmcPoint {
  return { date: `2026-06-${String(i + 1).padStart(2, '0')}`, load, ctl, atl, tsb: ctl - atl }
}

describe('buildPmcGeometry', () => {
  it('scales the first/last points to the chart box', () => {
    const series = [point(0, 10, 20), point(1, 30, 25), point(2, 50, 40)]
    const g = buildPmcGeometry(series)

    // maxV = 50 × 1.15 = 57.5; x spans padL..(width-padR); first x = padL, last x = width-padR.
    expect(g.maxV).toBeCloseTo(57.5, 5)
    expect(g.ctlPath.startsWith(`M${PMC_DIMS.padL.toFixed(1)} `)).toBe(true)
    expect(g.endMarkers!.ctl.x).toBeCloseTo(PMC_DIMS.width - PMC_DIMS.padR, 5)
    // CTL line is monotone increasing here → y decreases (SVG y grows downward).
    expect(g.endMarkers!.ctl.y).toBeLessThan(g.endMarkers!.atl.y)
  })

  it('emits one load bar per day and a closed CTL fill for >= 2 points', () => {
    const series = [point(0, 10, 5, 100), point(1, 12, 6, 0)]
    const g = buildPmcGeometry(series)

    expect(g.loadBars).toHaveLength(2)
    expect(g.ctlFillPath.endsWith('Z')).toBe(true)
  })

  it('guards an empty series (no NaN, no markers)', () => {
    const g = buildPmcGeometry([])

    expect(g.maxV).toBe(1)
    expect(g.ctlPath).toBe('')
    expect(g.ctlFillPath).toBe('')
    expect(g.endMarkers).toBeNull()
    expect(g.yTicks.every((t) => Number.isFinite(t.y))).toBe(true)
  })

  it('guards an all-zero series (maxV falls back to 1, no NaN)', () => {
    const g = buildPmcGeometry([point(0, 0, 0), point(1, 0, 0)])

    expect(g.maxV).toBe(1)
    expect(g.ctlPath).not.toContain('NaN')
    expect(g.loadBars.every((b) => Number.isFinite(b.y1))).toBe(true)
  })
})
