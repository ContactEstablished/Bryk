// Mirrors the 10-1 backend contract (Bryk.Application.Zones). Enums serialize as strings.

export type ZoneSport = 'Bike' | 'Run' | 'Swim'
export type ZoneMetric = 'Power' | 'Hr' | 'Pace'

export interface ZoneDto {
  zoneNumber: number
  lowerBound: number
  upperBound: number | null
  isOverride: boolean
}

export interface SportZonesResponse {
  sport: ZoneSport
  metric: ZoneMetric
  zones: ZoneDto[]
}

export interface ZonesResponse {
  sports: SportZonesResponse[]
}

export interface ZoneBoundDto {
  zoneNumber: number
  lowerBound: number
  upperBound: number | null
}

export interface ZoneOverrideRequest {
  zones: ZoneBoundDto[]
}
