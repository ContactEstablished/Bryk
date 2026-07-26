<script setup lang="ts">
import { computed } from 'vue'
import TypePill from '@/components/common/TypePill.vue'
import { formatEventDate } from '@/lib/dateFormat'
import type { GoalType } from '@/types/onboarding'
import type { GoalListItem, GoalStatus } from '@/types/goals'

const props = defineProps<{ goal: GoalListItem }>()

const typeLabel: Record<GoalType, string> = {
  General: 'General',
  EventDriven: 'Event driven',
}

// Server-computed status (GoalProgress.Compute) — the card only styles it.
const statusLabel: Record<GoalStatus, string> = {
  Overdue: 'Overdue',
  DueSoon: 'Due soon',
  Upcoming: 'Upcoming',
  NoDate: 'No date',
}

const statusClass: Record<GoalStatus, string> = {
  Overdue: 'border-destructive/40 bg-destructive/10 text-destructive',
  DueSoon: 'border-primary-lo bg-primary-glow text-primary-hi',
  Upcoming: 'border-border-strong bg-muted text-subtle',
  NoDate: 'border-border text-faint',
}

const formattedDate = computed(() =>
  props.goal.targetDate ? formatEventDate(props.goal.targetDate) : 'No target date',
)

// 'in 12 days' / '3 days ago' / 'today'; nothing at all for an undated goal.
const relativeDays = computed(() => {
  const d = props.goal.daysRemaining
  if (d == null) return null
  if (d === 0) return 'today'
  const n = Math.abs(d)
  const unit = n === 1 ? 'day' : 'days'
  return d > 0 ? `in ${n} ${unit}` : `${n} ${unit} ago`
})
</script>

<template>
  <article class="card-surface flex flex-col gap-2 p-5">
    <div class="flex items-center gap-2">
      <TypePill>{{ typeLabel[goal.type] }}</TypePill>
      <span
        class="ml-auto inline-flex items-center rounded-full border px-2 py-1 font-mono text-[10px] uppercase tracking-[0.08em]"
        :class="statusClass[goal.status]"
      >
        {{ statusLabel[goal.status] }}
      </span>
    </div>

    <p class="text-[15px] font-semibold leading-snug tracking-[-0.01em] text-foreground">
      {{ goal.description }}
    </p>

    <p class="font-mono text-xs text-muted-foreground">
      {{ formattedDate }}<template v-if="relativeDays"> · {{ relativeDays }}</template>
    </p>
  </article>
</template>
