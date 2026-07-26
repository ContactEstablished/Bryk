import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import { getWellnessRange, getWellnessSummary, putWellness } from '@/services/wellness'
import type {
  WellnessEntryRequest,
  WellnessEntryResponse,
  WellnessSummaryResponse,
} from '@/types/wellness'

// Today as YYYY-MM-DD in UTC, so the date in the PUT URL and the range read match the server's
// DateOnly semantics. Mirrors the local helpers in stores/goals.ts:20-26 and stores/profile.ts.
function utcTodayIso(): string {
  const now = new Date()
  const yyyy = now.getUTCFullYear()
  const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(now.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

export const useWellnessStore = defineStore('wellness', () => {
  const summary = ref<WellnessSummaryResponse | null>(null)
  const today = ref<WellnessEntryResponse | null>(null)
  const loadingSummary = ref(false)
  const loadingToday = ref(false)
  const saving = ref(false)
  const error = ref<ApiError | Error | null>(null)

  async function loadSummary() {
    loadingSummary.value = true
    error.value = null
    try {
      summary.value = await getWellnessSummary()
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loadingSummary.value = false
    }
  }

  // A missing entry is the normal case for most of the day, not an error: the range read asks for a
  // single day and `today` simply stays null when it comes back empty.
  async function loadToday() {
    const d = utcTodayIso()
    loadingToday.value = true
    try {
      const rows = await getWellnessRange(d, d)
      today.value = rows[0] ?? null
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loadingToday.value = false
    }
  }

  // PUT replaces the whole day (ADR-0011 §2), then BOTH reads are re-fetched so every surface
  // bound to this store renders server truth rather than an optimistic guess.
  async function saveToday(values: WellnessEntryRequest) {
    saving.value = true
    error.value = null
    try {
      await putWellness(utcTodayIso(), values)
      await Promise.all([loadToday(), loadSummary()])
    } catch (e) {
      error.value = e as ApiError | Error
      // DELIBERATE re-throw. WellnessQuickEntryCard maps the server's field-prefixed messages
      // ("RestingHr: ...") onto its vee-validate fields, which it can only do if the ApiError
      // reaches it. Do NOT "tidy" this into a swallowed error - the card would then show nothing
      // on a 400. (Same convention as the re-throwing writes in stores/training.ts.)
      throw e
    } finally {
      saving.value = false
    }
  }

  return {
    summary,
    today,
    loadingSummary,
    loadingToday,
    saving,
    error,
    loadSummary,
    loadToday,
    saveToday,
  }
})
