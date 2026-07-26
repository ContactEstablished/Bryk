<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { Plus } from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import GoalsEventCard from '@/components/goals/GoalsEventCard.vue'
import GoalsGoalCard from '@/components/goals/GoalsGoalCard.vue'
import { useGoalsStore } from '@/stores/goals'

const store = useGoalsStore()
const { events, goals, loading, error, upcomingEvents } = storeToRefs(store)

onMounted(() => {
  void store.loadAll()
})

// Whatever `upcomingEvents` didn't claim is past — derived from the store's filter rather than
// re-deriving "today", so both lists always agree. Date-desc, so the most recent sits nearest
// the upcoming group.
const pastEvents = computed(() => {
  const upcoming = new Set(upcomingEvents.value.map((e) => e.id))
  return (events.value ?? [])
    .filter((e) => !upcoming.has(e.id))
    .sort((a, b) => b.eventDate.localeCompare(a.eventDate))
})

const isEmpty = computed(
  () =>
    !loading.value &&
    !error.value &&
    (events.value?.length ?? 0) === 0 &&
    (goals.value?.length ?? 0) === 0,
)
</script>

<template>
  <AppShell title="Goals" subtitle="Events, goals &amp; countdowns">
    <!-- Loading skeleton -->
    <template v-if="loading && !events">
      <div class="flex flex-col gap-3">
        <div v-for="i in 3" :key="`ev-${i}`" class="h-[148px] animate-pulse rounded-lg bg-muted" />
      </div>
      <div class="flex flex-col gap-3">
        <div v-for="i in 3" :key="`gl-${i}`" class="h-[104px] animate-pulse rounded-lg bg-muted" />
      </div>
    </template>

    <!-- Error state -->
    <div v-else-if="error" class="rounded-lg border border-border bg-muted p-6 text-center">
      <p class="text-sm font-semibold text-foreground">Could not load goals</p>
      <p class="mt-1 font-mono text-[11px] text-muted-foreground">{{ error.message }}</p>
      <Button variant="outline" size="sm" class="mt-4" @click="store.loadAll()">Retry</Button>
    </div>

    <!-- Empty state (fresh athlete) -->
    <div v-else-if="isEmpty" class="py-16 text-center">
      <p class="text-[15px] font-semibold text-foreground">Nothing on the calendar yet</p>
      <p class="mt-1 text-sm text-muted-foreground">
        Add your first event or goal to start a countdown.
      </p>
      <div class="mt-4 flex items-center justify-center gap-2">
        <!-- 17-4 wires these to the event/goal forms -->
        <Button variant="outline" size="sm" disabled title="Coming soon">
          <Plus />
          Add event
        </Button>
        <Button variant="outline" size="sm" disabled title="Coming soon">
          <Plus />
          Add goal
        </Button>
      </div>
    </div>

    <!-- Content -->
    <template v-else>
      <!-- Events -->
      <section class="flex flex-col gap-3">
        <header class="flex items-center gap-3">
          <h2 class="eyebrow">Events</h2>
          <div class="flex-1" />
          <Button variant="outline" size="sm" disabled title="Coming soon">
            <Plus />
            Add event
          </Button>
        </header>

        <!-- 17-4 mounts the event form here -->

        <p v-if="(events?.length ?? 0) === 0" class="text-sm text-muted-foreground">
          No events yet.
        </p>

        <GoalsEventCard v-for="event in upcomingEvents" :key="event.id" :event="event" />

        <template v-if="pastEvents.length > 0">
          <h3 class="eyebrow pt-2 text-faint">Past events</h3>
          <div class="flex flex-col gap-3 opacity-60">
            <GoalsEventCard v-for="event in pastEvents" :key="event.id" :event="event" />
          </div>
        </template>
      </section>

      <!-- Goals -->
      <section class="flex flex-col gap-3">
        <header class="flex items-center gap-3">
          <h2 class="eyebrow">Goals</h2>
          <div class="flex-1" />
          <Button variant="outline" size="sm" disabled title="Coming soon">
            <Plus />
            Add goal
          </Button>
        </header>

        <!-- 17-4 mounts the goal form here -->

        <p v-if="(goals?.length ?? 0) === 0" class="text-sm text-muted-foreground">
          No goals yet.
        </p>

        <!-- Server returns goals target-date ascending, undated last — no client sort needed. -->
        <GoalsGoalCard v-for="goal in goals ?? []" :key="goal.id" :goal="goal" />
      </section>
    </template>
  </AppShell>
</template>
