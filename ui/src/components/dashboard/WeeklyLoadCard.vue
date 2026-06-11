<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useTrainingStore } from '@/stores/training'

const store = useTrainingStore()

onMounted(() => {
  if (!store.thisWeek) void store.loadThisWeek()
  if (!store.recentWorkouts) void store.loadRecentWorkouts()
})

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
      <p class="text-xs text-muted-foreground">planned this week</p>
    </template>
  </MetricTile>
</template>
