<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useProfileStore } from '@/stores/profile'
import { useCountUp } from '@/composables/useCountUp'
import ProgressRing from '@/components/common/ProgressRing.vue'

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

// Rolling-horizon fill: an EventResponse carries no creation or plan-start date, so the dashboard
// card cannot compute a true [start, target] elapsed fraction. Approximate against a fixed look-back
// horizon — the honest date-based signal until GoalsView (17-3), which has the linked plan's
// startDate, passes the real window. Driven by days rather than weeks so it advances daily.
const HORIZON_DAYS = 168 // 24 weeks

const fraction = computed(() =>
  days.value != null ? Math.min(1, Math.max(0, 1 - days.value / HORIZON_DAYS)) : 0,
)
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

      <!-- Countdown through the shared ring; race day / eve swaps the centre for a headline -->
      <ProgressRing class="mt-5" :fraction="fraction">
        <template #center>
          <span
            v-if="days != null && days <= 1"
            class="bg-[linear-gradient(180deg,var(--bryk-fg-0),#888c98)] bg-clip-text text-3xl font-bold leading-[0.9] tracking-[-0.05em] text-transparent"
          >
            {{ days <= 0 ? 'Today' : 'Tomorrow' }}
          </span>
          <template v-else>
            <span
              class="bg-[linear-gradient(180deg,var(--bryk-fg-0),#888c98)] bg-clip-text text-4xl font-bold leading-[0.9] tracking-[-0.05em] tabular-nums text-transparent"
            >
              {{ animatedWeeks }}
            </span>
            <span class="eyebrow">weeks to go</span>
            <span class="font-mono text-xs text-subtle">{{ days }} days</span>
          </template>
        </template>
      </ProgressRing>
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
