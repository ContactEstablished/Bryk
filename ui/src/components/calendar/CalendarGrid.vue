<script setup lang="ts">
import { computed, onMounted, onUnmounted, provide, toRef, watch } from 'vue'
import { X } from 'lucide-vue-next'
import { useEventListener } from '@vueuse/core'
import CalendarDayCell from '@/components/calendar/CalendarDayCell.vue'
import { DRAG_RESCHEDULE_KEY } from '@/composables/injectionKeys'
import { useDragReschedule, type PlanWindow } from '@/composables/useDragReschedule'
import { buildMonthMatrix } from '@/lib/calendar'
import { isoDate } from '@/lib/calendar'
import { useCalendarStore } from '@/stores/calendar'
import type { CalendarDayDto } from '@/types/calendar'

const props = defineProps<{
  days: CalendarDayDto[]
  anchorMonth: { year: number; month: number }
  planWindows: Record<string, PlanWindow>
}>()

const calendarStore = useCalendarStore()

const today = computed(() => isoDate(new Date()))

const matrix = computed(() => buildMonthMatrix(props.days, props.anchorMonth, today.value))

const dayHeaders = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

const drag = useDragReschedule({
  planWindows: toRef(props, 'planWindows'),
  onRescheduled: () => calendarStore.loadFeed(),
})

provide(DRAG_RESCHEDULE_KEY, drag)

// Global pointer listeners while dragging — attached to window so
// elementFromPoint can see day cells outside the grid root element.
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

// Esc-to-cancel (Step 6).
useEventListener('keydown', (e) => {
  if (e.key === 'Escape' && drag.isDragging.value) {
    drag.cancelDrag()
  }
})

// Auto-clear error after 5 s (Step 5).
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
</script>

<template>
  <div>
    <!-- Error banner (Step 5) -->
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

    <!-- Day-of-week header row -->
    <div class="grid grid-cols-7">
      <div
        v-for="day in dayHeaders"
        :key="day"
        class="px-1 pb-1.5 pt-1 text-center font-mono text-[10px] uppercase tracking-[0.08em] text-faint"
      >
        {{ day }}
      </div>
    </div>

    <!-- Calendar grid -->
    <div class="grid grid-cols-7 border-l border-border">
      <template v-for="(row, wi) in matrix" :key="wi">
        <CalendarDayCell
          v-for="(cell, di) in row"
          :key="cell.date"
          :cell="cell"
          :today="today"
          :class="di < 6 ? 'border-r' : ''"
        />
      </template>
    </div>
  </div>
</template>