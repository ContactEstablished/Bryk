import type { PlannedSport } from '@/types/training'

export type PillKind = 'run' | 'bike' | 'swim' | 'strength' | 'triathlon' | 'neutral'

const sportKinds: Record<string, PillKind> = {
  Run: 'run',
  Bike: 'bike',
  Swim: 'swim',
  Strength: 'strength',
  Triathlon: 'triathlon',
}

export function sportToPillKind(sport: PlannedSport | string): PillKind {
  return sportKinds[sport] ?? 'neutral'
}
