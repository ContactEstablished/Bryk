<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Button } from '@/components/ui/button'
import { useProfileStore } from '@/stores/profile'
import ProfileEventCard from '@/components/profile/ProfileEventCard.vue'
import ProfileGoalCard from '@/components/profile/ProfileGoalCard.vue'

const store = useProfileStore()

// Local draft cards (no server id yet), keyed by a monotonic counter so each keeps a
// stable :key independent of its array position. A draft is dropped once it saves
// (its real row arrives from the store) or the user discards it.
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

onMounted(() => {
  void store.loadGoals()
})
</script>

<template>
  <section class="card-surface p-6">
    <h2 class="text-2xl font-semibold tracking-[-0.02em]">Goals</h2>
    <p class="mt-2 text-sm text-muted-foreground">
      Your upcoming events and training goals.
    </p>

    <template v-if="store.goals">
      <!-- Events -->
      <fieldset class="mt-6">
        <legend class="eyebrow">Events</legend>

        <div
          v-if="(store.goals?.events.length ?? 0) === 0 && eventDrafts.length === 0"
          class="mt-3 text-sm text-muted-foreground/60"
        >
          No events added yet.
        </div>

        <div class="mt-4 space-y-4">
          <ProfileEventCard
            v-for="ev in store.goals?.events ?? []"
            :key="ev.id"
            :event="ev"
          />
          <ProfileEventCard
            v-for="key in eventDrafts"
            :key="`event-draft-${key}`"
            @remove="removeEventDraft(key)"
            @created="removeEventDraft(key)"
          />
        </div>

        <Button type="button" variant="outline" class="mt-4" @click="addEventDraft">Add Event</Button>
      </fieldset>

      <!-- Goals -->
      <fieldset class="mt-10">
        <legend class="eyebrow">Goals</legend>

        <div
          v-if="(store.goals?.goals.length ?? 0) === 0 && goalDrafts.length === 0"
          class="mt-3 text-sm text-muted-foreground/60"
        >
          No goals added yet.
        </div>

        <div class="mt-4 space-y-4">
          <ProfileGoalCard
            v-for="g in store.goals?.goals ?? []"
            :key="g.id"
            :goal="g"
          />
          <ProfileGoalCard
            v-for="key in goalDrafts"
            :key="`goal-draft-${key}`"
            @remove="removeGoalDraft(key)"
            @created="removeGoalDraft(key)"
          />
        </div>

        <Button type="button" variant="outline" class="mt-4" @click="addGoalDraft">Add Goal</Button>
      </fieldset>
    </template>

    <div v-else-if="store.goalsError" class="mt-4">
      <p class="text-sm text-destructive">Couldn't load your events and goals.</p>
      <Button class="mt-3" variant="outline" @click="store.loadGoals()">Retry</Button>
    </div>

    <p v-else class="mt-4 text-sm text-muted-foreground">
      Loading…
    </p>
  </section>
</template>
