<script setup lang="ts">
import { complianceColor } from '@/lib/calendar'
import type { ComplianceBucket } from '@/types/calendar'

defineProps<{
  compact?: boolean
}>()

interface LegendEntry {
  bucket: ComplianceBucket | null
  label: string
}

const entries: LegendEntry[] = [
  { bucket: 'Green', label: 'On target' },
  { bucket: 'Yellow', label: 'Under/over' },
  { bucket: 'Red', label: 'Missed' },
  { bucket: 'Grey', label: 'Scheduled' },
  { bucket: null, label: 'Unplanned' },
]
</script>

<template>
  <div class="flex items-center gap-3 text-[11px] text-faint">
    <template v-for="entry in entries" :key="entry.label">
      <span v-if="entry.bucket" class="flex items-center gap-1.5">
        <span class="size-2 shrink-0 rounded-full" :class="complianceColor(entry.bucket).dot" />
        <span v-if="!compact">{{ entry.label }}</span>
      </span>
      <!-- Unplanned: no dot, uses the same styling as the chip tag -->
      <span v-else class="flex items-center gap-1.5">
        <span class="shrink-0 rounded-sm border border-border-strong px-1 font-mono text-[9px] uppercase">{{ entry.label }}</span>
      </span>
    </template>
  </div>
</template>