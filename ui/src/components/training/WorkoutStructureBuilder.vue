<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useFieldArray, useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { CheckCircle2 } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import WorkoutBlockCard from './WorkoutBlockCard.vue'
import { useTrainingStore } from '@/stores/training'
import { useZonesStore } from '@/stores/zones'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import {
  buildWorkoutStructureSchema,
  type WorkoutStructureFormValues,
  type WorkoutBlockFormItem,
} from '@/schemas/training'
import type {
  PlannedSport,
  PlannedWorkoutResponse,
  WorkoutStructureRequest,
} from '@/types/training'

const props = defineProps<{
  planId: string
  plannedWorkoutId: string
  sport: PlannedSport
  title?: string
}>()
const emit = defineEmits<{ close: [] }>()

const trainingStore = useTrainingStore()
const zonesStore = useZonesStore()

const hasZones = computed(() => ['Bike', 'Run', 'Swim'].includes(props.sport))

const form = useForm<WorkoutStructureFormValues>({
  validationSchema: toTypedSchema(buildWorkoutStructureSchema(props.sport)),
  initialValues: { blocks: [] },
})

const { fields: blocks, push, remove } = useFieldArray<WorkoutBlockFormItem>('blocks')

// vee-validate's setFieldValue is strictly path-typed; cast once so child rows can set dynamic paths.
const setField = form.setFieldValue as unknown as (path: string, value: unknown) => void

const globalError = ref<string | null>(null)
const justSaved = ref(false)

function toFormValues(data: PlannedWorkoutResponse): WorkoutStructureFormValues {
  return {
    blocks: (data.blocks ?? []).map((b) => ({
      repeats: b.repeats,
      steps: b.steps.map((s) => ({
        intent: s.intent,
        title: s.title,
        durationSeconds: s.durationSeconds,
        distanceMeters: s.distanceMeters,
        targetZone: s.targetZone,
        targetPowerLow: s.targetPowerLow,
        targetPowerHigh: s.targetPowerHigh,
        targetHrLow: s.targetHrLow,
        targetHrHigh: s.targetHrHigh,
        targetPaceLow: s.targetPaceLow,
        targetPaceHigh: s.targetPaceHigh,
        sets: s.sets,
        reps: s.reps,
        loadKg: s.loadKg,
        rpe: s.rpe,
      })),
    })),
  }
}

// Seed the form once the structure for THIS workout arrives (mirrors the profile section pattern).
watch(
  () => trainingStore.structure,
  (data) => {
    if (data && data.id === props.plannedWorkoutId) {
      form.resetForm({ values: toFormValues(data) })
    }
  },
)

onMounted(() => {
  void trainingStore.loadStructure(props.planId, props.plannedWorkoutId)
  if (hasZones.value && !zonesStore.zones) void zonesStore.loadZones()
})

function addBlock() {
  justSaved.value = false
  push({ repeats: 1, steps: [] })
}

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const request: WorkoutStructureRequest = {
    blocks: values.blocks.map((b, bi) => ({
      orderIndex: bi,
      repeats: b.repeats,
      steps: b.steps.map((s) => ({ ...s, title: s.title ?? null })),
    })),
  }
  try {
    await trainingStore.saveStructure(props.planId, props.plannedWorkoutId, request)
    justSaved.value = true
  } catch (e) {
    const messages = extractApiValidationMessages(e)
    globalError.value = messages
      ? messages.join(' ')
      : e instanceof ApiError
        ? `Couldn't save: ${e.statusText} (${e.status})`
        : e instanceof Error
          ? `Couldn't save: ${e.message}`
          : "Couldn't save — please try again."
  }
})

const isSubmitting = form.isSubmitting
</script>

<template>
  <section class="rounded-lg border bg-card p-6 space-y-4">
    <div class="flex items-center justify-between">
      <div>
        <h3 class="text-lg font-semibold">Build workout<span v-if="title">: {{ title }}</span></h3>
        <p class="text-sm text-muted-foreground">
          {{ sport }} &middot; two-level blocks (a block repeats its steps). No nested blocks.
        </p>
      </div>
      <Button type="button" variant="ghost" size="sm" @click="emit('close')">Close</Button>
    </div>

    <form class="space-y-4" @submit="onSubmit">
      <div v-if="blocks.length === 0" class="text-sm text-muted-foreground/60">
        No blocks yet. Add a block to start composing intervals.
      </div>

      <WorkoutBlockCard
        v-for="(block, index) in blocks"
        :key="block.key"
        :block-index="index"
        :sport="sport"
        :set-field="setField"
        @remove="remove(index)"
      />

      <Button type="button" variant="outline" @click="addBlock">Add block</Button>

      <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ globalError }}
      </div>

      <div class="flex items-center justify-end gap-3">
        <span v-if="justSaved" class="flex items-center gap-1 text-sm text-muted-foreground">
          <CheckCircle2 :size="16" class="text-primary" />
          Saved
        </span>
        <Button type="submit" :disabled="isSubmitting">Save workout</Button>
      </div>
    </form>
  </section>
</template>
