<script setup lang="ts">
import { computed } from 'vue'

// The tap-grid extracted from RpeSelector (ADR-0011 §4). RPE is 1-10 with Easy/Steady/Max; soreness
// (1-10) and sleep quality (1-5) use this component directly with their own labels.
const props = withDefaults(
  defineProps<{
    modelValue: number | null
    /** 5 or 10 today. Any other value renders that many buttons but falls back to the
     *  grid-cols-10 class — see gridClass. Deliberately not a union type: RpeSelector binds a
     *  plain :max="10" and a union would break that binding. */
    max?: number
    /** left / centre / right. null renders no label row. */
    labels?: [string, string, string] | null
  }>(),
  { max: 10, labels: null },
)

const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

const values = computed(() => Array.from({ length: props.max }, (_, i) => i + 1))

// Tailwind's scanner only generates classes it can see as LITERAL strings. `grid-cols-${max}` would
// compile to nothing and silently render a single column - so both variants are written out.
const gridClass = computed(() =>
  props.max === 5 ? 'grid grid-cols-5 gap-1' : 'grid grid-cols-10 gap-1',
)
</script>

<template>
  <div>
    <div :class="gridClass">
      <button
        v-for="v in values"
        :key="v"
        type="button"
        class="rounded-md border py-2.5 font-mono text-[13px] font-semibold transition-all duration-[120ms]"
        :class="
          modelValue === v
            ? 'border-primary bg-gradient-to-b from-primary to-primary-lo text-primary-foreground shadow-[0_4px_14px_var(--bryk-accent-glow)]'
            : 'border-border-strong bg-[#0d1015] text-subtle hover:border-[#3a4252] hover:text-foreground'
        "
        :aria-pressed="modelValue === v"
        @click="emit('update:modelValue', v)"
      >
        {{ v }}
      </button>
    </div>
    <div
      v-if="labels"
      class="mt-1.5 flex justify-between font-mono text-[9.5px] uppercase tracking-[0.1em] text-faint"
    >
      <span>{{ labels[0] }}</span>
      <span>{{ labels[1] }}</span>
      <span>{{ labels[2] }}</span>
    </div>
  </div>
</template>
