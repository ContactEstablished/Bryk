import type { PmcPoint } from '@/types/analytics'

// Shared chart dimensions (ported from the design export's charts.jsx PMCChart viewBox).
export interface ChartDims {
  width: number
  height: number
  padL: number
  padR: number
  padT: number
  padB: number
}

export const PMC_DIMS: ChartDims = { width: 720, height: 220, padL: 40, padR: 20, padT: 16, padB: 28 }

export interface PmcGeometry {
  ctlPath: string
  ctlFillPath: string
  atlPath: string
  loadBars: { x: number; y0: number; y1: number }[]
  yTicks: { value: number; y: number }[]
  endMarkers: { ctl: { x: number; y: number }; atl: { x: number; y: number } } | null
  maxV: number
}

// Pure: a zero-filled PMC series → all the SVG geometry the chart needs (CTL/ATL line + fill paths, the
// daily-load tick bars, y-axis ticks, end markers). No DOM, no Vue — unit-tested directly. Guards an empty
// / all-zero series so no path carries NaN. maxV = 1.15 × max(ctl, atl) (the design's headroom factor).
export function buildPmcGeometry(series: PmcPoint[], dims: ChartDims = PMC_DIMS): PmcGeometry {
  const innerW = dims.width - dims.padL - dims.padR
  const innerH = dims.height - dims.padT - dims.padB
  const baselineY = dims.padT + innerH
  const n = series.length

  const rawMax = series.length ? Math.max(...series.map((d) => Math.max(d.ctl, d.atl))) : 0
  const maxV = rawMax > 0 ? rawMax * 1.15 : 1

  const x = (i: number): number => (n <= 1 ? dims.padL : dims.padL + (i / (n - 1)) * innerW)
  const y = (v: number): number => dims.padT + innerH - (v / maxV) * innerH

  const toPath = (pts: readonly (readonly [number, number])[]): string =>
    pts.map((p, i) => (i ? 'L' : 'M') + p[0].toFixed(1) + ' ' + p[1].toFixed(1)).join(' ')

  const ctlPath = toPath(series.map((d, i) => [x(i), y(d.ctl)] as const))
  const atlPath = toPath(series.map((d, i) => [x(i), y(d.atl)] as const))
  const ctlFillPath =
    n >= 2 ? `${ctlPath} L ${x(n - 1).toFixed(1)} ${baselineY} L ${x(0).toFixed(1)} ${baselineY} Z` : ''

  const loadBars = series.map((d, i) => ({ x: x(i), y0: baselineY, y1: y(d.load) }))

  const yTicks = [0, 0.5, 1].map((t) => {
    const value = Math.round(t * maxV)
    return { value, y: y(value) }
  })

  const endMarkers = n >= 1
    ? { ctl: { x: x(n - 1), y: y(series[n - 1].ctl) }, atl: { x: x(n - 1), y: y(series[n - 1].atl) } }
    : null

  return { ctlPath, ctlFillPath, atlPath, loadBars, yTicks, endMarkers, maxV }
}
