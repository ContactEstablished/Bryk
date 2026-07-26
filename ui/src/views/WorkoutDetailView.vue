<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft } from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import MetricTile from '@/components/common/MetricTile.vue'
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import LogWorkoutForm from '@/components/training/LogWorkoutForm.vue'
import { useTrainingStore } from '@/stores/training'
import { useActivityFilesStore } from '@/stores/activityFiles'
import { ApiError } from '@/services/api'
import type { WorkoutStepResponse } from '@/types/training'

const route = useRoute()
const router = useRouter()
const store = useTrainingStore()
const activityFilesStore = useActivityFilesStore()

const id = computed(() => String(route.params.id))
const editing = ref(false)
const confirmingDelete = ref(false)
const deleteError = ref<string | null>(null)

async function load() {
  await store.loadWorkout(id.value)
  // The "from file" badge is a reverse lookup (ADR-0010 §4) — there is no field on the workout read.
  await activityFilesStore.loadSource(id.value)
  const w = store.currentWorkout
  if (w?.plannedWorkoutId && w.trainingPlanId) {
    await store.loadStructure(w.trainingPlanId, w.plannedWorkoutId)
  }
}

onMounted(load)

const workout = computed(() => store.currentWorkout)

// Planned steps for this workout's linked structure (flattened, ordered).
const plannedSteps = computed<WorkoutStepResponse[]>(() => {
  const s = store.structure
  if (!s || !workout.value || s.id !== workout.value.plannedWorkoutId) return []
  return (s.blocks ?? [])
    .slice()
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .flatMap((b) => b.steps.slice().sort((s1, s2) => s1.orderIndex - s2.orderIndex))
})

// Zip planned steps to step results by workoutStepId; append any unmatched (ad-hoc) actuals.
const comparisonRows = computed(() => {
  const results = workout.value?.stepResults ?? []
  const used = new Set<string>()
  const planned = plannedSteps.value.map((step, i) => {
    const result = results.find((r) => r.workoutStepId === step.id) ?? null
    if (result) used.add(result.id)
    return { key: step.id, index: i + 1, step, result }
  })
  const extra = results
    .filter((r) => !used.has(r.id))
    .map((result, i) => ({ key: result.id, index: planned.length + i + 1, step: null, result }))
  return [...planned, ...extra]
})

const hasComparison = computed(() => comparisonRows.value.length > 0)

function formatDuration(totalSeconds: number | null): string | null {
  if (totalSeconds == null) return null
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = Math.floor(totalSeconds % 60)
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`
}

function formatDistance(meters: number | null): string | null {
  return meters == null ? null : `${(meters / 1000).toFixed(1)} km`
}

// avgPace / planned pace are sec per unit (run = /km, swim = /100m) → M:SS.
function formatPace(secPerUnit: number | null): string {
  if (secPerUnit == null) return '—'
  const m = Math.floor(secPerUnit / 60)
  const s = Math.floor(secPerUnit % 60)
  return `${m}:${String(s).padStart(2, '0')}`
}

function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
}

// Planned target summary for a step (cardio: zone/power/pace/duration; strength: sets×reps×load).
function plannedSummary(s: WorkoutStepResponse): string {
  if (workout.value?.sport === 'Strength') {
    return `${s.sets ?? '–'}×${s.reps ?? '–'}${s.loadKg != null ? ` @ ${s.loadKg}kg` : ''}`
  }
  const bits: string[] = []
  if (s.targetZone != null) bits.push(`Z${s.targetZone}`)
  if (s.targetPowerLow != null || s.targetPowerHigh != null) {
    bits.push(`${s.targetPowerLow ?? ''}–${s.targetPowerHigh ?? ''}W`)
  }
  if (s.targetPaceLow != null || s.targetPaceHigh != null) {
    bits.push(`${formatPace(s.targetPaceLow)}–${formatPace(s.targetPaceHigh)}`)
  }
  if (s.durationSeconds != null) bits.push(formatDuration(s.durationSeconds) ?? '')
  if (s.distanceMeters != null) bits.push(`${s.distanceMeters}m`)
  return bits.filter(Boolean).join(' · ') || '—'
}

function stepLabel(s: WorkoutStepResponse, index: number): string {
  return s.title || s.intent || `Step ${index}`
}

async function onSaved() {
  editing.value = false
  await load()
}

async function confirmDelete() {
  deleteError.value = null
  try {
    await store.deleteWorkout(id.value)
    await router.push('/workouts')
  } catch (e) {
    confirmingDelete.value = false
    deleteError.value =
      e instanceof ApiError ? `Couldn't delete: ${e.statusText} (${e.status})` : "Couldn't delete — please try again."
  }
}
</script>

