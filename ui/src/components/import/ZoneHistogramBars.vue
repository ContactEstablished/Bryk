<script setup lang="ts">
import { computed } from 'vue'
import { formatHm } from '@/lib/format'
import type { ZoneHistogramEntry } from '@/types/activityFiles'

// Accepted duplication, on purpose: this mirrors TimeInZoneSection.vue's stacked bar + legend markup.
// Extracting a shared component would mean editing that file, which Task 19-6 owns in the same phase.
// The duplication is recorded as tech debt in the phase handoff.
const props = defineProps<{ zones: ZoneHistogramEntry[] }>()

const total = computed(() => props.zones.reduce((sum, z) => sum + z.seconds, 0))

const segments = computed(() =>
  props.zones
    .filter((z) => z.seconds > 0)
    .map((z) => ({
      key: `z${z.zoneNumber}`,
      label: `Z${z.zoneNumber}`,
      seconds: z.seconds,
      color: `var(--chart-${Math.min(z.zoneNumber, 5)})`,
    })),
)

const pct = (seconds: number) => `${(seconds / total.value) * 100}%`
</script>

<template>
  <div v-if="total > 0" class="flex flex-col gap-2">
    <div class="flex h-5 w-full overflow-hidden rounded-md">
      <div
        v-for="seg in segments"
        :key="seg.key"
        class="h-full"
        :style="{ width: pct(seg.seconds), background: seg.color }"
        :title="`${seg.label} · ${formatHm(seg.seconds)}`"
      />
    </div>

    <div class="flex flex-wrap gap-x-4 gap-y-1 font-mono text-[11px] text-muted-foreground">
      <span v-for="seg in segments" :key="seg.key" class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" :style="{ background: seg.color }" />
        {{ seg.label }} · {{ formatHm(seg.seconds) }}
      </span>
    </div>
  </div>
</template>
