import type { WellnessDailyPoint, WellnessMetricKey } from '@/types/wellness'

// Tile maths for the wellness dashboard cards (ADR-0011 §5). Which metrics may carry a DeltaChip is a
// decision, not a detail, and it lives here:
//
//   up is good  → sleep hours, HRV        → upIsGoodDelta() → MetricTile's `delta` prop (DeltaChip)
//   down is good → resting HR, weight, soreness → invertedChange() → MetricTile's `#footer` slot
//
// DeltaChip colours `up` green and `down` red by documented convention (lib/weeklyTarget.ts:21-23:
// "Do not 'fix' the chip's colours."), and it has four existing consumers. Routing an inverted metric
// through it would paint good news red — so those tiles pass NO `delta` prop at all and the inversion
// lives here, once. Soreness has no tile this phase; if it earns one, it takes the inverted path.

// The non-null values of `key` in day order — Sparkline's input. Callers hand the result straight to
// MetricTile, whose own `spark && spark.length >= 2` guard (MetricTile.vue:80) handles the 0- and
// 1-entry athlete: fewer than two points renders no sparkline rather than a misleading flat line.
// Never padded, never zero-filled — a day with no reading is missing, not a zero.
export function metricSeries(days: WellnessDailyPoint[], key: WellnessMetricKey): number[] {
  const out: number[] = []
  for (const day of days) {
    const value = day[key]
    if (value != null) out.push(value)
  }
  return out
}

// ONLY for metrics where up is good (sleep hours, HRV). Mirrors stores/analytics.ts:120-130's
// tsbDeltaVs7d: null when there is no delta, otherwise a sign-prefixed label and the direction.
export function upIsGoodDelta(
  delta: number | null | undefined,
  digits = 1,
): { text: string; dir: 'up' | 'down' | 'flat' } | null {
  if (delta == null) return null
  const dir = delta > 0 ? 'up' : delta < 0 ? 'down' : 'flat'
  return { text: `${delta > 0 ? '+' : ''}${delta.toFixed(digits)}`, dir }
}

// For the inverted metrics (resting HR, weight, soreness), which must NEVER pass MetricTile's `delta`
// prop. Returns footer text plus its own colour class: a DROP is good news, which is the inversion
// DeltaChip deliberately cannot express (see the header comment).
export function invertedChange(
  delta: number | null | undefined,
  unit: string,
  digits = 0,
): { text: string; className: string } | null {
  if (delta == null) return null
  const className = delta < 0 ? 'text-good' : delta > 0 ? 'text-bad' : 'text-muted-foreground'
  // Plain ASCII '-' — whatever toFixed emits. Do not substitute a typographic minus: the specs assert
  // on this exact string.
  return { text: `${delta > 0 ? '+' : ''}${delta.toFixed(digits)} ${unit} vs prior 7d`, className }
}
