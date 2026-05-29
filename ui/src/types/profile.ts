import type {
  Gender,
  MethodologyChoice,
  SportThresholdsDto,
  EventDto,
  GoalDto,
} from '@/types/onboarding'

// Read-side shapes for the /profile surface. Mirror the backend Profile responses
// (Bryk.Application.Profile / Events / Goals) and reuse the onboarding DTOs/enums.

export interface ProfileRequiredResponse {
  name: string
  gender: Gender
  dateOfBirth: string // ISO 8601 DateOnly
  heightCm: number
  weightKg: number
  yearsTraining: number
  typicalWeeklyHours: number
  methodology: MethodologyChoice
}

export interface ProfileRecommendedResponse {
  restingHr: number | null
  maxHr: number | null
  sportThresholds: SportThresholdsDto[]
}

// Id-bearing read shapes so the editor can target per-item update / delete.
// Create/update request bodies use the Id-less EventDto / GoalDto.
export interface EventResponse extends EventDto {
  id: string
}

export interface GoalResponse extends GoalDto {
  id: string
}

export interface ProfileGoalsResponse {
  events: EventResponse[]
  goals: GoalResponse[]
}
