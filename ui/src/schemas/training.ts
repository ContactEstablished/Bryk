import { z } from 'zod'

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
