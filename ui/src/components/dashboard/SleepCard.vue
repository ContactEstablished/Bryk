<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useWellnessStore } from '@/stores/wellness'
import { metricSeries, upIsGoodDelta } from '@/lib/wellness'

const store = useWellnessStore()

onMounted(() => {
  if (!store.summary) void store.loadSummary()
})

const sleep = computed(() => store.summary?.sleepHours ?? null)

// One decimal, as a STRING. MetricTile animates NUMERIC values through useCountUp, which formats with
// 0 decimals (MetricTile.vue:34 passes no options), so a numeric 7.5 would render "8". A string value
// is rendered verbatim (MetricTile.vue:35-39). The server rounds to 2 decimals; tiles show 1. Null
// stays null so the tile shows "—".
const value = computed(() => {
  const average = sleep.value?.average
  return average == null ? null : average.toFixed(1)
})

// Sleep hours is one of the two metrics ADR-0011 §5 allows a DeltaChip for (more sleep is good).
const delta = computed(() => upIsGoodDelta(sleep.value?.delta, 1))

const spark = computed(() => metricSeries(store.summary?.days ?? [], 'sleepHours'))

const nights = computed(() => sleep.value?.daysWithData ?? 0)
</script>

<template>
  <MetricTile
    label="Sleep Avg"
    :value="value"
    unit="h"
    :delta="delta"
    :spark="spark"
    :loading="store.loadingSummary && !store.summary"
  >
    <template #footer>
      <p v-if="nights > 0" class="text-xs text-muted-foreground">
        {{ nights }} night{{ nights === 1 ? '' : 's' }} logged
      </p>
      <!-- Nothing logged: no fabricated zero — say what to do (the FormCard.vue:32-37 pattern). -->
      <p v-else-if="store.summary" class="text-xs text-muted-foreground">
        Log sleep to see your 7-day average
      </p>
    </template>
  </MetricTile>
</template>
