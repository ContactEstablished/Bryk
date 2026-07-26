// Target-vs-actual state for the This Week bar. The thresholds are ADR-0008 §1's compliance bands,
// copied verbatim from Bryk.Application.Calendar.ComplianceClassifier so a dashboard week and a
// calendar day are graded by one rule. Pure: no imports, no Vue, no Date.

export type TargetState = 'good' | 'warn' | 'bad'

export interface TargetProgress {
  ratio: number
  state: TargetState
  dir: 'up' | 'down' | 'flat'
  deltaLabel: string
  widthPct: number
}

export function buildTargetProgress(actual: number, target: number): TargetProgress {
  // Degenerate guard, same as the classifier's: a zero target with anything completed reads as on-target.
  const ratio = target === 0 ? 1 : actual / target

  const state: TargetState = ratio >= 0.8 && ratio <= 1.2 ? 'good' : ratio >= 0.5 ? 'warn' : 'bad'

  // DeltaChip reports the DIRECTION OF THE DELTA, and colours `up` green / `down` red. That is
  // deliberate and separate from `state`: the bar carries the honest band colour, so an over-target
  // week shows a green "up" chip beside a warn-coloured bar. Do not "fix" the chip's colours.
  const dir: TargetProgress['dir'] = ratio > 1.2 ? 'up' : ratio < 0.8 ? 'down' : 'flat'

  const d = Math.round(actual - target)
  const deltaLabel = `${d > 0 ? '+' : ''}${d} TSS`

  const widthPct = Math.round(Math.min(100, Math.max(0, ratio * 100)))

  return { ratio, state, dir, deltaLabel, widthPct }
}
