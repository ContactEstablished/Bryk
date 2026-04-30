// ── Enums ──────────────────────────────────────────────

export type Gender = 'Male' | 'Female' | 'Other' | 'PreferNotToSay'

export type MethodologyChoice =
  | 'Pyramidal'
  | 'Periodization'
  | 'Polarized'
  | 'Norwegian'

export type Sport = 'Swim' | 'Bike' | 'Run'

export type TriathlonDistance =
  | 'Sprint'
  | 'Olympic'
  | 'HalfIronman'
  | 'Ironman'
  | 'Custom'

export type EventPriority = 'A' | 'B' | 'C'

export type GoalType = 'General' | 'EventDriven'

// ── DTOs ───────────────────────────────────────────────

export interface OnboardingRequiredRequest {
  name: string
  gender: Gender
  dateOfBirth: string // ISO 8601 DateOnly
  heightCm: number
  weightKg: number
  yearsTraining: number
  typicalWeeklyHours: number
  methodology: MethodologyChoice
}

export interface SportThresholdsDto {
  sport: Sport
  isActive: boolean
  thresholdValue: number | null
  lt1: number | null
  lt2: number | null
  customZonesJson: string | null
}

export interface OnboardingRecommendedRequest {
  restingHr: number | null
  maxHr: number | null
  sportThresholds: SportThresholdsDto[]
}

export interface EventDto {
  name: string
  eventDate: string // ISO 8601 DateOnly
  sport: Sport | null
  triathlonDistance: TriathlonDistance | null
  priority: EventPriority
  notes: string | null
}

export interface GoalDto {
  type: GoalType
  description: string
  targetDate: string | null // ISO 8601 DateOnly
}

export interface OnboardingGoalsRequest {
  events: EventDto[]
  goals: GoalDto[]
}

export interface OnboardingStatusResponse {
  requiredComplete: boolean
  recommendedComplete: boolean
  goalsComplete: boolean
}
