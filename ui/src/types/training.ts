import type { Sport } from '@/types/onboarding'

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
