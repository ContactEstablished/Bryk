<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useProfileStore } from '@/stores/profile'
import { useCountUp } from '@/composables/useCountUp'

const store = useProfileStore()

onMounted(() => {
  if (!store.goals) void store.loadGoals()
})

// Whole days from today (UTC) to the event date. Parse the YYYY-MM-DD string as a
// UTC calendar date so the diff is timezone-stable and matches the server's DateOnly.
function daysUntil(eventDate: string): number {
  const [y, m, d] = eventDate.split('-').map(Number)
  const event = Date.UTC(y, m - 1, d)
  const now = new Date()
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())
  return Math.round((event - today) / 86_400_000)
}

const formattedDate = computed(() => {
  const ev = store.primaryEvent
  if (!ev) return ''
  const [y, m, d] = ev.eventDate.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
})

const days = computed(() => {
  const ev = store.primaryEvent
  return ev ? daysUntil(ev.eventDate) : null
})

const weeks = computed(() => (days.value != null ? Math.ceil(days.value / 7) : null))
const animatedWeeks = useCountUp(weeks)
</script>

<template>
  <div class="card-surface card-glow flex flex-col gap-4 p-6">
    <h3 class="eyebrow">Primary Goal</h3>

    <!-- Populated -->
    <div v-if="store.primaryEvent">
      <p class="text-lg font-bold tracking-[-0.02em]">{{ store.primaryEvent.name }}</p>
      <p class="mt-1 font-mono text-xs text-muted-foreground">
        <span v-if="store.primaryEvent.sport">{{ store.primaryEvent.sport }} · </span>{{ formattedDate }}
      </p>

      <!-- Race day / eve: headline instead of the week counter -->
      <p
        v-if="days != null && days <= 1"
        class="mt-5 bg-[linear-gradient(180deg,var(--bryk-fg-0),#888c98)] bg-clip-text text-5xl font-bold leading-[0.9] tracking-[-0.05em] text-transparent"
      >
        {{ days <= 0 ? 'Today' : 'Tomorrow' }}
      </p>

      <div v-else class="mt-5 flex items-baseline gap-3">
        <span
          class="bg-[linear-gradient(180deg,var(--bryk-fg-0),#888c98)] bg-clip-text text-[88px] font-bold leading-[0.9] tracking-[-0.05em] tabular-nums text-transparent"
        >
          {{ animatedWeeks }}
        </span>
        <span class="flex flex-col gap-1">
          <span class="eyebrow">weeks to go</span>
          <span class="font-mono text-xs text-subtle">{{ days }} days</span>
        </span>
      </div>
    </div>

    <!-- Loading (goals not yet fetched) -->
    <p v-else-if="!store.goals" class="text-sm text-muted-foreground">Loading…</p>

    <!-- Empty (no upcoming events) -->
    <div v-else>
      <p class="text-sm text-muted-foreground">No upcoming events.</p>
      <router-link
        to="/profile"
        class="mt-2 inline-block text-sm font-medium text-primary-hi hover:underline"
      >Set a goal</router-link>
    </div>
  </div>
</template>