<template>
  <AppShell title="Workout" subtitle="Planned vs. actual for a completed session.">
    <div class="mx-auto w-full max-w-3xl space-y-5">
      <RouterLink
        to="/workouts"
        class="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft :size="14" /> Back to workouts
      </RouterLink>

      <p v-if="store.loadingCurrent && !workout" class="card-surface p-6 text-sm text-muted-foreground">
        Loading…
      </p>

      <p v-else-if="!workout" class="card-surface p-6 text-sm text-muted-foreground">
        Workout not found.
      </p>

      <template v-else>
        <!-- Header -->
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div class="flex items-center gap-3">
            <TypePill :kind="sportToPillKind(workout.sport)">{{ workout.sport }}</TypePill>
            <span
              v-if="activityFilesStore.source"
              class="rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-muted-foreground"
              :title="activityFilesStore.source.fileName"
            >
              from file
            </span>
            <span class="font-mono text-sm text-muted-foreground">{{ formatDay(workout.completedDate) }}</span>
          </div>
          <div class="flex items-center gap-2">
            <Button v-if="!editing" type="button" variant="outline" size="sm" @click="editing = true">Edit</Button>
            <Button
              v-if="!confirmingDelete"
              type="button"
              variant="ghost"
              size="sm"
              @click="confirmingDelete = true"
            >
              Delete
            </Button>
            <template v-else>
              <span class="text-sm text-muted-foreground">Delete this workout?</span>
              <Button type="button" variant="destructive" size="sm" @click="confirmDelete">Confirm</Button>
              <Button type="button" variant="ghost" size="sm" @click="confirmingDelete = false">Cancel</Button>
            </template>
          </div>
        </div>

        <p v-if="deleteError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {{ deleteError }}
        </p>

        <!-- Metric strip -->
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <MetricTile label="Load" :value="workout.effectiveLoad" unit="TSS" />
          <MetricTile label="Duration" :value="formatDuration(workout.actualDurationSeconds)" />
          <MetricTile label="Distance" :value="formatDistance(workout.actualDistanceMeters)" />
          <MetricTile label="Avg HR" :value="workout.avgHr" unit="bpm" />
        </div>

        <!-- Edit form -->
        <LogWorkoutForm v-if="editing" :workout="workout" @saved="onSaved" @close="editing = false" />

        <!-- Notes -->
        <div v-if="workout.notes" class="card-surface p-5">
          <h3 class="eyebrow mb-2">Notes</h3>
          <p class="whitespace-pre-line text-sm text-foreground">{{ workout.notes }}</p>
        </div>

        <!-- Planned vs actual -->
        <div class="card-surface overflow-hidden">
          <div class="border-b border-border px-6 py-4">
            <h3 class="text-sm font-semibold">Planned vs. actual</h3>
          </div>

          <div v-if="hasComparison" class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-border text-left">
                  <th class="px-4 py-2 font-medium text-muted-foreground">Step</th>
                  <th class="px-4 py-2 font-medium text-muted-foreground">Planned</th>
                  <th class="px-4 py-2 text-right font-medium text-muted-foreground">Power</th>
                  <th class="px-4 py-2 text-right font-medium text-muted-foreground">Pace</th>
                  <th class="px-4 py-2 text-right font-medium text-muted-foreground">HR</th>
                  <th class="px-4 py-2 text-right font-medium text-muted-foreground">Dur</th>
                  <th class="px-4 py-2 text-right font-medium text-muted-foreground">RPE</th>
                </tr>
              </thead>
              <tbody class="font-mono text-[13px]">
                <tr v-for="row in comparisonRows" :key="row.key" class="border-b border-border last:border-0">
                  <td class="px-4 py-2.5 font-sans">
                    {{ row.step ? stepLabel(row.step, row.index) : `Step ${row.index}` }}
                  </td>
                  <td class="px-4 py-2.5 text-muted-foreground">{{ row.step ? plannedSummary(row.step) : '—' }}</td>
                  <td class="px-4 py-2.5 text-right">{{ row.result?.avgPower ?? '—' }}</td>
                  <td class="px-4 py-2.5 text-right">{{ row.result?.avgPace != null ? formatPace(row.result.avgPace) : '—' }}</td>
                  <td class="px-4 py-2.5 text-right">{{ row.result?.avgHr ?? '—' }}</td>
                  <td class="px-4 py-2.5 text-right">{{ formatDuration(row.result?.actualDurationSeconds ?? null) ?? '—' }}</td>
                  <td class="px-4 py-2.5 text-right">{{ row.result?.rpe ?? '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <p v-else class="px-6 py-6 text-sm text-muted-foreground">No per-step detail for this workout.</p>
        </div>
      </template>
    </div>
  </AppShell>
</template>
