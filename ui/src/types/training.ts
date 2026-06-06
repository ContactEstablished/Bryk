import type { Sport, MethodologyChoice } from '@/types/onboarding'

// Planned workouts add Strength (a first-class v1 discipline, ADR-0003 §4) on top of the cardio
// sports. Onboarding's Sport union stays cardio-only by design (Strength is a conscious gap in the
// threshold flow), so training extends it here rather than widening the shared onboarding type.
export type PlannedSport = Sport | 'Strength'

// Mirrors Bryk.Application.Training.PlannedWorkoutResponse. Dates are 'YYYY-MM-DD' (DateOnly).
export interface PlannedWorkoutResponse {
  id: string
  trainingPlanId: string
  sport: PlannedSport
  scheduledDate: string
  title: string
  description: string | null
  plannedDurationMinutes: number | null
  plannedLoad: number | null
}

// Mirrors Bryk.Application.Training.ThisWeekResponse.
export interface ThisWeekResponse {
  weekStart: string
  weekEnd: string
  plannedWorkouts: PlannedWorkoutResponse[]
}

// ── Write-side request shapes (Task 9-6), mirroring Bryk.Application.Training request DTOs ──

export interface PlannedWorkoutDto {
  sport: PlannedSport
  scheduledDate: string
  title: string
  description: string | null
  plannedDurationMinutes: number | null
  plannedLoad: number | null
}

// The periodization fields (buildWeeks/recoveryWeeks/recoveryWeekPercentage) are nullable,
// forward-looking server fields not surfaced in the Phase 9 UI (ADR-0003); omitted here —
// the server treats them as null when absent.
export interface TrainingPlanRequest {
  name: string
  methodology: MethodologyChoice
  startDate: string
  endDate: string
  eventId: string | null
  plannedWorkouts: PlannedWorkoutDto[]
}

// Mirrors Bryk.Application.Training.TrainingPlanResponse (Id-bearing read shape).
export interface TrainingPlanResponse {
  id: string
  name: string
  methodology: MethodologyChoice
  startDate: string
  endDate: string
  eventId: string | null
  buildWeeks: number | null
  recoveryWeeks: number | null
  recoveryWeekPercentage: number | null
  plannedWorkouts: PlannedWorkoutResponse[]
}
