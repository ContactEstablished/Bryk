<script setup lang="ts">
import CalendarItemChip from '@/components/calendar/CalendarItemChip.vue'
import type { CalendarDayCell } from '@/lib/calendar'

defineProps<{
  cell: CalendarDayCell
  today: string
}>()
</script>

<template>
  <div
    class="flex min-h-[88px] flex-col gap-1 border-t border-border p-1"
    :class="cell.isInMonth ? 'bg-background' : 'bg-muted/30'"
  >
    <!-- Date number -->
    <div class="flex items-center gap-1 px-0.5 pt-0.5">
      <span
        class="inline-flex items-center justify-center text-[12px] font-medium leading-none"
        :class="
          cell.isToday
            ? 'size-6 rounded-full bg-primary-hi text-primary-foreground'
            : cell.isInMonth
              ? 'text-foreground'
              : 'text-faint'
        "
      >
        {{ parseInt(cell.date.slice(8), 10) }}
      </span>
    </div>

    <!-- Items (in-month only, cap at 3) -->
    <template v-if="cell.isInMonth && cell.items.length">
      <CalendarItemChip
        v-for="item in cell.items.slice(0, 3)"
        :key="item.id"
        :item="item"
      />
      <span
        v-if="cell.items.length > 3"
        class="cursor-pointer px-1 font-mono text-[10px] text-muted-foreground hover:text-foreground"
      >
        +{{ cell.items.length - 3 }} more
      </span>
    </template>
  </div>
</template>
