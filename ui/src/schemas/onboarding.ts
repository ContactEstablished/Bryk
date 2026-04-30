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
