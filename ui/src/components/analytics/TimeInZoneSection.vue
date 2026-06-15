<script setup lang="ts">
import { computed, watch } from 'vue'
import { storeToRefs } from 'pinia'
import ChartRangeToggle from '@/components/charts/ChartRangeToggle.vue'
import { useAnalyticsStore } from '@/stores/analytics'
import { isoDate } from '@/services/analytics'
import { formatHm } from '@/lib/format'

const props = defineProps<{ modelValue: string }>()
defineEmits<{ 'update:modelValue': [value: string] }>()

const store = useAnalyticsStore()
const { timeInZone, timeInZoneLoading } = storeToRefs(store)

const SPORT_OPTIONS = [
  { value: '', label: 'All' },
  { value: 'Bike', label: 'Bike' },
  { value: 'Run', label: 'Run' },
  { value: 'Swim', label: 'Swim' },
]

// Fixed trailing 90-day window (the time-in-zone span; the PMC/Load toggles own their own ranges).
function last90(): { from: string; to: string } {
  const to = new Date()
  const from = new Date()
  from.setDate(from.getDate() - 90)
  return { from: isoDate(from), to: isoDate(to) }
}

watch(
  () => props.modelValue,
  (sport) => {
    const { from, to } = last90()
    void store.loadTimeInZone(from, to, sport)
  },
  { immediate: true },
)

const total = computed(() => timeInZone.value?.totalSeconds ?? 0)

// Stacked segments: zones 1..5 (positive) coloured by the ZonesView convention + an unclassified remainder.
const segments = computed(() => {
  const tiz = timeInZone.value
  if (!tiz || total.value === 0) return []
  const zoneSegs = tiz.zones
    .filter((z) => z.seconds > 0)
    .map((z) => ({
      key: `z${z.zoneNumber}`,
      label: `Z${z.zoneNumber}`,
      seconds: z.seconds,
      color: `var(--chart-${Math.min(z.zoneNumber, 5)})`,
    }))
  const unclassified = tiz.methodBreakdown.unclassifiedSeconds
  return unclassified > 0
    ? [...zoneSegs, { key: 'unc', label: 'Unclassified', seconds: unclassified, color: 'var(--bryk-fg-3)' }]
    : zoneSegs
})

const pct = (seconds: number) => `${(seconds / total.value) * 100}%`
</script>

<template>
  <section class="card-surface flex flex-col gap-4 p-5">
    <header class="flex flex-wrap items-center gap-3">
      <div class="flex flex-col">
        <div class="flex items-center gap-2">
          <h2 class="text-[15px] font-semibold tracking-[-0.02em] text-foreground">Time in Zone</h2>
          <span
            class="rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-warn"
          >
            estimated
          </span>
        </div>
        <span class="eyebrow text-faint">Last 90 days · intensity distribution</span>
      </div>
      <div class="ml-auto">
        <ChartRangeToggle
          :model-value="modelValue"
          :options="SPORT_OPTIONS"
          aria-label="Sport"
          @update:model-value="(v) => $emit('update:modelValue', v)"
        />
      </div>
    </header>

    <p v-if="timeInZoneLoading && !timeInZone" class="py-8 text-center text-sm text-muted-foreground">
      Loading…
    </p>

    <template v-else-if="total > 0">
      <!-- stacked bar -->
      <div class="flex h-5 w-full overflow-hidden rounded-md">
        <div
          v-for="seg in segments"
          :key="seg.key"
          class="h-full"
          :style="{ width: pct(seg.seconds), background: seg.color }"
          :title="`${seg.label} · ${formatHm(seg.seconds)}`"
        />
      </div>

      <!-- per-zone legend -->
      <div class="flex flex-wrap gap-x-4 gap-y-1 font-mono text-[11px] text-muted-foreground">
        <span v-for="seg in segments" :key="seg.key" class="inline-flex items-center gap-1.5">
          <i class="size-2 rounded-full" :style="{ background: seg.color }" />
          {{ seg.label }} · {{ formatHm(seg.seconds) }}
        </span>
      </div>

      <!-- honest provenance behind the "estimated" badge -->
      <p class="font-mono text-[11px] text-faint">
        Estimated from
        <span v-if="timeInZone!.methodBreakdown.structureSeconds > 0">
          planned structure ({{ formatHm(timeInZone!.methodBreakdown.structureSeconds) }})</span
        ><span v-if="timeInZone!.methodBreakdown.sessionAvgSeconds > 0">
          · session HR ({{ formatHm(timeInZone!.methodBreakdown.sessionAvgSeconds) }})</span
        ><span v-if="timeInZone!.methodBreakdown.unclassifiedSeconds > 0">
          · unclassified ({{ formatHm(timeInZone!.methodBreakdown.unclassifiedSeconds) }})</span
        >. Real sample data lands with file import.
      </p>
    </template>

    <p v-else class="py-8 text-center text-sm text-muted-foreground">
      — &nbsp;No classifiable training in this window yet.
    </p>
  </section>
</template>
