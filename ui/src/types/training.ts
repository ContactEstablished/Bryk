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
  // Structured payload (Task 10-4 / ADR-0004 §2). Only populated by the structure endpoint;
  // the plan/This-Week reads omit it, so it's optional on the shared shape.
  blocks?: WorkoutBlockResponse[]
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

// ── Structured-workout payload (Task 10-4 / ADR-0004 §2), mirroring the backend DTOs ──

export type StepIntent = 'Warmup' | 'Work' | 'Recovery' | 'Cooldown' | 'Rest'

// Write-side step. Order is positional; the server assigns it. Sport-discriminated fields
// (validated server-side and by the client schema factory): cardio uses duration/distance +
// zone/power/HR/pace targets; strength uses sets/reps/load/RPE.
export interface WorkoutStepDto {
  intent: StepIntent
  title: string | null
  durationSeconds: number | null
  distanceMeters: number | null
  targetZone: number | null
  targetPowerLow: number | null
  targetPowerHigh: number | null
  targetHrLow: number | null
  targetHrHigh: number | null
  targetPaceLow: number | null
  targetPaceHigh: number | null
  sets: number | null
  reps: number | null
  loadKg: number | null
  rpe: number | null
}

export interface WorkoutBlockDto {
  orderIndex: number
  repeats: number
  steps: WorkoutStepDto[]
}

export interface WorkoutStructureRequest {
  blocks: WorkoutBlockDto[]
}

export interface WorkoutStepResponse extends WorkoutStepDto {
  id: string
  orderIndex: number
}

export interface WorkoutBlockResponse {
  id: string
  orderIndex: number
  repeats: number
  steps: WorkoutStepResponse[]
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
