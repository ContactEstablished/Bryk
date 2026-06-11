<script setup lang="ts">
import { onMounted, ref } from 'vue'
import {
  Activity,
  Bike,
  Dumbbell,
  Footprints,
  Heart,
  Medal,
  Waves,
  type LucideIcon,
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import LogWorkoutForm from '@/components/training/LogWorkoutForm.vue'
import { useTrainingStore } from '@/stores/training'

const store = useTrainingStore()
const showLog = ref(false)

onMounted(() => {
  if (!store.recentWorkouts) void store.loadRecentWorkouts()
})

// Parse the YYYY-MM-DD string as a UTC calendar date so the label is timezone-stable.
function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  })
}

function formatDuration(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = Math.floor(totalSeconds % 60)
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`
}

function formatDistance(meters: number): string {
  return `${(meters / 1000).toFixed(1)} km`
}

const sportIcons: Record<string, LucideIcon> = {
  Run: Footprints,
  Bike: Bike,
  Swim: Waves,
  Strength: Dumbbell,
  Triathlon: Medal,
}

function sportIcon(sport: string): LucideIcon {
  return sportIcons[sport] ?? Activity
}

function onLogged() {
  showLog.value = false
}
</script>

<template>
  <div class="card-surface">
    <div class="flex items-center justify-between gap-3 border-b border-border px-6 py-4">
      <h3 class="text-sm font-semibold">Recent Activity</h3>
      <Button v-if="!showLog" type="button" variant="outline" size="sm" @click="showLog = true">
        Log a workout
      </Button>
    </div>

    <LogWorkoutForm v-if="showLog" class="p-6" @logged="onLogged" @close="showLog = false" />

    <template v-else>
      <p v-if="!store.recentWorkouts" class="px-6 py-4 text-sm text-muted-foreground">Loading…</p>

      <ul v-else-if="store.recentWorkouts.length > 0">
        <li
          v-for="(w, i) in store.recentWorkouts"
          :key="w.id"
          class="grid grid-cols-[32px_1fr_auto] items-center gap-4 px-6 py-3.5 transition-colors duration-[120ms] hover:bg-muted"
          :class="i > 0 ? 'border-t border-border' : ''"
        >
          <div
            class="flex size-8 items-center justify-center rounded-lg border border-border-strong bg-raised text-subtle"
          >
            <component :is="sportIcon(w.sport)" :size="16" />
          </div>
          <div class="min-w-0">
            <p class="truncate text-sm font-semibold">{{ w.sport }}</p>
            <p class="mt-0.5 flex flex-wrap gap-x-3 font-mono text-[11px] text-muted-foreground">
              <span v-if="w.actualDurationSeconds != null">{{ formatDuration(w.actualDurationSeconds) }}</span>
              <span v-if="w.actualDistanceMeters != null">{{ formatDistance(w.actualDistanceMeters) }}</span>
              <span v-if="w.avgHr != null" class="inline-flex items-center gap-1">
                <Heart :size="10" />{{ w.avgHr }}
              </span>
              <span v-if="w.effectiveLoad != null" class="text-primary-hi">{{ w.effectiveLoad }} TSS</span>
              <span v-if="w.rpe != null">RPE {{ w.rpe }}</span>
            </p>
          </div>
          <span class="font-mono text-[11px] text-muted-foreground">{{ formatDay(w.completedDate) }}</span>
        </li>
      </ul>

      <p v-else class="px-6 py-4 text-sm text-muted-foreground">No completed workouts yet.</p>
    </template>
  </div>
</template>
