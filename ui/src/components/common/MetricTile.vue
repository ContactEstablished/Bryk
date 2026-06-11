<script setup lang="ts">
import { computed } from 'vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import Sparkline from '@/components/common/Sparkline.vue'
import { useCountUp } from '@/composables/useCountUp'

const props = withDefaults(
  defineProps<{
    label: string
    value?: string | number | null
    unit?: string
    delta?: { text: string; dir: 'up' | 'down' | 'flat' } | null
    spark?: number[] | null
    sparkAccent?: boolean
    loading?: boolean
    placeholder?: boolean
  }>(),
  {
    value: null,
    unit: undefined,
    delta: null,
    spark: null,
    sparkAccent: true,
    loading: false,
    placeholder: false,
  },
)

const numericValue = computed(() => (typeof props.value === 'number' ? props.value : null))
const animated = useCountUp(numericValue)
const displayValue = computed(() => {
  if (props.value == null) return '—'
  return typeof props.value === 'number' ? animated.value : props.value
})
</script>

<template>
  <div
    class="card-surface flex min-h-[132px] flex-col gap-3 p-5 pb-4"
    :class="placeholder ? 'border-dashed' : ''"
  >
    <div class="flex items-center gap-2">
      <h3 class="eyebrow">{{ label }}</h3>
      <span
        v-if="placeholder"
        class="ml-auto rounded border border-border px-1 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-faint"
      >
        soon
      </span>
    </div>

    <p v-if="loading" class="text-sm text-muted-foreground">Loading…</p>

    <template v-else>
      <div class="flex items-end gap-3">
        <div
          class="text-[38px] font-bold leading-none tracking-[-0.04em] tabular-nums"
          :class="placeholder || value == null ? 'text-faint/40' : ''"
        >
          <template v-if="placeholder">—</template>
          <template v-else>{{ displayValue }}</template
          ><span
            v-if="unit && !placeholder && value != null"
            class="ml-1 text-sm font-medium tracking-normal text-muted-foreground"
            >{{ unit }}</span
          >
        </div>
        <DeltaChip v-if="delta && !placeholder" :dir="delta.dir" class="mb-0.5 ml-auto">
          {{ delta.text }}
        </DeltaChip>
      </div>

      <slot name="footer" />

      <div v-if="spark && spark.length >= 2 && !placeholder" class="mt-auto">
        <Sparkline :data="spark" :accent="sparkAccent" />
      </div>
    </template>
  </div>
</template>
