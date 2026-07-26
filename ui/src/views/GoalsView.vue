<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { Plus } from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import GoalsEventCard from '@/components/goals/GoalsEventCard.vue'
import GoalsEventForm from '@/components/goals/GoalsEventForm.vue'
import GoalsGoalCard from '@/components/goals/GoalsGoalCard.vue'
import GoalsGoalForm from '@/components/goals/GoalsGoalForm.vue'
import { useGoalsStore } from '@/stores/goals'

const store = useGoalsStore()
const { events, goals, loading, error, upcomingEvents } = storeToRefs(store)

onMounted(() => {
  void store.loadAll()
})

// Local draft cards (no server id yet), keyed by a monotonic counter so each keeps a stable :key
// independent of its array position. A draft is dropped once it saves (its real row arrives from
// the store) or the user discards it. Mirrors ProfileGoalsSection.
let draftCounter = 0
const eventDrafts = ref<number[]>([])
const goalDrafts = ref<number[]>([])

function addEventDraft() {
  eventDrafts.value.push((draftCounter += 1))
}

function removeEventDraft(key: number) {
  eventDrafts.value = eventDrafts.value.filter((k) => k !== key)
}

function addGoalDraft() {
  goalDrafts.value.push((draftCounter += 1))
}

function removeGoalDraft(key: number) {
  goalDrafts.value = goalDrafts.value.filter((k) => k !== key)
}

// Existing rows show their read card by default; Edit reveals the form beneath it. The card stays
// visible so a save's re-fetch is immediately reflected above the open form.
const editingEvents = ref(new Set<string>())
const editingGoals = ref(new Set<string>())

function toggleEventEdit(id: string) {
  if (!editingEvents.value.delete(id)) editingEvents.value.add(id)
}

function toggleGoalEdit(id: string) {
  if (!editingGoals.value.delete(id)) editingGoals.value.add(id)
}

// Whatever `upcomingEvents` didn't claim is past — derived from the store's filter rather than
// re-deriving "today", so both lists always agree. Date-desc, so the most recent sits nearest
// the upcoming group.
const pastEvents = computed(() => {
  const upcoming = new Set(upcomingEvents.value.map((e) => e.id))
  return (events.value ?? [])
    .filter((e) => !upcoming.has(e.id))
    .sort((a, b) => b.eventDate.localeCompare(a.eventDate))
})

// A pending draft counts as content — otherwise adding the first event would render the
// empty state and swallow the form.
const isEmpty = computed(
  () =>
    !loading.value &&
    !error.value &&
    (events.value?.length ?? 0) === 0 &&
    (goals.value?.length ?? 0) === 0 &&
    eventDrafts.value.length === 0 &&
    goalDrafts.value.length === 0,
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
        <Button variant="outline" size="sm" @click="addEventDraft">
          <Plus />
          Add Event
        </Button>
        <Button variant="outline" size="sm" @click="addGoalDraft">
          <Plus />
          Add Goal
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
          <Button variant="outline" size="sm" @click="addEventDraft">
            <Plus />
            Add Event
          </Button>
        </header>

        <GoalsEventForm
          v-for="key in eventDrafts"
          :key="`event-draft-${key}`"
          @remove="removeEventDraft(key)"
          @created="removeEventDraft(key)"
        />

        <p
          v-if="(events?.length ?? 0) === 0 && eventDrafts.length === 0"
          class="text-sm text-muted-foreground"
        >
          No events yet.
        </p>

        <div v-for="event in upcomingEvents" :key="event.id" class="flex flex-col gap-2">
          <GoalsEventCard :event="event" />
          <div class="flex justify-end">
            <Button variant="ghost" size="sm" @click="toggleEventEdit(event.id)">
              {{ editingEvents.has(event.id) ? 'Close' : 'Edit' }}
            </Button>
          </div>
          <GoalsEventForm v-if="editingEvents.has(event.id)" :event="event" />
        </div>

        <template v-if="pastEvents.length > 0">
          <h3 class="eyebrow pt-2 text-faint">Past events</h3>
          <div v-for="event in pastEvents" :key="event.id" class="flex flex-col gap-2 opacity-60">
            <GoalsEventCard :event="event" />
            <div class="flex justify-end">
              <Button variant="ghost" size="sm" @click="toggleEventEdit(event.id)">
                {{ editingEvents.has(event.id) ? 'Close' : 'Edit' }}
              </Button>
            </div>
            <GoalsEventForm v-if="editingEvents.has(event.id)" :event="event" />
          </div>
        </template>
      </section>

      <!-- Goals -->
      <section class="flex flex-col gap-3">
        <header class="flex items-center gap-3">
          <h2 class="eyebrow">Goals</h2>
          <div class="flex-1" />
          <Button variant="outline" size="sm" @click="addGoalDraft">
            <Plus />
            Add Goal
          </Button>
        </header>

        <GoalsGoalForm
          v-for="key in goalDrafts"
          :key="`goal-draft-${key}`"
          @remove="removeGoalDraft(key)"
          @created="removeGoalDraft(key)"
        />

        <p
          v-if="(goals?.length ?? 0) === 0 && goalDrafts.length === 0"
          class="text-sm text-muted-foreground"
        >
          No goals yet.
        </p>

        <!-- Server returns goals target-date ascending, undated last — no client sort needed. -->
        <div v-for="goal in goals ?? []" :key="goal.id" class="flex flex-col gap-2">
          <GoalsGoalCard :goal="goal" />
          <div class="flex justify-end">
            <Button variant="ghost" size="sm" @click="toggleGoalEdit(goal.id)">
              {{ editingGoals.has(goal.id) ? 'Close' : 'Edit' }}
            </Button>
          </div>
          <GoalsGoalForm v-if="editingGoals.has(goal.id)" :goal="goal" />
        </div>
      </section>
    </template>
  </AppShell>
</template>
