<script setup lang="ts">
import { computed } from 'vue'
import { useFieldArray } from 'vee-validate'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import WorkoutStepRow from './WorkoutStepRow.vue'
import type { PlannedSport, WorkoutStepDto } from '@/types/training'

const props = defineProps<{
  blockIndex: number
  sport: PlannedSport
  setField: (path: string, value: unknown) => void
}>()
const emit = defineEmits<{ remove: [] }>()

// Reactive path so the steps array stays bound to this block even if earlier blocks are removed.
const { fields: steps, push, remove } = useFieldArray<WorkoutStepDto>(
  computed(() => `blocks[${props.blockIndex}].steps`),
)

function addStep() {
  push({
    intent: 'Work',
    title: null,
    durationSeconds: null,
    distanceMeters: null,
    targetZone: null,
    targetPowerLow: null,
    targetPowerHigh: null,
    targetHrLow: null,
    targetHrHigh: null,
    targetPaceLow: null,
    targetPaceHigh: null,
    sets: null,
    reps: null,
    loadKg: null,
    rpe: null,
  })
}
</script>

<template>
  <div class="rounded-md border p-4 space-y-4">
    <div class="flex items-center justify-between gap-4">
      <span class="text-sm font-medium">Block {{ blockIndex + 1 }}</span>
      <Button type="button" variant="ghost" size="sm" @click="emit('remove')">Remove block</Button>
    </div>

    <FormField v-slot="{ componentField }" :name="`blocks[${blockIndex}].repeats`">
      <FormItem class="w-32">
        <FormLabel class="text-xs">Repeats (×)</FormLabel>
        <FormControl><Input type="number" min="1" v-bind="componentField" /></FormControl>
        <FormMessage />
      </FormItem>
    </FormField>

    <div v-if="steps.length === 0" class="text-xs text-muted-foreground/60">No steps yet.</div>

    <WorkoutStepRow
      v-for="(step, index) in steps"
      :key="step.key"
      :block-index="blockIndex"
      :step-index="index"
      :sport="sport"
      :set-field="setField"
      @remove="remove(index)"
    />

    <Button type="button" variant="outline" size="sm" @click="addStep">Add step</Button>
  </div>
</template>
