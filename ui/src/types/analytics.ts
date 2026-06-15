// Mirrors the Bryk.Application.Analytics DTOs (ADR-0006/0007). Dates are 'YYYY-MM-DD' (DateOnly).
import type { PlannedSport } from '@/types/training'

export interface DailyLoadPoint {
  date: string
  load: number
}

export interface PmcPoint {
  date: string
  load: number
  ctl: number
  atl: number
  tsb: number
}

// The "current" summary for the requested range's last day. acwr is null under 28 days of history.
export interface PmcSummary {
  date: string
  ctl: number
  atl: number
  tsb: number
  acwr: number | null
}

// The pmc read shape: the [from, to] series plus the current summary (null for an athlete with no history).
export interface PmcResponse {
  series: PmcPoint[]
  current: PmcSummary | null
}

// One ISO week of load (ADR-0007 §3). rollingAverage = trailing 4-week mean of actualLoad.
export interface WeeklyLoadWeek {
  weekStart: string
  plannedLoad: number
  actualLoad: number
  rollingAverage: number
}

// The single optimal band (ADR-0007 §1): [0.8, 1.3] × the trailing 4-week mean actual load.
export interface OptimalBand {
  lower: number
  upper: number
}

// The weekly-load read shape: the N ISO weeks (oldest → newest) + the band (null for a fresh athlete).
export interface WeeklyLoadResponse {
  weeks: WeeklyLoadWeek[]
  optimalBand: OptimalBand | null
}

// Coarse, estimated time-in-zone (ADR-0007 §4). zoneNumber 1..5.
export interface ZoneTime {
  zoneNumber: number
  seconds: number
}

export interface ZoneTimeMethodBreakdown {
  structureSeconds: number
  sessionAvgSeconds: number
  unclassifiedSeconds: number
}

export interface TimeInZoneResponse {
  zones: ZoneTime[]
  methodBreakdown: ZoneTimeMethodBreakdown
  totalSeconds: number
}

// Session-level peaks (ADR-0007 §2). value's unit is implied by kind + sport.
export type PeakKind = 'Load' | 'Duration' | 'Distance' | 'Pace' | 'Power'

export interface PeakRecord {
  kind: PeakKind
  sport: PlannedSport
  value: number
  achievedDate: string
  achievedWorkoutId: string
  isRecent: boolean
  previousValue: number | null
}

export interface PeaksResponse {
  records: PeakRecord[]
}
