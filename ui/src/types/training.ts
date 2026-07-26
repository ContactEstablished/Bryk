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
  // Training load (Task 11-1 / ADR-0005 §3). computedLoad is null on reads that don't load Blocks;
  // effectiveLoad = plannedLoad ?? computedLoad; isLoadOverride is true when plannedLoad is set.
  // Optional on the shared shape (older mocks / bare reads), like blocks.
  computedLoad?: number | null
  effectiveLoad?: number | null
  isLoadOverride?: boolean
  // Structured payload (Task 10-4 / ADR-0004 §2). Only populated by the structure endpoint;
  // the plan/This-Week reads omit it, so it's optional on the shared shape.
  blocks?: WorkoutBlockResponse[]
}

// Mirrors Bryk.Application.Training.ThisWeekResponse.
export interface ThisWeekResponse {
  weekStart: string
  weekEnd: string
  weeklyLoad?: number
  // Phase 18: the week's ramp target (null when no active plan / no usable baseline) and the sum of
  // the week's completed effective load. Optional, matching weeklyLoad, so older fixtures still fit.
  targetLoad?: number | null
  actualLoad?: number
  plannedWorkouts: PlannedWorkoutResponse[]
}

// ── Executed-workout shapes (Task 11-4 / ADR-0005 §4-6) ──

export interface WorkoutStepResultResponse {
  id: string
  workoutStepId: string | null
  orderIndex: number
  actualDurationSeconds: number | null
  actualDistanceMeters: number | null
  avgPower: number | null
  avgHr: number | null
  avgPace: number | null
  rpe: number | null
}

export interface WorkoutResponse {
  id: string
  plannedWorkoutId: string | null
  // Plan owning the linked planned workout — populated only on the single-workout detail read so the
  // detail view can reach GET .../structure; null on list reads and unlinked workouts (Task 13-1).
  trainingPlanId: string | null
  sport: PlannedSport
  completedDate: string
  actualDurationSeconds: number | null
  actualDistanceMeters: number | null
  avgHr: number | null
  maxHr: number | null
  computedLoad: number | null
  loadOverride: number | null
  effectiveLoad: number | null
  isLoadOverride: boolean
  rpe: number | null
  notes: string | null
  stepResults: WorkoutStepResultResponse[]
}

export interface WorkoutStepResultDto {
  workoutStepId: string | null
  actualDurationSeconds: number | null
  actualDistanceMeters: number | null
  avgPower: number | null
  avgHr: number | null
  avgPace: number | null
  rpe: number | null
}

export interface LogWorkoutRequest {
  sport: PlannedSport
  completedDate: string
  plannedWorkoutId: string | null
  actualDurationSeconds: number | null
  actualDistanceMeters: number | null
  avgHr: number | null
  maxHr: number | null
  loadOverride: number | null
  rpe: number | null
  notes: string | null
  stepResults: WorkoutStepResultDto[]
}

// Replace-style edit body (Task 13-1); same shape as a log (the PUT replaces actuals + step results).
export type UpdateWorkoutRequest = LogWorkoutRequest

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

// Mirrors Bryk.Application.Training.TrainingPlanUpdateRequest (Task 18-2). Metadata only —
// planned workouts are edited through their own endpoints. recoveryWeekPercentage is percent-scale
// (60 = 60% of a build week, ADR-0009 §6); eventId null clears the link.
export interface TrainingPlanUpdateRequest {
  name: string
  methodology: MethodologyChoice
  startDate: string
  endDate: string
  eventId: string | null
  buildWeeks: number | null
  recoveryWeeks: number | null
  recoveryWeekPercentage: number | null
}

// Mirrors Bryk.Application.Training.Periodization.* (Task 18-3).
export type TargetBaselineSource = 'None' | 'TrailingActual' | 'FirstWeekPlanned'

export interface WeeklyTargetWeek {
  weekStart: string
  targetLoad: number
  isRecoveryWeek: boolean
  isTaperWeek: boolean
  plannedLoad: number
  actualLoad: number
}

export interface WeeklyTargetsResponse {
  planId: string
  startDate: string
  endDate: string
  baseline: number | null
  baselineSource: TargetBaselineSource
  weeks: WeeklyTargetWeek[]
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
