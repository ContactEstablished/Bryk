import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError } from '@/services/api'
import { getPmc, defaultPmcRange } from '@/services/analytics'
import type { PmcResponse, PmcSummary } from '@/types/analytics'

export const useAnalyticsStore = defineStore('analytics', () => {
  const pmc = ref<PmcResponse | null>(null)
  const loading = ref(false)
  const error = ref<ApiError | Error | null>(null)

  // One call feeds both the Form (TSB) tile and the Weekly Load ACWR chip (pmc carries the current summary).
  async function loadPmc() {
    loading.value = true
    error.value = null
    try {
      const { from, to } = defaultPmcRange()
      pmc.value = await getPmc(from, to)
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loading.value = false
    }
  }

  // The current summary for the range's last day (today). Null for a fresh athlete — surfaces are honest.
  const current = computed<PmcSummary | null>(() => pmc.value?.current ?? null)

  // Signed TSB change vs 7 days ago for the Form tile's DeltaChip. The series is contiguous and
  // zero-filled (ADR-0006 §3), so "7 days ago" is simply the 8th-from-last point. Null (no chip) when
  // there is no current summary or fewer than 8 days in series — honest, no fabricated trend.
  const tsbDeltaVs7d = computed<{ text: string; dir: 'up' | 'down' | 'flat' } | null>(() => {
    const data = pmc.value
    if (!data || !data.current || data.series.length < 8) return null

    const last = data.series[data.series.length - 1]
    const prior = data.series[data.series.length - 8]
    const delta = Math.round((last.tsb - prior.tsb) * 10) / 10
    const dir = delta > 0 ? 'up' : delta < 0 ? 'down' : 'flat'
    const text = `${delta > 0 ? '+' : ''}${delta.toFixed(1)}`
    return { text, dir }
  })

  return { pmc, loading, error, loadPmc, current, tsbDeltaVs7d }
})
