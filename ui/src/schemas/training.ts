import { z } from 'zod'
import type { PlannedSport, WorkoutStepDto } from '@/types/training'

// '' / null inputs collapse to null; otherwise coerce and validate. Kept local
// (mirrors schemas/onboarding.ts) rather than exporting a one-liner across modules.
function optionalNumber<T extends z.ZodType<number>>(check: T) {
  return z.preprocess((v) => (v === '' || v == null ? null : v), check.nullable())
}

export const plannedWorkoutItemSchema = z.object({
  sport: z.enum(['Swim', 'Bike', 'Run', 'Triathlon', 'Strength'], {
    message: 'Sport is required',
  }),
  scheduledDate: z.string().min(1, 'Date is required'),
  title: z
    .string()
    .min(1, 'Title is required')
    .max(200, 'Title must be 200 characters or fewer'),
  plannedDurationMinutes: optionalNumber(
    z.coerce.number().int('Whole number').gte(0, 'Cannot be negative'),
  ),
  plannedLoad: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
})

export const trainingPlanSchema = z
  .object({
    name: z
      .string()
      .min(1, 'Plan name is required')
      .max(200, 'Name must be 200 characters or fewer'),
    methodology: z.enum(['Pyramidal', 'Periodization', 'Polarized', 'Norwegian'], {
      message: 'Please select a methodology',
    }),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    eventId: z.string(),
    plannedWorkouts: z.array(plannedWorkoutItemSchema),
  })
  .refine((d) => !d.startDate || !d.endDate || d.endDate >= d.startDate, {
    message: 'End date must be on or after start date',
    path: ['endDate'],
  })

export type TrainingPlanFormValues = z.infer<typeof trainingPlanSchema>

// ── Structured-workout builder (Task 10-5) ─────────────

// Form shape — orderIndex is positional (assigned at submit), so it is not a form field.
export interface WorkoutBlockFormItem {
  repeats: number
  steps: WorkoutStepDto[]
}
export interface WorkoutStructureFormValues {
  blocks: WorkoutBlockFormItem[]
}

function zoneCount(sport: PlannedSport): number {
  if (sport === 'Bike') return 7
  if (sport === 'Run' || sport === 'Swim') return 5
  return 0
}

// Sport-aware schema mirroring Task 10-4's server rules. Built per builder instance because the
// sport-discriminated rules (exactly one of duration/distance for cardio; strength requires
// sets/reps and forbids cardio targets; TargetZone bounded by the sport's zone count) need the sport.
export function buildWorkoutStructureSchema(sport: PlannedSport) {
  const isStrength = sport === 'Strength'
  const maxZone = zoneCount(sport)

  const stepSchema = z
    .object({
      intent: z.enum(['Warmup', 'Work', 'Recovery', 'Cooldown', 'Rest']),
      title: z.string().max(200).nullable().optional(),
      durationSeconds: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
      distanceMeters: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
      targetZone: optionalNumber(z.coerce.number().int().gte(1, 'Must be at least 1')),
      targetPowerLow: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
      targetPowerHigh: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
      targetHrLow: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
      targetHrHigh: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
      targetPaceLow: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
      targetPaceHigh: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
      sets: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
      reps: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
      loadKg: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
      rpe: optionalNumber(z.coerce.number().gte(0, 'Min 0').lte(10, 'Max 10')),
    })
    .superRefine((s, ctx) => {
      const range = (low: number | null, high: number | null, path: string) => {
        if (low != null && high != null && high < low) {
          ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'High must be ≥ low', path: [path] })
        }
      }
      range(s.targetPowerLow, s.targetPowerHigh, 'targetPowerHigh')
      range(s.targetHrLow, s.targetHrHigh, 'targetHrHigh')
      range(s.targetPaceLow, s.targetPaceHigh, 'targetPaceHigh')

      if (isStrength) {
        if (s.sets == null || s.reps == null) {
          ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Sets and reps are required', path: ['sets'] })
        }
      } else {
        if ((s.durationSeconds == null) === (s.distanceMeters == null)) {
          ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Set exactly one of duration or distance', path: ['durationSeconds'] })
        }
        if (s.targetZone != null && (maxZone === 0 || s.targetZone > maxZone)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: maxZone === 0 ? `${sport} has no zones` : `Zone must be 1–${maxZone}`,
            path: ['targetZone'],
          })
        }
      }
    })

  const blockSchema = z.object({
    repeats: z.coerce.number().int('Whole number').gte(1, 'Must repeat at least once'),
    steps: z.array(stepSchema),
  })

  return z.object({ blocks: z.array(blockSchema) })
}
