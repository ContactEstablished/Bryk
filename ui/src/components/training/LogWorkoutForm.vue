<script setup lang="ts">
import { computed, ref } from 'vue'
import { useFieldArray, useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import {
  Bike,
  CheckCircle2,
  Dumbbell,
  Footprints,
  Medal,
  Waves,
  type LucideIcon,
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import RpeSelector from '@/components/common/RpeSelector.vue'
import { useTrainingStore } from '@/stores/training'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { logWorkoutSchema, type LogWorkoutFormValues } from '@/schemas/workouts'
import type {
  LogWorkoutRequest,
  PlannedWorkoutResponse,
  PlannedSport,
  WorkoutResponse,
} from '@/types/training'

// Launched from a planned workout: seeds a step-result row per planned step + shows planned-vs-actual.
// Launched with `workout`: edit mode — pre-fills from the workout and saves via PUT instead of logging.
const props = defineProps<{
  plannedWorkout?: PlannedWorkoutResponse | null
  workout?: WorkoutResponse | null
}>()
const emit = defineEmits<{ logged: []; saved: []; close: [] }>()

const isEdit = computed(() => props.workout != null)

const store = useTrainingStore()

function utcTodayIso(): string {
  const now = new Date()
  const yyyy = now.getUTCFullYear()
  const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(now.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

const sport = ref<PlannedSport>(props.workout?.sport ?? props.plannedWorkout?.sport ?? 'Run')
const isPlanned = computed(() => props.plannedWorkout != null)

const sportOptions: { value: PlannedSport; label: string; icon: LucideIcon }[] = [
  { value: 'Swim', label: 'Swim', icon: Waves },
  { value: 'Bike', label: 'Bike', icon: Bike },
  { value: 'Run', label: 'Run', icon: Footprints },
  { value: 'Triathlon', label: 'Triathlon', icon: Medal },
  { value: 'Strength', label: 'Strength', icon: Dumbbell },
]

// Flatten the planned steps (for seeding + the planned-vs-actual labels).
const plannedSteps = computed(() =>
  (props.plannedWorkout?.blocks ?? [])
    .slice()
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .flatMap((b) => b.steps.slice().sort((s1, s2) => s1.orderIndex - s2.orderIndex)),
)

function emptyResult(workoutStepId: string | null = null) {
  return {
    workoutStepId,
    actualDurationSeconds: null,
    actualDistanceMeters: null,
    avgPower: null,
    avgHr: null,
    avgPace: null,
    rpe: null,
  }
}

// Edit mode pre-fills from the workout (incl. its step results); otherwise seed from the planned steps.
const initialStepResults = props.workout
  ? props.workout.stepResults.map((r) => ({
      workoutStepId: r.workoutStepId,
      actualDurationSeconds: r.actualDurationSeconds,
      actualDistanceMeters: r.actualDistanceMeters,
      avgPower: r.avgPower,
      avgHr: r.avgHr,
      avgPace: r.avgPace,
      rpe: r.rpe,
    }))
  : plannedSteps.value.map((s) => emptyResult(s.id))

const form = useForm<LogWorkoutFormValues>({
  validationSchema: toTypedSchema(logWorkoutSchema),
  initialValues: {
    completedDate: props.workout?.completedDate ?? utcTodayIso(),
    actualDurationSeconds: props.workout?.actualDurationSeconds ?? null,
    actualDistanceMeters: props.workout?.actualDistanceMeters ?? null,
    avgHr: props.workout?.avgHr ?? null,
    maxHr: props.workout?.maxHr ?? null,
    loadOverride: props.workout?.loadOverride ?? null,
    rpe: props.workout?.rpe ?? null,
    notes: props.workout?.notes ?? null,
    stepResults: initialStepResults,
  },
})

const { fields: stepResults, push, remove } = useFieldArray('stepResults')

const globalError = ref<string | null>(null)
const justLogged = ref(false)

function plannedLabel(index: number): string {
  const s = plannedSteps.value[index]
  if (!s) return ''
  if (sport.value === 'Strength') {
    return `Planned: ${s.sets ?? '–'}×${s.reps ?? '–'}${s.loadKg != null ? ` @ ${s.loadKg}kg` : ''}`
  }
  const bits: string[] = []
  if (s.targetZone != null) bits.push(`Z${s.targetZone}`)
  if (s.durationSeconds != null) bits.push(`${s.durationSeconds}s`)
  if (s.distanceMeters != null) bits.push(`${s.distanceMeters}m`)
  return bits.length ? `Planned: ${bits.join(' · ')}` : ''
}

function addStepResult() {
  push(emptyResult())
}

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const req: LogWorkoutRequest = {
    sport: sport.value,
    completedDate: values.completedDate,
    plannedWorkoutId: props.workout?.plannedWorkoutId ?? props.plannedWorkout?.id ?? null,
    actualDurationSeconds: values.actualDurationSeconds ?? null,
    actualDistanceMeters: values.actualDistanceMeters ?? null,
    avgHr: values.avgHr ?? null,
    maxHr: values.maxHr ?? null,
    loadOverride: values.loadOverride ?? null,
    rpe: values.rpe ?? null,
    notes: values.notes ?? null,
    stepResults: values.stepResults.map((r) => ({
      workoutStepId: r.workoutStepId ?? null,
      actualDurationSeconds: r.actualDurationSeconds ?? null,
      actualDistanceMeters: r.actualDistanceMeters ?? null,
      avgPower: r.avgPower ?? null,
      avgHr: r.avgHr ?? null,
      avgPace: r.avgPace ?? null,
      rpe: r.rpe ?? null,
    })),
  }

  try {
    if (props.workout) {
      await store.updateWorkout(props.workout.id, req)
      justLogged.value = true
      emit('saved')
    } else {
      await store.logWorkout(req)
      justLogged.value = true
      emit('logged')
    }
  } catch (e) {
    const verb = isEdit.value ? 'save' : 'log'
    const messages = extractApiValidationMessages(e)
    globalError.value = messages
      ? messages.join(' ')
      : e instanceof ApiError
        ? `Couldn't ${verb}: ${e.statusText} (${e.status})`
        : e instanceof Error
          ? `Couldn't ${verb}: ${e.message}`
          : `Couldn't ${verb} — please try again.`
  }
})

const isSubmitting = form.isSubmitting
</script>

<template>
  <section class="rounded-[10px] border border-border bg-[#0e1218] p-4 space-y-4">
    <div class="flex items-center justify-between">
      <h4 class="text-sm font-semibold">
        <template v-if="isEdit">Edit workout</template>
        <template v-else>Log workout<span v-if="isPlanned && plannedWorkout"> &middot; {{ plannedWorkout.title }}</span></template>
      </h4>
      <Button type="button" variant="ghost" size="sm" @click="emit('close')">Close</Button>
    </div>

    <form class="space-y-4" @submit="onSubmit">
      <!-- Sport: selectable only when unplanned. -->
      <div v-if="!isPlanned" class="space-y-2">
        <span class="eyebrow block">Sport</span>
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="opt in sportOptions"
            :key="opt.value"
            type="button"
            class="inline-flex items-center gap-1.5 rounded-full border px-3 py-2 text-[13px] font-medium transition-all duration-[120ms]"
            :class="
              sport === opt.value
                ? 'border-primary bg-primary-glow text-primary-hi'
                : 'border-border-strong bg-[#0d1015] text-subtle hover:border-[#3a4252] hover:text-foreground'
            "
            :aria-pressed="sport === opt.value"
            @click="sport = opt.value"
          >
            <component :is="opt.icon" :size="14" />
            {{ opt.label }}
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <FormField v-slot="{ componentField }" name="completedDate">
          <FormItem>
            <FormLabel>Completed date</FormLabel>
            <FormControl><Input type="date" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
      </div>

      <div class="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <FormField v-slot="{ componentField }" name="actualDurationSeconds">
          <FormItem><FormLabel>Duration (sec)</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" name="actualDistanceMeters">
          <FormItem><FormLabel>Distance (m)</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" name="avgHr">
          <FormItem><FormLabel>Avg HR</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" name="maxHr">
          <FormItem><FormLabel>Max HR</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" name="loadOverride">
          <FormItem><FormLabel>Load (override)</FormLabel><FormControl><Input type="number" step="0.01" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
        </FormField>
      </div>

      <!-- Perceived effort: tap-to-select 1-10 grid (decimal RPE stays on per-step rows). -->
      <FormField v-slot="{ value, handleChange }" name="rpe">
        <FormItem>
          <FormLabel>
            Perceived effort · RPE<span
              v-if="value != null"
              class="ml-1 font-mono normal-case tracking-normal text-subtle"
            >{{ value }}</span>
          </FormLabel>
          <FormControl>
            <RpeSelector
              :model-value="(value as number | null) ?? null"
              @update:model-value="handleChange"
            />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="notes">
        <FormItem><FormLabel>Notes</FormLabel><FormControl><Input v-bind="componentField" /></FormControl><FormMessage /></FormItem>
      </FormField>

      <!-- Per-step actuals (optional; seeded from the plan for comparison). -->
      <fieldset class="space-y-3">
        <legend class="eyebrow">Per-step actuals</legend>
        <div v-if="stepResults.length === 0" class="text-xs text-muted-foreground/60">No per-step actuals.</div>

        <div
          v-for="(field, index) in stepResults"
          :key="field.key"
          class="rounded-md border border-dashed p-3 space-y-2"
        >
          <div class="flex items-center justify-between">
            <span class="text-xs font-medium">Step result {{ index + 1 }}</span>
            <span v-if="plannedLabel(index)" class="text-[11px] text-muted-foreground">{{ plannedLabel(index) }}</span>
            <Button type="button" variant="ghost" size="sm" @click="remove(index)">Remove</Button>
          </div>
          <div class="grid grid-cols-2 gap-2 sm:grid-cols-3">
            <FormField v-slot="{ componentField }" :name="`stepResults[${index}].actualDurationSeconds`">
              <FormItem><FormLabel>Dur (s)</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
            </FormField>
            <FormField v-slot="{ componentField }" :name="`stepResults[${index}].avgPower`">
              <FormItem><FormLabel>Power</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
            </FormField>
            <FormField v-slot="{ componentField }" :name="`stepResults[${index}].avgHr`">
              <FormItem><FormLabel>HR</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
            </FormField>
            <FormField v-slot="{ componentField }" :name="`stepResults[${index}].avgPace`">
              <FormItem><FormLabel>Pace</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
            </FormField>
            <FormField v-slot="{ componentField }" :name="`stepResults[${index}].actualDistanceMeters`">
              <FormItem><FormLabel>Dist (m)</FormLabel><FormControl><Input type="number" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
            </FormField>
            <FormField v-slot="{ componentField }" :name="`stepResults[${index}].rpe`">
              <FormItem><FormLabel>RPE</FormLabel><FormControl><Input type="number" step="0.1" v-bind="componentField" /></FormControl><FormMessage /></FormItem>
            </FormField>
          </div>
        </div>

        <Button type="button" variant="outline" size="sm" @click="addStepResult">Add step result</Button>
      </fieldset>

      <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ globalError }}
      </div>

      <div class="flex items-center justify-end gap-3">
        <span v-if="justLogged" class="flex items-center gap-1 text-sm text-muted-foreground">
          <CheckCircle2 :size="16" class="text-primary" />
          {{ isEdit ? 'Saved' : 'Logged' }}
        </span>
        <Button type="submit" :disabled="isSubmitting">{{ isEdit ? 'Save changes' : 'Log workout' }}</Button>
      </div>
    </form>
  </section>
</template>
