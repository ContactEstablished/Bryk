<script setup lang="ts">
import { computed, inject } from 'vue'
import { complianceColor, sportColor } from '@/lib/calendar'
import { DRAG_RESCHEDULE_KEY } from '@/composables/injectionKeys'
import type { CalendarItemDto } from '@/types/calendar'

const props = defineProps<{
  item: CalendarItemDto
  /** The date of the day cell containing this chip (YYYY-MM-DD). */
  currentDate: string
}>()

const drag = inject(DRAG_RESCHEDULE_KEY, null)

const sportCls = computed(() => sportColor(props.item.sport))

const dot = computed(() => (props.item.kind !== 'Event' ? complianceColor(props.item.compliance) : null))

const priorityLabel = computed(() => {
  if (props.item.kind !== 'Event' || !props.item.priority) return null
  return props.item.priority
})

const loadDisplay = computed(() => {
  const l = props.item.load
  if (l == null) return null
  return Number.isInteger(l) ? l : Math.round(l * 10) / 10
})

const isDraggable = computed(() => props.item.kind === 'Planned')

const isDragging = computed(
  () => isDraggable.value && drag?.isDragging.value && drag.draggingItem.value?.id === props.item.id,
)

const cursorClass = computed(() => (isDraggable.value ? 'cursor-grab' : 'cursor-default'))

function onPointerDown(event: PointerEvent) {
  if (!isDraggable.value || !drag) return
  // Stamp the origin date onto a shallow clone so the composable can
  // compare the drop target against the chip's current day cell.
  const itemWithDate = { ...props.item, __currentDate: props.currentDate }
  drag.onPointerDown(itemWithDate, event)
}
</script>

<template>
  <div
    class="group flex items-center gap-1.5 rounded-md border border-border bg-muted/50 px-1.5 py-0.5 text-[11px] leading-tight transition-shadow hover:shadow-sm"
    :class="[cursorClass, { 'is-dragging': isDragging, 'touch-none': isDraggable }]"
    @pointerdown="onPointerDown"
  >
    <!-- Sport color square -->
    <span class="size-2 shrink-0 rounded-sm" :class="sportCls" />

    <!-- Title (truncated) -->
    <span class="min-w-0 truncate font-medium text-foreground">{{ item.title }}</span>

    <!-- Load number -->
    <span v-if="loadDisplay != null" class="shrink-0 font-mono text-[10px] text-muted-foreground">
      {{ loadDisplay }}
    </span>

    <!-- Unplanned tag -->
    <span
      v-if="item.isUnplanned"
      class="shrink-0 rounded-sm border border-border-strong px-1 font-mono text-[9px] uppercase text-faint"
    >
      unplanned
    </span>

    <!-- Event priority badge -->
    <span
      v-if="priorityLabel"
      class="shrink-0 rounded-sm border border-border-strong px-1 font-mono text-[9px] uppercase text-faint"
    >
      {{ priorityLabel }}
    </span>

    <!-- Compliance dot (right-aligned; absent for events) -->
    <span v-if="dot && dot.dot" class="ml-auto size-1.5 shrink-0 rounded-full" :class="dot.dot" />
  </div>
</template>