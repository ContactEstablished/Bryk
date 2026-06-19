// Mirrors the Bryk.Application.Calendar DTOs (16-1). Dates are 'YYYY-MM-DD' (DateOnly).
// Enums serialize as strings via JsonStringEnumConverter.

export type ComplianceBucket = 'Grey' | 'Green' | 'Yellow' | 'Red'
export type CalendarItemKind = 'Planned' | 'Completed' | 'Event'

export interface CalendarItemDto {
  id: string
  kind: CalendarItemKind
  sport?: string | null
  title: string
  load?: number | null
  plannedLoad?: number | null
  compliance?: ComplianceBucket | null
  isUnplanned: boolean
  plannedWorkoutId?: string | null
  workoutId?: string | null
  trainingPlanId?: string | null
  priority?: string | null
  notes?: string | null
}

export interface CalendarDayDto {
  date: string
  items: CalendarItemDto[]
}

export interface CalendarFeedResponse {
  rangeStart: string
  rangeEnd: string
  days: CalendarDayDto[]
}
