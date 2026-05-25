import { ApiError } from '@/services/api'

interface ApiValidationBody {
  status?: number
  error?: string
  errors?: unknown
  traceId?: string
}

export interface MappedFieldError {
  path: string
  message: string
}

export interface ApiValidationResult {
  fieldErrors: MappedFieldError[]
  globalMessages: string[]
}

export type OnboardingStep = 'required' | 'recommended' | 'goals'

export interface OnboardingMapperContext {
  /** Number of active sports in the recommended step (used to disambiguate indexed paths). */
  activeSportCount?: number
  /** Zero-based form indices of active sports, in submit order. */
  activeSportIndices?: number[]
  /** Number of event rows in the goals step. */
  eventCount?: number
  /** Number of goal rows in the goals step. */
  goalCount?: number
}

/**
 * Returns the validation messages array from a 400 ApiError matching the current
 * backend shape (`{ errors: string[] }`), or null if the error is not a recognized
 * server validation response.
 */
export function extractApiValidationMessages(err: unknown): string[] | null {
  if (!(err instanceof ApiError)) return null
  if (err.status !== 400) return null
  const body = err.body as ApiValidationBody | null | undefined
  const errors = body?.errors
  if (!Array.isArray(errors)) return null
  const stringErrors = errors.filter((m): m is string => typeof m === 'string')
  if (stringErrors.length === 0) return null
  return stringErrors
}

/**
 * Maps an ApiError's validation messages to vee-validate field paths for a given
 * onboarding step. Returns null if the error is not a 400 with the expected shape.
 * Unmappable messages are returned in `globalMessages` for display in a global area.
 */
export function mapApiValidationToFields(
  err: unknown,
  step: OnboardingStep,
  context: OnboardingMapperContext = {},
): ApiValidationResult | null {
  const messages = extractApiValidationMessages(err)
  if (messages === null) return null

  const fieldErrors: MappedFieldError[] = []
  const globalMessages: string[] = []

  for (const message of messages) {
    const path = mapMessage(message, step, context)
    if (path) {
      fieldErrors.push({ path, message })
    } else {
      globalMessages.push(message)
    }
  }

  return { fieldErrors, globalMessages }
}

// FluentValidation's default messages begin with the property name in single quotes,
// e.g. `'Height Cm' must be greater than '0'.` PascalCase is split with spaces.
const QUOTED_FIELD_RE = /^'([^']+)'\s+/

function quotedField(message: string): string | null {
  const m = QUOTED_FIELD_RE.exec(message)
  return m ? m[1] : null
}

function mapMessage(
  message: string,
  step: OnboardingStep,
  ctx: OnboardingMapperContext,
): string | null {
  switch (step) {
    case 'required':
      return mapRequired(message)
    case 'recommended':
      return mapRecommended(message, ctx)
    case 'goals':
      return mapGoals(message, ctx)
  }
}

function mapRequired(message: string): string | null {
  // Custom WithMessage override on DateOfBirth
  if (/at least 13 years old/i.test(message)) return 'dateOfBirth'

  const field = quotedField(message)
  if (!field) return null
  switch (field) {
    case 'Name':
      return 'name'
    case 'Gender':
      return 'gender'
    case 'Date Of Birth':
      return 'dateOfBirth'
    case 'Height Cm':
      return 'heightCm'
    case 'Weight Kg':
      return 'weightKg'
    case 'Years Training':
      return 'yearsTraining'
    case 'Typical Weekly Hours':
      return 'typicalWeeklyHours'
    case 'Methodology':
      return 'methodology'
    default:
      return null
  }
}

function mapRecommended(
  message: string,
  ctx: OnboardingMapperContext,
): string | null {
  // Custom WithMessage override: MaxHr vs RestingHr cross-field check.
  if (/MaxHr must be greater than RestingHr/i.test(message)) return 'maxHr'

  // Sport-level cross-field: Lt2 > Lt1. Index is not in the message.
  if (/Lt2 must be greater than Lt1/i.test(message)) {
    const idx = singleActiveSportIndex(ctx)
    return idx === null ? null : `sportThresholds[${idx}].lt2`
  }

  if (/CustomZonesJson must be valid JSON/i.test(message)) {
    const idx = singleActiveSportIndex(ctx)
    return idx === null ? null : `sportThresholds[${idx}].customZonesJson`
  }

  const field = quotedField(message)
  if (!field) return null
  switch (field) {
    case 'Resting Hr':
      return 'restingHr'
    case 'Max Hr':
      return 'maxHr'
    case 'Threshold Value': {
      const idx = singleActiveSportIndex(ctx)
      return idx === null ? null : `sportThresholds[${idx}].thresholdValue`
    }
    case 'Lt1': {
      const idx = singleActiveSportIndex(ctx)
      return idx === null ? null : `sportThresholds[${idx}].lt1`
    }
    case 'Lt2': {
      const idx = singleActiveSportIndex(ctx)
      return idx === null ? null : `sportThresholds[${idx}].lt2`
    }
    case 'Sport': {
      const idx = singleActiveSportIndex(ctx)
      return idx === null ? null : `sportThresholds[${idx}].sport`
    }
    default:
      return null
  }
}

function singleActiveSportIndex(ctx: OnboardingMapperContext): number | null {
  if (ctx.activeSportIndices && ctx.activeSportIndices.length === 1) {
    return ctx.activeSportIndices[0]
  }
  if (ctx.activeSportCount === 1 && ctx.activeSportIndices?.length) {
    return ctx.activeSportIndices[0]
  }
  return null
}

function mapGoals(
  message: string,
  ctx: OnboardingMapperContext,
): string | null {
  // Custom WithMessage overrides
  if (/EventDate must be today or in the future/i.test(message)) {
    return ctx.eventCount === 1 ? 'events[0].eventDate' : null
  }
  if (/TargetDate must be today or in the future/i.test(message)) {
    return ctx.goalCount === 1 ? 'goals[0].targetDate' : null
  }

  const field = quotedField(message)
  if (!field) return null

  // Fields unique to goal items
  const goalField = goalFieldMap[field]
  if (goalField) {
    return ctx.goalCount === 1 ? `goals[0].${goalField}` : null
  }

  // Fields unique to event items
  const eventField = eventFieldMap[field]
  if (eventField) {
    return ctx.eventCount === 1 ? `events[0].${eventField}` : null
  }

  return null
}

const goalFieldMap: Record<string, string> = {
  Description: 'description',
  Type: 'type',
  'Target Date': 'targetDate',
}

const eventFieldMap: Record<string, string> = {
  Name: 'name',
  'Event Date': 'eventDate',
  Sport: 'sport',
  'Triathlon Distance': 'triathlonDistance',
  Priority: 'priority',
  Notes: 'notes',
}
