import { z } from 'zod'

// '' / null inputs collapse to null; otherwise coerce and validate (mirrors schemas/onboarding.ts).
function optionalNumber<T extends z.ZodType<number>>(check: T) {
  return z.preprocess((v) => (v === '' || v == null ? null : v), check.nullable())
}

function utcTodayIso(): string {
  const now = new Date()
  const yyyy = now.getUTCFullYear()
  const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(now.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

// All per-step actuals are nullable (partial entry is valid, ADR-0005 §5); workoutStepId links a row
// to the planned step it realizes (set when seeded from a plan).
export const workoutStepResultItemSchema = z.object({
  workoutStepId: z.string().nullable(),
  actualDurationSeconds: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  actualDistanceMeters: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  avgPower: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  avgHr: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  avgPace: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  rpe: optionalNumber(z.coerce.number().gte(0, 'Min 0').lte(10, 'Max 10')),
})

export const logWorkoutSchema = z.object({
  completedDate: z
    .string()
    .min(1, 'Date is required')
    .refine((s) => s <= utcTodayIso(), 'Completed date cannot be in the future'),
  actualDurationSeconds: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  actualDistanceMeters: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  avgHr: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  maxHr: optionalNumber(z.coerce.number().int('Whole number').gt(0, 'Must be greater than 0')),
  loadOverride: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
  rpe: optionalNumber(z.coerce.number().gte(0, 'Min 0').lte(10, 'Max 10')),
  notes: z.string().max(2000, 'Notes must be 2000 characters or fewer').nullable(),
  stepResults: z.array(workoutStepResultItemSchema),
})

export type LogWorkoutFormValues = z.infer<typeof logWorkoutSchema>
