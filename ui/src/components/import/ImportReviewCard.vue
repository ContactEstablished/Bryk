<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import { Button } from '@/components/ui/button'
import MetricTile from '@/components/common/MetricTile.vue'
import ZoneHistogramBars from './ZoneHistogramBars.vue'
import MatchCandidateList from './MatchCandidateList.vue'
import { useActivityFilesStore } from '@/stores/activityFiles'

const emit = defineEmits<{ committed: [workoutId: string]; cancelled: [] }>()

const store = useActivityFilesStore()
const { preview, committing, commitError, selectedPlannedWorkoutId } = storeToRefs(store)

// Copied from WorkoutsView rather than extracted: the task fence keeps that view's local helpers as
// they are, and a shared module is a separate change.
function formatDuration(totalSeconds: number | null): string | null {
  if (totalSeconds == null) return null
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = Math.floor(totalSeconds % 60)
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`
}

function formatDistance(meters: number | null): string | null {
  return meters == null ? null : `${(meters / 1000).toFixed(1)} km`
}

const sizeLabel = computed(() =>
  preview.value ? `${(preview.value.byteSize / 1024).toFixed(0)} KB` : '',
)

async function onConfirm() {
  const workoutId = await store.commit()
  if (workoutId) emit('committed', workoutId)
}

async function onDiscard() {
  await store.discard()
  emit('cancelled')
}
</script>

<template>
  <div v-if="preview" class="card-surface flex flex-col gap-4 p-5">
    <header class="flex flex-wrap items-center gap-2">
      <h2 class="text-[15px] font-semibold tracking-[-0.02em] text-foreground">Review import</h2>
      <span
        class="rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-muted-foreground"
      >
        {{ preview.format }}
      </span>
      <span class="font-mono text-[11px] text-muted-foreground">
        {{ preview.fileName }} · {{ sizeLabel }}
      </span>
    </header>

    <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
      <MetricTile label="Load" :value="preview.computedLoad" unit="TSS" />
      <MetricTile label="Duration" :value="formatDuration(preview.parsed.durationSeconds)" />
      <MetricTile label="Distance" :value="formatDistance(preview.parsed.distanceMeters)" />
      <MetricTile label="Avg HR" :value="preview.parsed.avgHr" unit="bpm" />
    </div>

    <ZoneHistogramBars :zones="preview.zoneSeconds" />

    <MatchCandidateList
      :candidates="preview.matchCandidates"
      :model-value="selectedPlannedWorkoutId"
      @update:model-value="(v) => (selectedPlannedWorkoutId = v)"
    />

    <p v-if="commitError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
      {{ commitError }}
    </p>

    <div class="flex items-center gap-2">
      <Button type="button" variant="outline" size="sm" :disabled="committing" @click="onConfirm">
        {{ committing ? 'Saving…' : 'Confirm import' }}
      </Button>
      <Button type="button" variant="ghost" size="sm" :disabled="committing" @click="onDiscard">
        Discard
      </Button>
    </div>
  </div>
</template>
