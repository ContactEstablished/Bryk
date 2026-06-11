<script setup lang="ts">
import { cva } from 'class-variance-authority'
import type { PillKind } from '@/components/common/pills'

const props = withDefaults(
  defineProps<{
    kind?: PillKind
  }>(),
  { kind: 'neutral' },
)

// Hues from the design prototype's up-pill variants, keyed by sport.
const pill = cva(
  'inline-flex items-center rounded-full border px-2 py-1 font-mono text-[10px] uppercase tracking-[0.08em]',
  {
    variants: {
      kind: {
        run: 'text-primary-hi border-primary-lo bg-primary-glow',
        bike: 'text-warn border-[oklch(0.6_0.12_75)] bg-[oklch(0.5_0.12_75_/_0.15)]',
        swim: 'text-[oklch(0.78_0.16_195)] border-[oklch(0.55_0.12_195)] bg-[oklch(0.5_0.12_195_/_0.15)]',
        strength: 'text-[oklch(0.78_0.16_320)] border-[oklch(0.55_0.12_320)] bg-[oklch(0.5_0.12_320_/_0.15)]',
        triathlon: 'text-good border-[oklch(0.55_0.1_155)] bg-[oklch(0.5_0.1_155_/_0.15)]',
        neutral: 'text-subtle border-border-strong bg-muted',
      },
    },
  },
)
</script>

<template>
  <span :class="pill({ kind: props.kind })"><slot /></span>
</template>
