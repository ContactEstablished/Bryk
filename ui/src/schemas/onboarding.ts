import { z } from 'zod'

const MIN_DOB_DATE = '1900-01-01'

function ageOnDate(dob: string, today: Date): number {
  const d = new Date(dob)
  let age = today.getFullYear() - d.getFullYear()
  const m = today.getMonth() - d.getMonth()
  if (m < 0 || (m === 0 && today.getDate() < d.getDate())) age--
  return age
}

export const onboardingRequiredSchema = z.object({
  name: z
    .string()
    .min(1, 'Name is required')
    .max(200, 'Name must be 200 characters or fewer'),
  gender: z.enum(['Male', 'Female', 'Other', 'PreferNotToSay'], {
    message: 'Please select a gender',
  }),
  dateOfBirth: z
    .string()
    .min(1, 'Date of birth is required')
    .refine((s) => s > MIN_DOB_DATE, 'Date of birth must be after 1900-01-01')
    .refine((s) => ageOnDate(s, new Date()) >= 13, 'You must be at least 13 years old'),
  heightCm: z.coerce.number().gt(0, 'Height must be greater than 0').lt(300, 'Height must be less than 300'),
  weightKg: z.coerce.number().gt(0, 'Weight must be greater than 0').lt(500, 'Weight must be less than 500'),
  yearsTraining: z.coerce.number().int('Must be a whole number').min(0, 'Cannot be negative').max(80, 'Cannot exceed 80'),
  typicalWeeklyHours: z.coerce.number().min(0, 'Cannot be negative').max(168, 'Cannot exceed 168'),
  methodology: z.enum(['Pyramidal', 'Periodization', 'Polarized', 'Norwegian'], {
    message: 'Please select a methodology',
  }),
})

export type OnboardingRequiredFormValues = z.infer<typeof onboardingRequiredSchema>

// ── Recommended step ─────────────────────────────────

function optionalNumber<T extends z.ZodType<number>>(check: T) {
  return z.preprocess(
    (v) => (v === '' || v == null ? null : v),
    check.nullable(),
  )
}

const sportThresholdsItemSchema = z
  .object({
    sport: z.enum(['Bike', 'Run', 'Swim']),
    isActive: z.boolean(),
    thresholdValue: optionalNumber(z.coerce.number().gt(0, 'Must be greater than 0')),
    lt1: optionalNumber(z.coerce.number().gt(0, 'Must be greater than 0')),
    lt2: optionalNumber(z.coerce.number().gt(0, 'Must be greater than 0')),
    customZonesJson: z.string().nullable().optional(),
  })
  .refine(
    (d) => d.lt1 == null || d.lt2 == null || d.lt2 > d.lt1,
    { message: 'LT2 must be greater than LT1', path: ['lt2'] },
  )

export const onboardingRecommendedSchema = z
  .object({
    restingHr: optionalNumber(
      z.coerce.number().int('Whole number').gte(1, 'Must be at least 1').lte(149, 'Cannot exceed 149'),
    ),
    maxHr: optionalNumber(
      z.coerce.number().int('Whole number').gte(1, 'Must be at least 1').lte(249, 'Cannot exceed 249'),
    ),
    sportThresholds: z.array(sportThresholdsItemSchema),
  })
  .refine(
    (d) => d.restingHr == null || d.maxHr == null || d.maxHr > d.restingHr,
    { message: 'Max HR must be greater than Resting HR', path: ['maxHr'] },
  )

export type OnboardingRecommendedFormValues = z.infer<typeof onboardingRecommendedSchema>

// ── Goals step ───────────────────────────────────────

const eventItemSchema = z.object({
  name: z.string().min(1, 'Event name is required'),
  eventDate: z.string().min(1, 'Date is required'),
  sport: z.enum(['Swim', 'Bike', 'Run', 'Triathlon']).nullable(),
  triathlonDistance: z.enum(['Sprint', 'Olympic', 'HalfIronman', 'Ironman', 'Custom']).nullable(),
  customDistanceName: z.string().nullable(),
  priority: z.enum(['A', 'B', 'C'], { message: 'Priority is required' }),
  notes: z.string().nullable(),
})

const goalItemSchema = z.object({
  description: z.string().min(1, 'Description is required'),
  targetDate: z.string().nullable(),
})

export const onboardingGoalsSchema = z.object({
  events: z.array(eventItemSchema),
  goals: z.array(goalItemSchema),
})

export type OnboardingGoalsFormValues = z.infer<typeof onboardingGoalsSchema>
