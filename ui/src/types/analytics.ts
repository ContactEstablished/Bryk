// Mirrors the Bryk.Application.Analytics DTOs (ADR-0006). Dates are 'YYYY-MM-DD' (DateOnly).

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
