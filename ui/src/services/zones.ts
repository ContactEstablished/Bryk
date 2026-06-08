import { apiFetch } from '@/services/api'
import type {
  ZonesResponse,
  SportZonesResponse,
  ZoneOverrideRequest,
  ZoneSport,
} from '@/types/zones'

export async function getZones(): Promise<ZonesResponse> {
  const result = await apiFetch<ZonesResponse>('/zones')
  if (result === null) {
    throw new Error('Unexpected empty response from /zones')
  }
  return result
}

export async function saveZones(
  sport: ZoneSport,
  request: ZoneOverrideRequest,
): Promise<SportZonesResponse> {
  const result = await apiFetch<SportZonesResponse>(`/zones/${sport}`, {
    method: 'PUT',
    body: JSON.stringify(request),
  })
  if (result === null) {
    throw new Error(`Unexpected empty response from PUT /zones/${sport}`)
  }
  return result
}

export async function resetZones(sport: ZoneSport): Promise<void> {
  await apiFetch<null>(`/zones/${sport}`, { method: 'DELETE' })
}
