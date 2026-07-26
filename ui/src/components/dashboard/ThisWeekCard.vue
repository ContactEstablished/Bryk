<script setup lang="ts">
import { computed, onMounted } from 'vue'
import TypePill from '@/components/common/TypePill.vue'
import DeltaChip from '@/components/common/DeltaChip.vue'
import { sportToPillKind } from '@/components/common/pills'
import { useTrainingStore } from '@/stores/training'
import { buildTargetProgress } from '@/lib/weeklyTarget'

const store = useTrainingStore()

onMounted(() => {
  if (!store.thisWeek) void store.loadThisWeek()
})

// Today as YYYY-MM-DD in UTC, matching the server's DateOnly semantics
// (mirrors stores/profile.ts's helper).
function utcTodayIso(): string {
  const now = new Date()
  const yyyy = now.getUTCFullYear()
  const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(now.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}
const today = utcTodayIso()

// Parse the YYYY-MM-DD string as a UTC calendar date so the label is timezone-stable
// and matches the server's DateOnly (mirrors PrimaryGoalCard).
function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  })
}

function dayNumber(iso: string): number {
  return Number(iso.split('-')[2])
}

function weekdayShort(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    weekday: 'short',
    timeZone: 'UTC',
  })
}

const weekRange = computed(() => {
  const tw = store.thisWeek
  if (!tw) return ''
  return `${formatDay(tw.weekStart)} – ${formatDay(tw.weekEnd)}`
})

const sessions = computed(() => store.thisWeek?.plannedWorkouts ?? [])

// Null when the athlete has no active plan or the plan has no usable baseline (ADR-0009 §1) — the
// card then renders exactly as it did before Phase 18: no bar, no placeholder, no dash row.
const targetProgress = computed(() => {
  const tw = store.thisWeek
  if (tw?.targetLoad == null) return null
  return buildTargetProgress(tw.actualLoad ?? 0, tw.targetLoad)
})

const barClass: Record<'good' | 'warn' | 'bad', string> = {
  good: 'bg-good',
  warn: 'bg-warn',
  bad: 'bg-bad',
}
</script>

<template>
  <div class="card-surface">
    <div class="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-border px-6 py-5">
      <h3 class="text-sm font-semibold">This Week</h3>
      <span v-if="store.thisWeek" class="eyebrow">
        {{ sessions.length }} sessions · {{ store.thisWeek.weeklyLoad ?? 0 }} TSS planned
      </span>
      <span v-if="store.thisWeek" class="ml-auto font-mono text-[11px] text-muted-foreground">
        {{ weekRange }}
      </span>
    </div>

    <div class="p-6">
      <!-- Target vs actual (Phase 18). Absent entirely when there is no target. -->
      <div v-if="targetProgress" class="mb-4 flex flex-col gap-1.5">
        <div class="flex items-center justify-between gap-3">
          <span class="font-mono text-[11px] text-muted-foreground">
            {{ store.thisWeek!.actualLoad ?? 0 }} / {{ store.thisWeek!.targetLoad }} TSS
          </span>
          <DeltaChip :dir="targetProgress.dir">{{ targetProgress.deltaLabel }}</DeltaChip>
        </div>
        <div
          class="h-1.5 overflow-hidden rounded-full bg-muted"
          role="progressbar"
          :aria-valuenow="targetProgress.widthPct"
          aria-valuemin="0"
          aria-valuemax="100"
          :aria-label="`Weekly load: ${store.thisWeek!.actualLoad ?? 0} of ${store.thisWeek!.targetLoad} TSS`"
        >
          <div
            class="h-full rounded-full transition-[width] duration-[200ms]"
            :class="barClass[targetProgress.state]"
            :style="{ width: targetProgress.widthPct + '%' }"
          />
        </div>
      </div>

      <!-- Loading (this week not yet fetched) -->
      <p v-if="!store.thisWeek" class="text-sm text-muted-foreground">Loading…</p>

      <!-- Populated -->
      <ul v-else-if="sessions.length > 0" class="flex flex-col gap-2.5">
        <li
          v-for="session in sessions"
          :key="session.id"
          class="grid grid-cols-[56px_1fr_auto] items-center gap-3 rounded-[10px] border bg-[#0e1218] p-3 transition-colors duration-[120ms]"
          :class="
            session.scheduledDate === today
              ? 'border-primary shadow-[inset_0_0_0_1px_var(--bryk-accent),0_8px_24px_var(--bryk-accent-glow)]'
              : 'border-border hover:border-border-strong'
          "
        >
          <div class="border-r border-border pr-3 text-center">
            <div class="font-mono text-lg font-bold leading-tight">
              {{ dayNumber(session.scheduledDate) }}
            </div>
            <div
              class="font-mono text-[10px] uppercase tracking-[0.1em]"
              :class="session.scheduledDate === today ? 'text-primary-hi' : 'text-muted-foreground'"
            >
              {{ session.scheduledDate === today ? 'Today' : weekdayShort(session.scheduledDate) }}
            </div>
          </div>
          <div class="min-w-0">
            <p class="truncate text-sm font-semibold">{{ session.title }}</p>
            <p class="mt-0.5 font-mono text-[11px] text-muted-foreground">
              <span v-if="session.plannedDurationMinutes != null">{{ session.plannedDurationMinutes }} min</span>
              <span v-if="session.plannedDurationMinutes != null && session.effectiveLoad != null"> · </span>
              <span v-if="session.effectiveLoad != null">{{ session.effectiveLoad }} TSS</span>
            </p>
          </div>
          <TypePill :kind="sportToPillKind(session.sport)">{{ session.sport }}</TypePill>
        </li>
      </ul>

      <!-- Empty (loaded, no sessions this week) -->
      <p v-else class="text-sm text-muted-foreground">
        No sessions planned this week.
        <!-- TODO(9-6+): link to /training once the plan-authoring UI ships -->
      </p>
    </div>
  </div>
</template>
