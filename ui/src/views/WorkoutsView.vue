<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  Activity,
  Bike,
  Dumbbell,
  Footprints,
  Heart,
  Medal,
  Waves,
  type LucideIcon,
} from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import ImportReviewCard from '@/components/import/ImportReviewCard.vue'
import { useTrainingStore } from '@/stores/training'
import { useActivityFilesStore } from '@/stores/activityFiles'
import { ACCEPTED_EXTENSIONS, MAX_UPLOAD_BYTES } from '@/services/activityFiles'
import type { PlannedSport } from '@/types/training'

const store = useTrainingStore()
const importStore = useActivityFilesStore()
const router = useRouter()

// Local filter state; applying a change reloads page 1 through the store.
const sport = ref<PlannedSport | null>(null)
const from = ref<string>('')
const to = ref<string>('')

const sportOptions: { value: PlannedSport | null; label: string; icon: LucideIcon }[] = [
  { value: null, label: 'All', icon: Activity },
  { value: 'Swim', label: 'Swim', icon: Waves },
  { value: 'Bike', label: 'Bike', icon: Bike },
  { value: 'Run', label: 'Run', icon: Footprints },
  { value: 'Triathlon', label: 'Triathlon', icon: Medal },
  { value: 'Strength', label: 'Strength', icon: Dumbbell },
]

function apply() {
  void store.loadWorkouts({
    sport: sport.value,
    from: from.value || null,
    to: to.value || null,
  })
}

function selectSport(value: PlannedSport | null) {
  sport.value = value
  apply()
}

function clearFilters() {
  sport.value = null
  from.value = ''
  to.value = ''
  apply()
}

onMounted(apply)

const sportIcons: Record<string, LucideIcon> = {
  Run: Footprints,
  Bike: Bike,
  Swim: Waves,
  Strength: Dumbbell,
  Triathlon: Medal,
}

function sportIcon(s: string): LucideIcon {
  return sportIcons[s] ?? Activity
}

// Parse the YYYY-MM-DD string as a UTC calendar date so the label is timezone-stable.
function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
}

