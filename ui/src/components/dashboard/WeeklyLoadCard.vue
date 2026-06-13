<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useTrainingStore } from '@/stores/training'
import { useAnalyticsStore } from '@/stores/analytics'

const store = useTrainingStore()
const analytics = useAnalyticsStore()

onMounted(() => {
  if (!store.thisWeek) void store.loadThisWeek()
  if (!store.recentWorkouts) void store.loadRecentWorkouts()
  if (!analytics.pmc) void analytics.loadPmc()
})

// ACWR sweet spot is 0.8–1.3; outside is a caution. Null = under 28 days of history → "—" (honest).
const acwr = computed(() => analytics.current?.acwr ?? null)
const acwrInBand = computed(() => acwr.value != null && acwr.value >= 0.8 && acwr.value <= 1.3)

// Effective loads of recent workouts in chronological order — an honest
// "recent training" sparkline until a weekly-history endpoint exists.
const loadSpark = computed(() => {
  const workouts = store.recentWorkouts
  if (!workouts) return null
  return [...workouts]
    .reverse()
    .map((w) => w.effectiveLoad)
    .filter((l): l is number => l != null)
})
</script>

<template>
  <MetricTile
    label="Weekly Load"
    :value="store.thisWeek?.weeklyLoad ?? 0"
    unit="TSS"
    :spark="loadSpark"
    :loading="!store.thisWeek"
  >
    <template #footer>
      <div class="flex items-center justify-between gap-2">
        <p class="text-xs text-muted-foreground">planned this week</p>
        <span
          class="font-mono text-[11px]"
          :class="acwr == null ? 'text-faint' : acwrInBand ? 'text-good' : 'text-bad'"
        >ACWR {{ acwr == null ? '—' : acwr.toFixed(2) }}</span>
      </div>
    </template>
  </MetricTile>
</template>
