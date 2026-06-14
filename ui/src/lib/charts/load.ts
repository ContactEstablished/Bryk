import type { OptimalBand, WeeklyLoadWeek } from '@/types/analytics'

// Ported from the design export's charts.jsx LoadChart viewBox.
export const LOAD_DIMS = { width: 720, height: 280, padL: 44, padR: 24, padT: 24, padB: 36 }

export interface Rect {
  x: number
  y: number
  width: number
  height: number
}

export interface LoadBar {
  plannedRect: Rect
  actualRect: Rect
  valueLabel: { x: number; y: number; text: string; isCurrent: boolean }
  weekLabel: { x: number; y: number; text: string; isCurrent: boolean }
}

export interface LoadGeometry {
  bars: LoadBar[]
  trendPath: string
  trendDots: { x: number; y: number }[]
  yTicks: { value: number; y: number }[]
  bandRect: Rect | null
  maxV: number
}

// 'YYYY-MM-DD' → 'M/D' without Date (no timezone shift).
function shortWeek(weekStart: string): string {
  const [, m, d] = weekStart.split('-')
  return `${Number(m)}/${Number(d)}`
}

// Pure: weekly-load weeks + optimal band → all the SVG geometry (planned/actual bars, the rolling-average
// trend line, y-ticks, the optimal-band rect). No DOM, no Vue. The server already supplies rollingAverage
// and the band, so this only positions them — no recompute. Guards an empty / all-zero series (no NaN).
export function buildLoadGeometry(
  weeks: WeeklyLoadWeek[],
  band: OptimalBand | null,
  dims = LOAD_DIMS,
): LoadGeometry {
  const innerW = dims.width - dims.padL - dims.padR
  const innerH = dims.height - dims.padT - dims.padB
  const n = weeks.length

  const candidates = weeks.flatMap((w) => [w.actualLoad, w.plannedLoad])
  if (band) candidates.push(band.upper)
  const rawMax = candidates.length ? Math.max(...candidates) : 0
  const maxV = rawMax > 0 ? rawMax * 1.1 : 1

  const slot = n > 0 ? innerW / n : innerW
  const bw = slot * 0.46
  const x = (i: number): number => dims.padL + (i + 0.5) * slot
  const y = (v: number): number => dims.padT + innerH - (v / maxV) * innerH
  const heightOf = (v: number): number => (v / maxV) * innerH

  const bars: LoadBar[] = weeks.map((w, i) => {
    const isCurrent = i === n - 1
    const cx = x(i)
    return {
      plannedRect: { x: cx - bw / 2, y: y(w.plannedLoad), width: bw, height: heightOf(w.plannedLoad) },
      actualRect: { x: cx - bw / 2, y: y(w.actualLoad), width: bw, height: heightOf(w.actualLoad) },
      valueLabel: { x: cx, y: y(w.actualLoad) - 8, text: Math.round(w.actualLoad).toString(), isCurrent },
      weekLabel: { x: cx, y: dims.height - 14, text: shortWeek(w.weekStart) + (isCurrent ? ' · NOW' : ''), isCurrent },
    }
  })

  const trendPath = weeks
    .map((w, i) => (i ? 'L' : 'M') + x(i).toFixed(1) + ' ' + y(w.rollingAverage).toFixed(1))
    .join(' ')
  const trendDots = weeks.map((w, i) => ({ x: x(i), y: y(w.rollingAverage) }))

  const yTicks = [0, 0.25, 0.5, 0.75, 1].map((t) => {
    const value = Math.round(t * maxV)
    return { value, y: y(value) }
  })

  const bandRect: Rect | null = band
    ? { x: dims.padL, y: y(band.upper), width: innerW, height: y(band.lower) - y(band.upper) }
    : null

  return { bars, trendPath, trendDots, yTicks, bandRect, maxV }
}
