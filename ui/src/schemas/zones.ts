import { z } from 'zod'

// Mirrors schemas/onboarding.ts's helper: '' / null collapse to null so an empty Upper input on an
// open-ended top/bottom zone validates as "no bound".
function optionalNumber<T extends z.ZodType<number>>(check: T) {
  return z.preprocess((v) => (v === '' || v == null ? null : v), check.nullable())
}

// Editable bounds only — zoneNumber is display metadata carried on the props, not a form field.
// LowerBound is non-negative (computed Z1 lower is 0 — % of threshold starts at 0); UpperBound is
// optional (null = open-ended), and when set must exceed the lower bound.
export const zoneBoundFormSchema = z
  .object({
    lowerBound: z.coerce.number().gte(0, 'Cannot be negative'),
    upperBound: optionalNumber(z.coerce.number().gte(0, 'Cannot be negative')),
  })
  .refine((d) => d.upperBound == null || d.upperBound > d.lowerBound, {
    message: 'Upper must be greater than lower',
    path: ['upperBound'],
  })

export const sportZonesFormSchema = z.object({
  zones: z.array(zoneBoundFormSchema),
})

export type SportZonesFormValues = z.infer<typeof sportZonesFormSchema>