function formatDuration(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = Math.floor(totalSeconds % 60)
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`
}

function formatDistance(meters: number): string {
  return `${(meters / 1000).toFixed(1)} km`
}

// --- activity-file import (Task 19-5) ---

const fileInput = ref<HTMLInputElement | null>(null)
const dragging = ref(false)

// Client-side pre-checks only avoid a wasted upload; the server's validator is the authority.
function handleFile(file: File | null | undefined) {
  if (!file) return

  const dot = file.name.lastIndexOf('.')
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : ''
  if (!ACCEPTED_EXTENSIONS.split(',').includes(extension)) {
    importStore.uploadError = 'Only .fit, .tcx and .gpx files are supported.'
    return
  }
  if (file.size > MAX_UPLOAD_BYTES) {
    importStore.uploadError = 'That file is larger than the 25 MB limit.'
    return
  }

  void importStore.upload(file)
}

function onFilePicked(event: Event) {
  const input = event.target as HTMLInputElement
  handleFile(input.files?.[0])
  input.value = '' // let the same file be re-picked after a discard
}

function onDrop(event: DragEvent) {
  dragging.value = false
  handleFile(event.dataTransfer?.files?.[0])
}

function onCommitted(workoutId: string) {
  void router.push(`/workouts/${workoutId}`)
}

function onCancelled() {
  importStore.reset()
  apply()
}
</script>

<template>
  <AppShell title="Workouts" subtitle="Your completed training history.">
    <div class="mx-auto w-full max-w-3xl space-y-5">
      <!-- Import: drop zone, replaced by the review card once a file has been parsed -->
      <ImportReviewCard
        v-if="importStore.preview"
        @committed="onCommitted"
        @cancelled="onCancelled"
      />

      <div
        v-else
        class="card-surface flex flex-col items-center gap-2 border-dashed p-5 transition-colors duration-[120ms]"
        :class="dragging ? 'border-primary bg-primary-glow' : ''"
        @dragover.prevent="dragging = true"
        @dragleave.prevent="dragging = false"
        @drop.prevent="onDrop"
      >
        <p class="text-sm text-muted-foreground">
          Drop a <span class="font-mono text-[11px]">.fit</span>,
          <span class="font-mono text-[11px]">.tcx</span> or
          <span class="font-mono text-[11px]">.gpx</span> file here
        </p>

        <p v-if="importStore.uploading" class="text-sm text-muted-foreground">Parsing…</p>
        <Button v-else type="button" variant="outline" size="sm" @click="fileInput?.click()">
          Import file
        </Button>

        <input
          ref="fileInput"
          type="file"
          class="sr-only"
          :accept="ACCEPTED_EXTENSIONS"
          @change="onFilePicked"
        />

        <p
          v-if="importStore.uploadError"
          class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          {{ importStore.uploadError }}
        </p>
      </div>

      <!-- Filter bar -->
      <div class="card-surface flex flex-wrap items-end gap-4 p-4">
        <div class="space-y-2">
          <span class="eyebrow block">Sport</span>
          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="opt in sportOptions"
              :key="opt.label"
              type="button"
              class="inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-[13px] font-medium transition-all duration-[120ms]"
              :class="
                sport === opt.value
                  ? 'border-primary bg-primary-glow text-primary-hi'
                  : 'border-border-strong bg-[#0d1015] text-subtle hover:border-[#3a4252] hover:text-foreground'
              "
              :aria-pressed="sport === opt.value"
              @click="selectSport(opt.value)"
            >
              <component :is="opt.icon" :size="14" />
              {{ opt.label }}
            </button>
          </div>
        </div>

        <div class="space-y-2">
          <span class="eyebrow block">From</span>
          <Input type="date" v-model="from" class="w-40" @change="apply" />
        </div>
        <div class="space-y-2">
          <span class="eyebrow block">To</span>
          <Input type="date" v-model="to" class="w-40" @change="apply" />
        </div>

        <Button
          v-if="sport !== null || from || to"
          type="button"
          variant="ghost"
          size="sm"
          class="ml-auto"
          @click="clearFilters"
        >
          Clear
        </Button>
      </div>

      <!-- History list -->
      <div class="card-surface">
        <p v-if="!store.workouts" class="px-6 py-4 text-sm text-muted-foreground">Loading…</p>

        <template v-else>
          <ul v-if="store.workouts.length > 0">
            <li
              v-for="(w, i) in store.workouts"
              :key="w.id"
              :class="i > 0 ? 'border-t border-border' : ''"
            >
              <RouterLink
                :to="`/workouts/${w.id}`"
                class="grid grid-cols-[32px_1fr_auto] items-center gap-4 px-6 py-3.5 transition-colors duration-[120ms] hover:bg-muted"
              >
                <div
                  class="flex size-8 items-center justify-center rounded-lg border border-border-strong bg-raised text-subtle"
                >
                  <component :is="sportIcon(w.sport)" :size="16" />
                </div>
                <div class="min-w-0">
                  <div class="flex items-center gap-2">
                    <TypePill :kind="sportToPillKind(w.sport)">{{ w.sport }}</TypePill>
                  </div>
                  <p class="mt-1 flex flex-wrap gap-x-3 font-mono text-[11px] text-muted-foreground">
                    <span v-if="w.actualDurationSeconds != null">{{ formatDuration(w.actualDurationSeconds) }}</span>
                    <span v-if="w.actualDistanceMeters != null">{{ formatDistance(w.actualDistanceMeters) }}</span>
                    <span v-if="w.avgHr != null" class="inline-flex items-center gap-1">
                      <Heart :size="10" />{{ w.avgHr }}
                    </span>
                    <span v-if="w.effectiveLoad != null" class="text-primary-hi">{{ w.effectiveLoad }} TSS</span>
                    <span v-if="w.rpe != null">RPE {{ w.rpe }}</span>
                  </p>
                </div>
                <span class="font-mono text-[11px] text-muted-foreground">{{ formatDay(w.completedDate) }}</span>
              </RouterLink>
            </li>
          </ul>

          <p v-else class="px-6 py-8 text-center text-sm text-muted-foreground">
            No workouts match these filters.
          </p>

          <div
            v-if="store.workoutsHasMore"
            class="flex justify-center border-t border-border px-6 py-4"
          >
            <Button
              type="button"
              variant="outline"
              size="sm"
              :disabled="store.loadingWorkouts"
              @click="store.loadMoreWorkouts()"
            >
              Load more
            </Button>
          </div>
        </template>
      </div>
    </div>
  </AppShell>
</template>
