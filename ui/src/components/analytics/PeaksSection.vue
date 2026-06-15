<script setup lang="ts">
import { computed, watch } from 'vue'
import { storeToRefs } from 'pinia'
import MetricTile from '@/components/common/MetricTile.vue'
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import { useAnalyticsStore } from '@/stores/analytics'
import { formatDuration, formatDistance, formatPace, formatDay } from '@/lib/format'
import type { PeakKind, PeakRecord } from '@/types/analytics'

const props = defineProps<{ sport: string }>()

const store = useAnalyticsStore()
const { peaks, peaksLoading } = storeToRefs(store)

watch(() => props.sport, (sport) => void store.loadPeaks(sport), { immediate: true })

const KIND_ORDER: PeakKind[] = ['Load', 'Duration', 'Distance', 'Pace', 'Power']
const KIND_LABEL: Record<PeakKind, string> = {
  Load: 'Best Load',
  Duration: 'Longest',
  Distance: 'Longest Distance',
  Pace: 'Fastest Pace',
  Power: 'Best Power',
}

const records = computed(() =>
  [...(peaks.value?.records ?? [])].sort((a, b) => KIND_ORDER.indexOf(a.kind) - KIND_ORDER.indexOf(b.kind)),
)

function peakValue(r: PeakRecord): string {
  switch (r.kind) {
    case 'Duration':
      return formatDuration(r.value)
    case 'Distance':
      return formatDistance(r.value)
    case 'Pace':
      return formatPace(r.value)
    default:
      return Math.round(r.value).toString() // Load, Power
  }
}

function peakUnit(r: PeakRecord): string {
  switch (r.kind) {
    case 'Load':
      return 'TSS'
    case 'Power':
      return 'W'
    case 'Pace':
      return r.sport === 'Swim' ? '/100m' : '/km'
    default:
      return ''
  }
}

// A real improvement over the previous best, shown only for records set in the last 90 days.
function peakDelta(r: PeakRecord): { text: string; dir: 'up' } | null {
  if (!r.isRecent || r.previousValue == null) return null
  if (r.kind === 'Pace') {
    const faster = r.previousValue - r.value // positive = faster
    return faster > 0 ? { text: `−${formatPace(faster)}`, dir: 'up' } : null
  }
  const improvement = r.value - r.previousValue
  if (improvement <= 0) return null
  const text =
    r.kind === 'Duration'
      ? `+${formatDuration(improvement)}`
      : r.kind === 'Distance'
        ? `+${formatDistance(improvement)}`
        : `+${Math.round(improvement)}`
  return { text, dir: 'up' }
}
</script>

<template>
  <section class="flex flex-col gap-4">
    <div class="flex flex-col">
      <h2 class="text-[15px] font-semibold tracking-[-0.02em] text-foreground">Personal Records</h2>
      <span class="eyebrow text-faint">Session bests · all time</span>
    </div>

    <p v-if="peaksLoading && !peaks" class="py-8 text-center text-sm text-muted-foreground">Loading…</p>

    <div v-else-if="records.length > 0" class="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <MetricTile
        v-for="r in records"
        :key="`${r.kind}-${r.sport}`"
        :label="KIND_LABEL[r.kind]"
        :value="peakValue(r)"
        :unit="peakUnit(r)"
        :delta="peakDelta(r)"
      >
        <template #footer>
          <div class="mt-auto flex items-center gap-2">
            <TypePill :kind="sportToPillKind(r.sport)">{{ r.sport }}</TypePill>
            <span class="font-mono text-[11px] text-faint">{{ formatDay(r.achievedDate) }}</span>
          </div>
        </template>
      </MetricTile>
    </div>

    <p v-else class="py-8 text-center text-sm text-muted-foreground">
      No records yet — log workouts to set personal bests.
    </p>
  </section>
</template>
