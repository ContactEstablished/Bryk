<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useTrainingStore } from '@/stores/training'

const store = useTrainingStore()

onMounted(() => {
  if (!store.thisWeek) void store.loadThisWeek()
})

// Parse the YYYY-MM-DD string as a UTC calendar date so the label is timezone-stable
// and matches the server's DateOnly (mirrors PrimaryGoalCard).
function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  })
}

const weekRange = computed(() => {
  const tw = store.thisWeek
  if (!tw) return ''
  return `${formatDay(tw.weekStart)} – ${formatDay(tw.weekEnd)}`
})

const sessions = computed(() => store.thisWeek?.plannedWorkouts ?? [])
</script>

<template>
  <div class="rounded-lg border bg-card p-5">
    <div class="flex items-baseline justify-between">
      <h3 class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        This Week
      </h3>
      <span v-if="store.thisWeek" class="text-xs text-muted-foreground">{{ weekRange }}</span>
    </div>

    <!-- Loading (this week not yet fetched) -->
    <p v-if="!store.thisWeek" class="mt-3 text-sm text-muted-foreground">
      Loading…
    </p>

    <!-- Populated -->
    <ul v-else-if="sessions.length > 0" class="mt-3 divide-y divide-border">
      <li
        v-for="session in sessions"
        :key="session.id"
        class="flex items-baseline justify-between gap-4 py-2"
      >
        <div class="min-w-0">
          <p class="truncate text-sm font-medium">{{ session.title }}</p>
          <p class="text-xs text-muted-foreground">
            {{ formatDay(session.scheduledDate) }} &middot; {{ session.sport }}
          </p>
        </div>
        <div class="shrink-0 text-right text-xs text-muted-foreground">
          <span v-if="session.plannedDurationMinutes != null">{{ session.plannedDurationMinutes }} min</span>
          <span v-if="session.effectiveLoad != null" class="ml-2">{{ session.effectiveLoad }} TSS</span>
        </div>
      </li>
    </ul>

    <!-- Empty (loaded, no sessions this week) -->
    <p v-else class="mt-3 text-sm text-muted-foreground">
      No sessions planned this week.
      <!-- TODO(9-6+): link to /training once the plan-authoring UI ships -->
    </p>
  </div>
</template>
