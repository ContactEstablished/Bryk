<script setup lang="ts">
import { inject } from 'vue'
import CalendarItemChip from '@/components/calendar/CalendarItemChip.vue'
import { DRAG_RESCHEDULE_KEY } from '@/composables/injectionKeys'
import type { CalendarDayCell } from '@/lib/calendar'

const props = defineProps<{
  cell: CalendarDayCell
  today: string
}>()

const emit = defineEmits<{
  openPopover: [cell: CalendarDayCell, rect: DOMRect]
}>()

const drag = inject(DRAG_RESCHEDULE_KEY, null)

function onHeaderClick(event: MouseEvent) {
  const el = event.currentTarget as HTMLElement
  emit('openPopover', props.cell, el.getBoundingClientRect())
}
</script>

<template>
  <div
    :data-date="cell.date"
    class="flex min-h-[88px] flex-col gap-1 border-t border-border p-1"
    :class="[
      cell.isInMonth ? 'bg-background' : 'bg-muted/30',
      {
        'drop-target': drag?.isDragging.value && drag.draggingOverDate.value === cell.date,
        'drop-rejected':
          drag?.isDragging.value &&
          drag.draggingOverDate.value === cell.date &&
          !drag.canDropHere.value,
      },
    ]"
  >
    <!-- Date number -->
    <div class="flex items-center gap-1 px-0.5 pt-0.5">
      <span
        class="inline-flex cursor-pointer select-none items-center justify-center text-[12px] font-medium leading-none hover:underline"
        :class="
          cell.isToday
            ? 'size-6 rounded-full bg-primary-hi text-primary-foreground'
            : cell.isInMonth
              ? 'text-foreground'
              : 'text-faint'
        "
        @click="onHeaderClick"
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
        :current-date="cell.date"
      />
      <span
        v-if="cell.items.length > 3"
        class="cursor-pointer px-1 font-mono text-[10px] text-muted-foreground hover:text-foreground"
        @click="onHeaderClick"
      >
        +{{ cell.items.length - 3 }} more
      </span>
    </template>
  </div>
</template>

<style scoped>
.drop-target {
  outline: 2px dashed var(--primary-hi, #818cf8);
}

.drop-rejected {
  outline: 2px dashed var(--rose-500, #f43f5e);
}
</style>