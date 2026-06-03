<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useProfileStore } from '@/stores/profile'

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

const countdown = computed(() => {
  const ev = store.primaryEvent
  if (!ev) return ''
  const days = daysUntil(ev.eventDate)
  if (days <= 0) return 'Today'
  if (days === 1) return 'Tomorrow'
  const weeks = Math.ceil(days / 7)
  return weeks === 1 ? '1 week out' : `${weeks} weeks out`
})
</script>

<template>
  <div class="rounded-lg border bg-card p-5">
    <h3 class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
      Primary Goal
    </h3>

    <!-- Populated -->
    <div v-if="store.primaryEvent" class="mt-3">
      <p class="text-xl font-semibold">{{ store.primaryEvent.name }}</p>
      <p class="mt-1 text-sm text-muted-foreground">
        <span v-if="store.primaryEvent.sport">{{ store.primaryEvent.sport }} &middot; </span>{{ formattedDate }}
      </p>
      <p class="mt-3 font-mono text-2xl font-semibold">{{ countdown }}</p>
    </div>

    <!-- Loading (goals not yet fetched) -->
    <p v-else-if="!store.goals" class="mt-3 text-sm text-muted-foreground">
      Loading…
    </p>

    <!-- Empty (no upcoming events) -->
    <div v-else class="mt-3">
      <p class="text-sm text-muted-foreground">No upcoming events.</p>
      <router-link
        to="/profile"
        class="mt-2 inline-block text-sm font-medium text-primary hover:underline"
      >
        Set a goal
      </router-link>
    </div>
  </div>
</template>
