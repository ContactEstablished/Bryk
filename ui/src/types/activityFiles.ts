import type { PlannedSport } from '@/types/training'

export type ActivityFileFormat = 'Fit' | 'Tcx' | 'Gpx'

export interface ZoneHistogramEntry {
  zoneNumber: number
  seconds: number
}

export interface ParsedActivity {
  sport: PlannedSport
  completedDate: string
  startTimeUtc: string
  durationSeconds: number | null
  distanceMeters: number | null
  avgHr: number | null
  maxHr: number | null
  avgPower: number | null
  avgPace: number | null
  sampleCount: number
}

export interface MatchCandidate {
  plannedWorkoutId: string
  trainingPlanId: string
  title: string
  sport: PlannedSport
  scheduledDate: string
  plannedLoad: number | null
  dayOffset: number
}

export interface ActivityFileUploadResponse {
  id: string
  fileName: string
  format: ActivityFileFormat
  byteSize: number
  parsed: ParsedActivity
  computedLoad: number | null
  zoneSeconds: ZoneHistogramEntry[]
  matchCandidates: MatchCandidate[]
}

export interface ActivityFileCommitResponse {
  workoutId: string
  plannedWorkoutId: string | null
  computedLoad: number | null
}

export interface ActivityFileSource {
  id: string
  fileName: string
  format: ActivityFileFormat
  uploadedAt: string
}
