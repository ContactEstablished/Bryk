import { z } from 'zod'

// '' / null inputs collapse to null; otherwise coerce and validate. Copied from
// schemas/workouts.ts:4-6 - the local copy is the established precedent here, not a shared util.
function optionalNumber<T extends z.ZodType<number>>(check: T) {
  return z.preprocess((v) => (v === '' || v == null ? null : v), check.nullable())
}

// Bounds mirror Task 20-2's WellnessEntryRequestValidator exactly (the ROADMAP's Phase 20 numbers,
// inclusive). Client-side validation is for instant feedback only - the server stays the authority.
export const wellnessEntrySchema = z
  .object({
    sleepHours: optionalNumber(z.coerce.number().gte(0, 'Min 0').lte(16, 'Max 16')),
    sleepQuality: optionalNumber(z.coerce.number().int('Whole number').gte(1, 'Min 1').lte(5, 'Max 5')),
    restingHr: optionalNumber(z.coerce.number().int('Whole number').gte(25, 'Min 25').lte(120, 'Max 120')),
    weightKg: optionalNumber(z.coerce.number().gte(30, 'Min 30').lte(250, 'Max 250')),
    soreness: optionalNumber(z.coerce.number().int('Whole number').gte(1, 'Min 1').lte(10, 'Max 10')),
    hrvMs: optionalNumber(z.coerce.number().int('Whole number').gte(10, 'Min 10').lte(250, 'Max 250')),
    notes: z.string().max(1000, 'Notes must be 1000 characters or fewer').nullable(),
  })
  // Mirrors the server's "Entry: At least one metric is required." rule. Notes alone does not
  // satisfy it - a row carrying only prose contributes to no tile and no average. The message is
  // attached to `sleepHours` so vee-validate has a field to render it against (the first field in
  // the form, so it lands where the eye already is).
  .refine(
    (v) =>
      v.sleepHours != null ||
      v.sleepQuality != null ||
      v.restingHr != null ||
      v.weightKg != null ||
      v.soreness != null ||
      v.hrvMs != null,
    { message: 'Enter at least one metric', path: ['sleepHours'] },
  )

export type WellnessFormValues = z.infer<typeof wellnessEntrySchema>
