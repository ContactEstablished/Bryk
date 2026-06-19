<script setup lang="ts">
import { computed, inject, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { onClickOutside, useEventListener } from '@vueuse/core'
import { complianceColor, sportColor } from '@/lib/calendar'
import { DRAG_RESCHEDULE_KEY } from '@/composables/injectionKeys'
import type { CalendarDayCell } from '@/lib/calendar'
import type { CalendarItemDto } from '@/types/calendar'

const props = defineProps<{
  cell: CalendarDayCell
  anchorRect: DOMRect | null
}>()

const emit = defineEmits<{
  close: []
}>()

const drag = inject(DRAG_RESCHEDULE_KEY, null)
const popoverRef = ref<HTMLElement | null>(null)

// Close on Esc
useEventListener('keydown', (e) => {
  if (e.key === 'Escape') emit('close')
})

// Close on outside click (relative to the popover card, not the backdrop)
onClickOutside(popoverRef, () => emit('close'))

const position = computed(() => {
  const rect = props.anchorRect
  if (!rect) return { top: '50%', left: '50%', transform: 'translate(-50%, -50%)' }
  const popoverW = 320
  const popoverH = 280
  const gap = 4
  let top = rect.bottom + gap
  let left = rect.left
  if (top + popoverH > window.innerHeight) {
    top = rect.top - popoverH - gap
  }
  if (left + popoverW > window.innerWidth) {
    left = Math.max(0, rect.right - popoverW)
  }
  return { top: `${top}px`, left: `${left}px` }
})

const fullDate = computed(() => {
  const d = new Date(props.cell.date + 'T00:00:00')
  return new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'long', day: 'numeric' }).format(d)
})

function loadDisplay(item: CalendarItemDto): string | null {
  const l = item.load
  if (l == null) return null
  return Number.isInteger(l) ? String(l) : String(Math.round(l * 10) / 10)
}

function itemSubtitle(item: CalendarItemDto): string {
  const load = loadDisplay(item)
  if (item.kind === 'Planned') {
    const parts = ['Planned']
    if (load) parts.push(`${load} load`)
    return parts.join(' · ')
  }
  if (item.kind === 'Completed') {
    const parts = ['Completed']
    if (item.isUnplanned) parts.push('unplanned')
    if (load) parts.push(`${load} load`)
    return parts.join(' · ')
  }
  // Event
  const parts: string[] = []
  if (item.priority) parts.push(`${item.priority} priority`)
  if (item.sport) parts.push(item.sport)
  return parts.join(' · ') || 'Event'
}

function linkFor(item: CalendarItemDto): { to: string; label: string } | null {
  if (item.kind === 'Planned' && item.trainingPlanId) {
    return { to: `/plans/${item.trainingPlanId}`, label: 'View structure →' }
  }
  if (item.kind === 'Completed') {
    return { to: `/workouts/${item.id}`, label: 'View workout →' }
  }
  return null
}
</script>

<template>
  <!-- Don't render while a drag is active — belt-and-braces guard. -->
  <template v-if="!drag?.isDragging.value">
    <!-- Fixed backdrop captures clicks to close -->
    <div class="fixed inset-0 z-40" @click="emit('close')" />

    <!-- Popover card -->
    <div
      ref="popoverRef"
      class="fixed z-50 w-[310px] max-w-[90vw] overflow-hidden rounded-xl border border-border-strong bg-background shadow-[0_8px_30px_rgba(0,0,0,0.35)]"
      :style="position"
    >
      <!-- Header: full date -->
      <div class="border-b border-border px-4 py-3">
        <p class="text-[14px] font-semibold text-foreground">{{ fullDate }}</p>
        <p class="mt-0.5 font-mono text-[10px] uppercase tracking-[0.06em] text-muted-foreground">
          {{ cell.items.length }} item{{ cell.items.length !== 1 ? 's' : '' }}
        </p>
      </div>

      <!-- Body: one row per item -->
      <div class="max-h-[260px] space-y-2 overflow-y-auto px-4 py-3">
        <div
          v-for="item in cell.items"
          :key="item.id"
          class="rounded-lg border border-border bg-muted/30 p-2.5"
        >
          <!-- Top row: sport pill + title + compliance dot -->
          <div class="flex items-center gap-2">
            <span class="size-2 shrink-0 rounded-sm" :class="sportColor(item.sport)" />
            <span class="min-w-0 truncate text-[13px] font-medium text-foreground">{{ item.title }}</span>
            <span
              v-if="item.kind !== 'Event' && complianceColor(item.compliance).dot"
              class="ml-auto size-1.5 shrink-0 rounded-full"
              :class="complianceColor(item.compliance).dot"
            />
            <!-- Unplanned tag -->
            <span
              v-if="item.isUnplanned"
              class="shrink-0 rounded-sm border border-border-strong px-1 font-mono text-[9px] uppercase text-faint"
            >
              unplanned
            </span>
            <!-- Event priority badge -->
            <span
              v-if="item.kind === 'Event' && item.priority"
              class="shrink-0 rounded-sm border border-border-strong px-1 font-mono text-[9px] uppercase text-faint"
            >
              {{ item.priority }}
            </span>
          </div>

          <!-- Subtitle line -->
          <p class="mt-1 font-mono text-[11px] text-muted-foreground">{{ itemSubtitle(item) }}</p>

          <!-- Event notes (finally rendered) -->
          <p
            v-if="item.kind === 'Event' && item.notes"
            class="mt-1.5 text-[12px] leading-relaxed text-subtle"
          >
            {{ item.notes }}
          </p>

          <!-- Link-out -->
          <RouterLink
            v-if="linkFor(item)"
            :to="linkFor(item)!.to"
            class="mt-1.5 inline-block font-mono text-[11px] text-primary-hi underline underline-offset-2 hover:text-primary-lo"
            @click.stop
          >
            {{ linkFor(item)!.label }}
          </RouterLink>
        </div>
      </div>

      <!-- Footer: minimal legend reference -->
      <div class="border-t border-border px-4 py-2">
        <p class="font-mono text-[10px] text-faint">
          See page header for the full compliance legend.
        </p>
      </div>
    </div>
  </template>
</template>