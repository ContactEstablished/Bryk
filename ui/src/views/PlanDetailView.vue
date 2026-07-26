<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ArrowLeft } from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import WorkoutStructureBuilder from '@/components/training/WorkoutStructureBuilder.vue'
import PeriodizationPanel from '@/components/training/PeriodizationPanel.vue'
import { useTrainingStore } from '@/stores/training'
import type { PlannedSport, PlannedWorkoutResponse } from '@/types/training'

const route = useRoute()
const store = useTrainingStore()

const id = computed(() => String(route.params.id))
const plan = computed(() => store.currentPlan)

const buildTarget = ref<{
  planId: string
  plannedWorkoutId: string
  sport: PlannedSport
  title: string
} | null>(null)

onMounted(() => {
  void store.loadPlan(id.value)
})

function openBuilder(pw: PlannedWorkoutResponse) {
  if (!plan.value) return
  buildTarget.value = {
    planId: plan.value.id,
    plannedWorkoutId: pw.id,
    sport: pw.sport,
    title: pw.title,
  }
}

function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
}
</script>

<template>
  <AppShell title="Training Plan" subtitle="Browse planned workouts and edit their structure.">
    <div class="mx-auto w-full max-w-3xl space-y-5">
      <RouterLink
        to="/plans"
        class="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft :size="14" /> Back to plans
      </RouterLink>

      <p v-if="store.loadingPlan && !plan" class="card-surface p-6 text-sm text-muted-foreground">Loading…</p>

      <p v-else-if="!plan" class="card-surface p-6 text-sm text-muted-foreground">Plan not found.</p>

      <template v-else>
        <PeriodizationPanel :plan="plan" />

        <!-- Planned workouts -->
        <div class="card-surface">
          <div class="border-b border-border px-6 py-4">
            <h3 class="text-sm font-semibold">Planned workouts</h3>
          </div>

          <ul v-if="plan.plannedWorkouts.length > 0">
            <li
              v-for="(pw, i) in plan.plannedWorkouts"
              :key="pw.id"
              class="flex items-center justify-between gap-4 px-6 py-3.5"
              :class="i > 0 ? 'border-t border-border' : ''"
            >
              <div class="min-w-0">
                <div class="flex items-center gap-2">
                  <TypePill :kind="sportToPillKind(pw.sport)">{{ pw.sport }}</TypePill>
                  <span class="truncate text-sm font-semibold">{{ pw.title }}</span>
                </div>
                <p class="mt-0.5 flex flex-wrap gap-x-3 font-mono text-[11px] text-muted-foreground">
                  <span>{{ formatDay(pw.scheduledDate) }}</span>
                  <span v-if="pw.plannedDurationMinutes != null">{{ pw.plannedDurationMinutes }} min</span>
                  <span v-if="pw.effectiveLoad != null" class="text-primary-hi">{{ pw.effectiveLoad }} TSS</span>
                </p>
              </div>
              <Button type="button" variant="outline" size="sm" @click="openBuilder(pw)">Edit structure</Button>
            </li>
          </ul>

          <p v-else class="px-6 py-6 text-sm text-muted-foreground">This plan has no planned workouts.</p>
        </div>

        <!-- Structure builder, reopened against an existing planned workout (Task 10-5 component) -->
        <WorkoutStructureBuilder
          v-if="buildTarget"
          :key="buildTarget.plannedWorkoutId"
          :plan-id="buildTarget.planId"
          :planned-workout-id="buildTarget.plannedWorkoutId"
          :sport="buildTarget.sport"
          :title="buildTarget.title"
          @close="buildTarget = null"
        />
      </template>
    </div>
  </AppShell>
</template>
