// Shared ring dimensions (the design export's ProgressRing: 160px dial, 8px stroke, 60 ticks).
export interface RingOptions {
  size?: number
  stroke?: number
  ticks?: number
}

export interface RingGeometry {
  size: number
  stroke: number
  radius: number
  cx: number
  cy: number
  circumference: number
  dashArray: number
  dashOffset: number
  ticks: { x1: number; y1: number; x2: number; y2: number }[]
}

export const RING_DEFAULTS: Required<RingOptions> = { size: 160, stroke: 8, ticks: 60 }

// Pure: a fill fraction + dial dimensions → all the SVG geometry the ring needs (track centre/radius,
// circumference, the progress arc's dash values, evenly spaced tick coordinates). No DOM, no Vue —
// unit-tested directly. Clamps out-of-range fractions (overdue → 1, not yet started → 0) and guards
// NaN/Infinity → 0 so a bad upstream date calc never renders a broken arc. Ticks and the arc share a
// 12-o'clock zero angle (-90°), so the template's arc rotation lines up with the tick marks.
export function buildRingGeometry(fraction: number, opts: RingOptions = {}): RingGeometry {
  const { size, stroke, ticks } = { ...RING_DEFAULTS, ...opts }
  const safeFraction = Number.isFinite(fraction) ? Math.min(1, Math.max(0, fraction)) : 0

  const cx = size / 2
  const cy = size / 2
  const radius = size / 2 - stroke / 2
  const circumference = 2 * Math.PI * radius
  const dashOffset = circumference * (1 - safeFraction)

  const tickInner = radius - stroke
  const tickOuter = radius + stroke / 2
  const tickMarks = Array.from({ length: ticks }, (_, i) => {
    const angle = (i / ticks) * 2 * Math.PI - Math.PI / 2
    const cos = Math.cos(angle)
    const sin = Math.sin(angle)
    return {
      x1: cx + tickInner * cos,
      y1: cy + tickInner * sin,
      x2: cx + tickOuter * cos,
      y2: cy + tickOuter * sin,
    }
  })

  return {
    size,
    stroke,
    radius,
    cx,
    cy,
    circumference,
    dashArray: circumference,
    dashOffset,
    ticks: tickMarks,
  }
}
