<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useWellnessStore } from '@/stores/wellness'
import { metricSeries, upIsGoodDelta } from '@/lib/wellness'

const store = useWellnessStore()

onMounted(() => {
  if (!store.summary) void store.loadSummary()
})

const hrv = computed(() => store.summary?.hrvMs ?? null)

// Whole ms — a number, so MetricTile's count-up (0 decimals) renders it correctly.
const value = computed(() => {
  const average = hrv.value?.average
  return average == null ? null : Math.round(average)
})

// HRV is the second metric where up is good, so it may carry a DeltaChip (ADR-0011 §5).
const delta = computed(() => upIsGoodDelta(hrv.value?.delta, 0))

const spark = computed(() => metricSeries(store.summary?.days ?? [], 'hrvMs'))

const days = computed(() => hrv.value?.daysWithData ?? 0)
</script>

<template>
  <MetricTile
    label="HRV"
    :value="value"
    unit="ms"
    :delta="delta"
    :spark="spark"
    :loading="store.loadingSummary && !store.summary"
  >
    <template #footer>
      <p v-if="days > 0" class="text-xs text-muted-foreground">{{ days }} days logged</p>
      <p v-else-if="store.summary" class="text-xs text-muted-foreground">Log HRV to see a trend</p>
    </template>
  </MetricTile>
</template>
