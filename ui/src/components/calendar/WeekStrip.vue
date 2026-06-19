<script setup lang="ts">
import { computed } from 'vue'
import CalendarDayCell from '@/components/calendar/CalendarDayCell.vue'
import { isoDate } from '@/lib/calendar'
import type { CalendarDayCell as CalendarDayCellType } from '@/lib/calendar'
import type { CalendarDayDto } from '@/types/calendar'

const props = defineProps<{
  days: CalendarDayDto[]
  weekStart: string
}>()

const emit = defineEmits<{
  openPopover: [cell: CalendarDayCellType, rect: DOMRect]
}>()

const today = computed(() => isoDate(new Date()))

// Derive the 7-day window from weekStart (Monday as YYYY-MM-DD)
const weekDates = computed(() => {
  const start = new Date(props.weekStart + 'T00:00:00')
  const dates: Date[] = []
  for (let i = 0; i < 7; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    dates.push(d)
  }
  return dates
})

const weekCells = computed(() => {
  const byDate = new Map(props.days.map((d) => [d.date, d]))
  return weekDates.value.map((d) => {
    const dateStr = isoDate(d)
    const dayDto = byDate.get(dateStr)
    return {
      date: dateStr,
      items: dayDto?.items ?? [],
      isInMonth: true,
      isToday: dateStr === today.value,
    }
  })
})
</script>

<template>
  <div>
    <!-- Day-of-week header row -->
    <div class="grid grid-cols-7">
      <div
        v-for="day in ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']"
        :key="day"
        class="px-1 pb-1.5 pt-1 text-center font-mono text-[10px] uppercase tracking-[0.08em] text-faint"
      >
        {{ day }}
      </div>
    </div>

    <!-- Week strip: 7 CalendarDayCell instances side by side -->
    <div class="grid grid-cols-7 border-l border-border">
      <CalendarDayCell
        v-for="(cell, di) in weekCells"
        :key="cell.date"
        :cell="cell"
        :today="today"
        :class="[di < 6 ? 'border-r' : '', 'min-h-[200px]']"
        @open-popover="(cell, rect) => emit('openPopover', cell, rect)"
      />
    </div>
  </div>
</template>