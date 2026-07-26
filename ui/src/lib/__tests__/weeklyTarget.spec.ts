import { describe, expect, it } from 'vitest'
import { buildTargetProgress } from '@/lib/weeklyTarget'

describe('buildTargetProgress — ADR-0008 §1 bands', () => {
  it.each([
    { actual: 80, target: 100, state: 'good', dir: 'flat', deltaLabel: '-20 TSS', widthPct: 80 },
    { actual: 79, target: 100, state: 'warn', dir: 'down', deltaLabel: '-21 TSS', widthPct: 79 },
    { actual: 100, target: 100, state: 'good', dir: 'flat', deltaLabel: '0 TSS', widthPct: 100 },
    { actual: 120, target: 100, state: 'good', dir: 'flat', deltaLabel: '+20 TSS', widthPct: 100 },
    { actual: 121, target: 100, state: 'warn', dir: 'up', deltaLabel: '+21 TSS', widthPct: 100 },
    { actual: 50, target: 100, state: 'warn', dir: 'down', deltaLabel: '-50 TSS', widthPct: 50 },
    { actual: 49, target: 100, state: 'bad', dir: 'down', deltaLabel: '-51 TSS', widthPct: 49 },
    // The div-by-zero guard: ratio 1 ⇒ good, full width.
    { actual: 0, target: 0, state: 'good', dir: 'flat', deltaLabel: '0 TSS', widthPct: 100 },
  ])(
    'actual $actual / target $target → $state, $dir, $deltaLabel, $widthPct%',
    ({ actual, target, state, dir, deltaLabel, widthPct }) => {
      const result = buildTargetProgress(actual, target)

      expect(result.state).toBe(state)
      expect(result.dir).toBe(dir)
      expect(result.deltaLabel).toBe(deltaLabel)
      expect(result.widthPct).toBe(widthPct)
    },
  )
})
