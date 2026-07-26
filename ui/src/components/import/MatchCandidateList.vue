<script setup lang="ts">
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import type { MatchCandidate } from '@/types/activityFiles'

defineProps<{ candidates: MatchCandidate[]; modelValue: string | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: string | null] }>()

function dayLabel(offset: number): string {
  if (offset === 0) return 'Same day'
  return offset < 0 ? `−${Math.abs(offset)} day` : `+${offset} day`
}

// Parse the YYYY-MM-DD string as a UTC calendar date so the label is timezone-stable.
function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  })
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <span class="eyebrow block">Matches a planned session?</span>

    <p v-if="candidates.length === 0" class="font-mono text-[11px] text-faint">
      No planned session within a day of this file.
    </p>

    <label
      v-for="c in candidates"
      :key="c.plannedWorkoutId"
      class="flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2 transition-colors duration-[120ms]"
      :class="
        modelValue === c.plannedWorkoutId
          ? 'border-primary bg-primary-glow'
          : 'border-border-strong hover:border-[#3a4252]'
      "
    >
      <input
        type="radio"
        name="match-candidate"
        class="accent-primary"
        :value="c.plannedWorkoutId"
        :checked="modelValue === c.plannedWorkoutId"
        @change="emit('update:modelValue', c.plannedWorkoutId)"
      />
      <TypePill :kind="sportToPillKind(c.sport)">{{ c.sport }}</TypePill>
      <span class="min-w-0 flex-1 truncate text-sm text-foreground">{{ c.title }}</span>
      <span v-if="c.plannedLoad != null" class="font-mono text-[11px] text-primary-hi">
        {{ c.plannedLoad }} TSS
      </span>
      <span class="font-mono text-[11px] text-muted-foreground">{{ formatDay(c.scheduledDate) }}</span>
      <span
        class="rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-muted-foreground"
      >
        {{ dayLabel(c.dayOffset) }}
      </span>
    </label>

    <!-- An unplanned import is first-class and must always be one click away. -->
    <label
      class="flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2 transition-colors duration-[120ms]"
      :class="modelValue === null ? 'border-primary bg-primary-glow' : 'border-border-strong hover:border-[#3a4252]'"
    >
      <input
        type="radio"
        name="match-candidate"
        class="accent-primary"
        :checked="modelValue === null"
        @change="emit('update:modelValue', null)"
      />
      <span class="text-sm text-muted-foreground">No planned workout</span>
    </label>
  </div>
</template>
