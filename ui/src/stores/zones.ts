import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import {
  getZones,
  saveZones as saveZonesApi,
  resetZones as resetZonesApi,
} from '@/services/zones'
import type { ZonesResponse, ZoneOverrideRequest, ZoneSport } from '@/types/zones'

export const useZonesStore = defineStore('zones', () => {
  const zones = ref<ZonesResponse | null>(null)
  const loading = ref(false)
  const error = ref<ApiError | Error | null>(null)

  async function loadZones() {
    loading.value = true
    error.value = null
    try {
      zones.value = await getZones()
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loading.value = false
    }
  }

  // Per-sport override write / reset, then a full re-fetch so every card reflects server truth
  // (computed-vs-override flags included). Re-throw so the section can surface the error.
  async function saveZones(sport: ZoneSport, request: ZoneOverrideRequest) {
    await saveZonesApi(sport, request)
    await loadZones()
  }

  async function resetZones(sport: ZoneSport) {
    await resetZonesApi(sport)
    await loadZones()
  }

  return { zones, loading, error, loadZones, saveZones, resetZones }
})
