<script setup lang="ts">
import { onMounted, ref } from 'vue'
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

function onLogged() {
  showLog.value = false
}
</script>

<template>
  <div class="rounded-lg border bg-card p-5">
    <div class="flex items-center justify-between">
      <h3 class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        Recent Activity
      </h3>
      <Button v-if="!showLog" type="button" variant="outline" size="sm" @click="showLog = true">
        Log a workout
      </Button>
    </div>

    <LogWorkoutForm v-if="showLog" class="mt-4" @logged="onLogged" @close="showLog = false" />

    <template v-else>
      <p v-if="!store.recentWorkouts" class="mt-3 text-sm text-muted-foreground">Loading…</p>

      <ul v-else-if="store.recentWorkouts.length > 0" class="mt-3 divide-y divide-border">
        <li
          v-for="w in store.recentWorkouts"
          :key="w.id"
          class="flex items-baseline justify-between gap-4 py-2"
        >
          <span class="text-sm">
            <span class="font-medium">{{ w.sport }}</span> &middot; {{ formatDay(w.completedDate) }}
          </span>
          <span v-if="w.effectiveLoad != null" class="text-xs text-muted-foreground">
            {{ w.effectiveLoad }} TSS
          </span>
        </li>
      </ul>

      <p v-else class="mt-3 text-sm text-muted-foreground">No completed workouts yet.</p>
    </template>
  </div>
</template>
