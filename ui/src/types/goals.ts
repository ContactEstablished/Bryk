import type { EventResponse, GoalResponse } from '@/types/profile'

// Read shapes for the /goals surface. Mirror the backend GET responses
// (Bryk.Application.Events.EventListItemResponse / Goals.GoalListItemResponse) and
// extend the id-bearing profile shapes with the fields those endpoints add.

// Id + name only — the chip navigates to /plans/{id}; no plan body is sent.
export interface LinkedPlan {
  id: string
  name: string
}

export interface EventListItem extends EventResponse {
  linkedPlans: LinkedPlan[]
}

export type GoalStatus = 'NoDate' | 'Upcoming' | 'DueSoon' | 'Overdue'

export interface GoalListItem extends GoalResponse {
  daysRemaining: number | null
  status: GoalStatus
}
