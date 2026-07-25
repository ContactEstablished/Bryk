import { describe, expect, it } from 'vitest'
import { buildRingGeometry } from '@/lib/progressRing'

describe('buildRingGeometry', () => {
  it('fraction = 0 → dashOffset equals the full circumference (empty ring)', () => {
    const g = buildRingGeometry(0)
    expect(g.dashOffset).toBeCloseTo(g.circumference, 5)
  })

  it('fraction = 1 → dashOffset is 0 (full ring)', () => {
    const g = buildRingGeometry(1)
    expect(g.dashOffset).toBeCloseTo(0, 5)
  })

  it('fraction = 0.5 → dashOffset is half the circumference', () => {
    const g = buildRingGeometry(0.5)
    expect(g.dashOffset).toBeCloseTo(g.circumference / 2, 5)
  })

  it('clamps an overshoot fraction (1.4) to 1', () => {
    const g = buildRingGeometry(1.4)
    expect(g.dashOffset).toBeCloseTo(buildRingGeometry(1).dashOffset, 5)
  })

  it('clamps a negative fraction to 0', () => {
    const g = buildRingGeometry(-0.2)
    expect(g.dashOffset).toBeCloseTo(buildRingGeometry(0).dashOffset, 5)
  })

  it('guards NaN to 0', () => {
    const g = buildRingGeometry(NaN)
    expect(g.dashOffset).toBeCloseTo(buildRingGeometry(0).dashOffset, 5)
  })

  it('guards Infinity to 0', () => {
    const g = buildRingGeometry(Infinity)
    expect(g.dashOffset).toBeCloseTo(buildRingGeometry(0).dashOffset, 5)
  })

  it('ticks length matches the option', () => {
    expect(buildRingGeometry(0.5, { ticks: 12 }).ticks).toHaveLength(12)
  })

  it('defaults to size 160, stroke 8, and 60 ticks', () => {
    const g = buildRingGeometry(0.3)
    expect(g.size).toBe(160)
    expect(g.stroke).toBe(8)
    expect(g.ticks).toHaveLength(60)
  })

  it('derives radius and circumference from size and stroke', () => {
    const g = buildRingGeometry(0.3, { size: 200, stroke: 10 })
    expect(g.cx).toBe(100)
    expect(g.cy).toBe(100)
    expect(g.radius).toBe(95) // size / 2 - stroke / 2
    expect(g.circumference).toBeCloseTo(2 * Math.PI * 95, 5)
    expect(g.dashArray).toBeCloseTo(g.circumference, 5)
  })

  it('never returns NaN in any numeric field', () => {
    for (const f of [0, 0.5, 1, 1.4, -0.2, NaN, Infinity, -Infinity]) {
      const g = buildRingGeometry(f)
      for (const v of [g.size, g.stroke, g.radius, g.cx, g.cy, g.circumference, g.dashArray, g.dashOffset]) {
        expect(Number.isFinite(v)).toBe(true)
      }
      for (const t of g.ticks) {
        for (const v of [t.x1, t.y1, t.x2, t.y2]) {
          expect(Number.isFinite(v)).toBe(true)
        }
      }
    }
  })
})
