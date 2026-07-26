<script setup lang="ts">
import ScaleSelector from '@/components/common/ScaleSelector.vue'

// A thin wrapper over ScaleSelector (ADR-0011 §4): RPE is 1-10 with Easy/Steady/Max, while soreness
// (1-10) and sleep quality (1-5) use ScaleSelector directly. Props and emits are unchanged from the
// pre-extraction component, so LogWorkoutForm.vue:252 and this component's three specs are untouched.
defineProps<{ modelValue: number | null }>()

const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

const RPE_LABELS: [string, string, string] = ['Easy', 'Steady', 'Max']
</script>

<template>
  <ScaleSelector
    :model-value="modelValue"
    :max="10"
    :labels="RPE_LABELS"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
