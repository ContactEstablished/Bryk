<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { ChevronLeft, ChevronRight } from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import CalendarGrid from '@/components/calendar/CalendarGrid.vue'
import MonthWeekToggle from '@/components/calendar/MonthWeekToggle.vue'
import { isoDate } from '@/lib/calendar'
import { useCalendarStore } from '@/stores/calendar'
import { getPlans } from '@/services/training'
import type { CalendarDayDto } from '@/types/calendar'
import type { PlanWindow } from '@/composables/useDragReschedule'

const route = useRoute()
const router = useRouter()
const store = useCalendarStore()
const { feed, loading, error } = storeToRefs(store)

type ViewMode = 'month' | 'week'
const viewMode = ref<ViewMode>('month')

// Anchor month as a Date frozen to the 1st of the anchor month.
const anchorDate = ref<Date>(new Date(new Date().getFullYear(), new Date().getMonth(), 1))

const anchorMonth = computed(() => ({
  year: anchorDate.value.getFullYear(),
  month: anchorDate.value.getMonth() + 1,
}))

const periodLabel = computed(() =>
  new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(anchorDate.value),
)

const days = computed<CalendarDayDto[]>(() => feed.value?.days ?? [])

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
    // Silently ignore — plan windows are used for the client-side window check,
    // but the server is the authority (400 on out-of-window).
    planWindows.value = {}
  }
}

// Compute the feed window for the visible anchor month.
function feedWindow(d: Date): { from: string; to: string } {
  const year = d.getFullYear()
  const month = d.getMonth() // 0-based
  const from = isoDate(new Date(year, month, 1))
  const to = isoDate(new Date(year, month + 1, 0))
  return { from, to }
}

function navigateMonth(delta: number) {
  anchorDate.value = new Date(anchorDate.value.getFullYear(), anchorDate.value.getMonth() + delta, 1)
}

// Feed the visible month.
function loadForAnchor() {
  const { from, to } = feedWindow(anchorDate.value)
  void store.loadFeed(from, to)
  // Sync query params (match ProgressView convention).
  void router.replace({ query: { from, to } })
}

// On mount: if query params present, use them; else load the current month.
onMounted(() => {
  const qFrom = route.query.from
  const qTo = route.query.to
  if (typeof qFrom === 'string' && typeof qTo === 'string') {
    // Parse the anchor month from `from` (the 1st of some month).
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

// Recompute feed when the anchor month changes.
watch(anchorDate, () => loadForAnchor())
</script>

<template>
  <AppShell title="Calendar" subtitle="Schedule, reschedule & track compliance">
    <!-- View-header actions -->
    <template #actions>
      <div class="flex items-center gap-2">
        <MonthWeekToggle v-model="viewMode" />
        <div class="flex items-center gap-0.5">
          <Button variant="ghost" size="icon" class="size-8" aria-label="Previous month" @click="navigateMonth(-1)">
            <ChevronLeft :size="16" />
          </Button>
          <span class="w-[120px] text-center text-[13px] font-semibold text-foreground">{{ periodLabel }}</span>
          <Button variant="ghost" size="icon" class="size-8" aria-label="Next month" @click="navigateMonth(1)">
            <ChevronRight :size="16" />
          </Button>
        </div>
      </div>
    </template>

    <!-- Loading skeleton (week view placeholder is handled separately) -->
    <template v-if="viewMode === 'month'">
      <div v-if="loading && !feed" class="grid grid-cols-7 gap-px">
        <div
          v-for="i in 42"
          :key="i"
          class="h-[88px] animate-pulse rounded bg-muted"
        />
      </div>

      <!-- Empty state -->
      <div
        v-else-if="!loading && !error && days.length === 0"
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

      <!-- Month grid -->
      <CalendarGrid v-else :days="days" :anchor-month="anchorMonth" :plan-windows="planWindows" />
    </template>

    <!-- Week placeholder (16-5 replaces with WeekStrip) -->
    <div v-else class="py-16 text-center text-sm text-muted-foreground border border-dashed border-border rounded-lg">
      Week view coming next (Task 16-5).
    </div>
  </AppShell>
</template>
