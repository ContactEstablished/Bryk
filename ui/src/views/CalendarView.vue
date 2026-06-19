<script setup lang="ts">
import { computed, onMounted, onUnmounted, provide, ref, toRef, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { ChevronLeft, ChevronRight, X } from 'lucide-vue-next'
import { useEventListener } from '@vueuse/core'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import CalendarGrid from '@/components/calendar/CalendarGrid.vue'
import ComplianceLegend from '@/components/calendar/ComplianceLegend.vue'
import DayDetailPopover from '@/components/calendar/DayDetailPopover.vue'
import MonthWeekToggle from '@/components/calendar/MonthWeekToggle.vue'
import WeekStrip from '@/components/calendar/WeekStrip.vue'
import { DRAG_RESCHEDULE_KEY } from '@/composables/injectionKeys'
import { useDragReschedule, type PlanWindow } from '@/composables/useDragReschedule'
import { isoDate } from '@/lib/calendar'
import { useCalendarStore } from '@/stores/calendar'
import { getPlans } from '@/services/training'
import type { CalendarDayCell } from '@/lib/calendar'
import type { CalendarDayDto } from '@/types/calendar'

const route = useRoute()
const router = useRouter()
const store = useCalendarStore()
const { feed, loading, error } = storeToRefs(store)

type ViewMode = 'month' | 'week'
const viewMode = ref<ViewMode>('month')

// Anchor date — in month view this is frozen to the 1st; in week view it's the Monday.
const anchorDate = ref<Date>(new Date(new Date().getFullYear(), new Date().getMonth(), 1))

const days = computed<CalendarDayDto[]>(() => feed.value?.days ?? [])

// Computed helpers
const anchorMonth = computed(() => ({
  year: anchorDate.value.getFullYear(),
  month: anchorDate.value.getMonth() + 1,
}))

// Week start: the Monday of the anchor date's week (ISO Monday).
const weekStart = computed(() => {
  const d = new Date(anchorDate.value)
  const day = d.getDay()
  const diff = day === 0 ? -6 : 1 - day // Sunday → -6, else go back to Monday
  d.setDate(d.getDate() + diff)
  return isoDate(d)
})

const periodLabel = computed(() => {
  if (viewMode.value === 'month') {
    return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(anchorDate.value)
  }
  // Week: "Jun 15–21, 2026"
  const start = new Date(weekStart.value + 'T00:00:00')
  const end = new Date(start)
  end.setDate(end.getDate() + 6)
  const fmt = (d: Date) => new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' }).format(d)
  return `${fmt(start)}–${fmt(end)}, ${end.getFullYear()}`
})

// ── Plan windows ──
const planWindows = ref<Record<string, PlanWindow>>({})

async function loadPlanWindows() {
  try {
    const plans = await getPlans()
    const map: Record<string, PlanWindow> = {}
    for (const plan of plans) {
      map[plan.id] = { start: plan.startDate, end: plan.endDate }
    }
    planWindows.value = map
  } catch {
    planWindows.value = {}
  }
}

// ── Drag composable (owned by CalendarView, shared by both grids + popover) ──
const drag = useDragReschedule({
  planWindows: toRef(planWindows),
  onRescheduled: () => store.loadFeed(),
})

provide(DRAG_RESCHEDULE_KEY, drag)

// Global pointer listeners
function handlePointerMove(e: PointerEvent) {
  drag.onPointerMove(e)
}

function handlePointerUp(e: PointerEvent) {
  void drag.onPointerUp(e)
}

onMounted(() => {
  window.addEventListener('pointermove', handlePointerMove)
  window.addEventListener('pointerup', handlePointerUp)
})

onUnmounted(() => {
  window.removeEventListener('pointermove', handlePointerMove)
  window.removeEventListener('pointerup', handlePointerUp)
})

// Esc-to-cancel
useEventListener('keydown', (e) => {
  if (e.key === 'Escape') {
    if (drag.isDragging.value) {
      drag.cancelDrag()
    } else if (activeCell.value) {
      activeCell.value = null
      activeCellRect.value = null
    }
  }
})

// Auto-clear error after 5s
let errorTimer: ReturnType<typeof setTimeout> | undefined
watch(drag.error, (val) => {
  if (errorTimer !== undefined) {
    clearTimeout(errorTimer)
    errorTimer = undefined
  }
  if (val != null) {
    errorTimer = setTimeout(() => {
      drag.error.value = null
    }, 5000)
  }
})

// ── Feed window ──
function feedWindow(d: Date): { from: string; to: string } {
  if (viewMode.value === 'month') {
    const year = d.getFullYear()
    const month = d.getMonth()
    return {
      from: isoDate(new Date(year, month, 1)),
      to: isoDate(new Date(year, month + 1, 0)),
    }
  }
  // Week
  const start = new Date(weekStart.value + 'T00:00:00')
  const end = new Date(start)
  end.setDate(end.getDate() + 6)
  return { from: isoDate(start), to: isoDate(end) }
}

function loadForAnchor() {
  const { from, to } = feedWindow(anchorDate.value)
  void store.loadFeed(from, to)
  void router.replace({ query: { from, to } })
}

// ── Navigation ──
function navigate(delta: number) {
  if (viewMode.value === 'month') {
    anchorDate.value = new Date(anchorDate.value.getFullYear(), anchorDate.value.getMonth() + delta, 1)
  } else {
    const d = new Date(anchorDate.value)
    d.setDate(d.getDate() + delta * 7)
    anchorDate.value = d
  }
}

// ── Empty state ──
const isEmpty = computed(() => !loading.value && !error.value && days.value.length > 0 && days.value.every((d) => d.items.length === 0))

// ── Popover state ──
const activeCell = ref<CalendarDayCell | null>(null)
const activeCellRect = ref<DOMRect | null>(null)

function openPopover(cell: CalendarDayCell, rect: DOMRect) {
  activeCell.value = cell
  activeCellRect.value = rect
}

function closePopover() {
  activeCell.value = null
  activeCellRect.value = null
}

// ── Init ──
onMounted(() => {
  // Responsive default: week view on mobile
  if (window.matchMedia('(max-width: 768px)').matches) {
    viewMode.value = 'week'
  }

  const qFrom = route.query.from
  const qTo = route.query.to
  if (typeof qFrom === 'string' && typeof qTo === 'string') {
    const fromDate = new Date(qFrom + 'T00:00:00')
    if (!isNaN(fromDate.getTime())) {
      anchorDate.value = new Date(fromDate.getFullYear(), fromDate.getMonth(), 1)
    }
    void store.loadFeed(qFrom, qTo)
  } else {
    loadForAnchor()
  }
  void loadPlanWindows()
})

// Recompute feed when anchor or view mode changes
watch([anchorDate, viewMode], () => loadForAnchor())
</script>

<template>
  <AppShell title="Calendar" subtitle="Schedule, reschedule &amp; track compliance">
    <!-- View-header actions -->
    <template #actions>
      <div class="flex items-center gap-3">
        <MonthWeekToggle v-model="viewMode" />
        <div class="flex items-center gap-0.5">
          <Button
            variant="ghost"
            size="icon"
            class="size-8"
            :aria-label="viewMode === 'month' ? 'Previous month' : 'Previous week'"
            @click="navigate(-1)"
          >
            <ChevronLeft :size="16" />
          </Button>
          <span class="w-[140px] text-center text-[13px] font-semibold text-foreground">{{ periodLabel }}</span>
          <Button
            variant="ghost"
            size="icon"
            class="size-8"
            :aria-label="viewMode === 'month' ? 'Next month' : 'Next week'"
            @click="navigate(1)"
          >
            <ChevronRight :size="16" />
          </Button>
        </div>
        <ComplianceLegend />
      </div>
    </template>

    <!-- Error banner (drag reschedule errors) -->
    <div
      v-if="drag.error.value"
      class="mb-2 flex items-center gap-2 rounded border border-rose-300 bg-rose-50 px-3 py-2 text-[13px] text-rose-800"
    >
      <span class="flex-1">Reschedule failed: {{ drag.error.value }}</span>
      <button
        class="inline-flex size-5 items-center justify-center rounded focus:outline-none"
        aria-label="Dismiss"
        @click="drag.error.value = null"
      >
        <X :size="12" />
      </button>
    </div>

    <!-- Loading skeleton -->
    <template v-if="loading && !feed">
      <!-- Month skeleton -->
      <template v-if="viewMode === 'month'">
        <div class="grid grid-cols-7 gap-px">
          <div v-for="i in 42" :key="i" class="h-[88px] animate-pulse rounded bg-muted" />
        </div>
      </template>
      <!-- Week skeleton -->
      <template v-else>
        <div class="grid grid-cols-7 gap-px">
          <div v-for="i in 7" :key="i" class="h-[200px] animate-pulse rounded bg-muted" />
        </div>
      </template>
    </template>

    <!-- Empty state -->
    <div
      v-else-if="isEmpty"
      class="py-16 text-center"
    >
      <p class="text-[15px] font-semibold text-foreground">No training scheduled yet</p>
      <p class="mt-1 text-sm text-muted-foreground">
        <RouterLink to="/plans" class="text-primary-hi underline underline-offset-2">Create a plan</RouterLink>
        to get started.
      </p>
    </div>

    <!-- Error state -->
    <div v-else-if="error && !feed" class="rounded-lg border border-border bg-muted p-6 text-center">
      <p class="text-sm font-semibold text-foreground">Could not load calendar</p>
      <p class="mt-1 font-mono text-[11px] text-muted-foreground">{{ error.message }}</p>
    </div>

    <!-- Content: Month or Week -->
    <template v-else>
      <CalendarGrid
        v-if="viewMode === 'month'"
        :days="days"
        :anchor-month="anchorMonth"
        @open-popover="openPopover"
      />
      <WeekStrip
        v-else
        :days="days"
        :week-start="weekStart"
        @open-popover="openPopover"
      />
    </template>

    <!-- Popover (driven by activeCell + activeCellRect) -->
    <DayDetailPopover
      v-if="activeCell && activeCellRect"
      :cell="activeCell"
      :anchor-rect="activeCellRect"
      @close="closePopover"
    />
  </AppShell>
</template>