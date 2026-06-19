<script setup lang="ts">
import { computed } from 'vue'
import CalendarDayCell from '@/components/calendar/CalendarDayCell.vue'
import { buildMonthMatrix, isoDate } from '@/lib/calendar'
import type { CalendarDayCell as CalendarDayCellType } from '@/lib/calendar'
import type { CalendarDayDto } from '@/types/calendar'

const props = defineProps<{
  days: CalendarDayDto[]
  anchorMonth: { year: number; month: number }
}>()

const emit = defineEmits<{
  openPopover: [cell: CalendarDayCellType, rect: DOMRect]
}>()

const today = computed(() => isoDate(new Date()))

const matrix = computed(() => buildMonthMatrix(props.days, props.anchorMonth, today.value))

const dayHeaders = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
</script>

<template>
  <div>
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
          @open-popover="(cell, rect) => emit('openPopover', cell, rect)"
        />
      </template>
    </div>
  </div>
</template>