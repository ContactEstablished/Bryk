// Mirrors Task 20-2's shapes in api/Bryk.Application/Wellness/WellnessResponses.cs -
// WellnessEntryResponse, WellnessMetricSummaryDto, WellnessDailyPointDto, WellnessSummaryResponse -
// so the two files can be diffed by eye. DateOnly serializes as a 'YYYY-MM-DD' string; decimal? and
// int? both arrive as number | null.

// Exactly the PUT body. No `date` field: the date lives in the URL, and the route segment wins over
// anything in the body (Task 20-2, WellnessService.UpsertAsync step 1).
export interface WellnessEntryRequest {
  sleepHours: number | null
  sleepQuality: number | null
  restingHr: number | null
  weightKg: number | null
  soreness: number | null
  hrvMs: number | null
  notes: string | null
}

export interface WellnessEntryResponse extends WellnessEntryRequest {
  id: string
  date: string
}

export interface WellnessMetricSummary {
  average: number | null
  priorAverage: number | null
  delta: number | null
  daysWithData: number
}

export interface WellnessDailyPoint {
  date: string
  sleepHours: number | null
  sleepQuality: number | null
  restingHr: number | null
  weightKg: number | null
  soreness: number | null
  hrvMs: number | null
}

export interface WellnessSummaryResponse {
  to: string
  from: string
  priorFrom: string
  sleepHours: WellnessMetricSummary
  sleepQuality: WellnessMetricSummary
  restingHr: WellnessMetricSummary
  weightKg: WellnessMetricSummary
  soreness: WellnessMetricSummary
  hrvMs: WellnessMetricSummary
  days: WellnessDailyPoint[]
  hasAnyEntries: boolean
}

// The six metric keys, in entry order. Exported because 20-4's tile helpers key off it.
export type WellnessMetricKey =
  | 'sleepHours' | 'sleepQuality' | 'restingHr' | 'weightKg' | 'soreness' | 'hrvMs'
