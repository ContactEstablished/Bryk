<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { Link2 } from 'lucide-vue-next'
import ProgressRing from '@/components/common/ProgressRing.vue'
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'
import { daysUntil, formatEventDate } from '@/lib/dateFormat'
import { useCountUp } from '@/composables/useCountUp'
import type { EventPriority } from '@/types/onboarding'
import type { EventListItem } from '@/types/goals'

const props = defineProps<{ event: EventListItem }>()

const days = computed(() => daysUntil(props.event.eventDate))
const weeks = computed(() => Math.ceil(days.value / 7))
const animatedWeeks = useCountUp(weeks)

const formattedDate = computed(() => formatEventDate(props.event.eventDate))

// Rolling-horizon fill: LinkedPlanDto carries id + name only, so there is no plan start date to
// measure a true [start, target] window against. Same 24-week horizon as the dashboard's
// PrimaryGoalCard, driven by days so it advances daily. Past events clamp to a full ring.
const HORIZON_DAYS = 168

const fraction = computed(() => Math.min(1, Math.max(0, 1 - days.value / HORIZON_DAYS)))

// A is the athlete's headline race, B/C progressively less so — the badge mutes to match.
const priorityClass: Record<EventPriority, string> = {
  A: 'border-primary-lo bg-primary-glow text-primary-hi',
  B: 'border-border-strong bg-muted text-subtle',
  C: 'border-border text-faint',
}
</script>

<template>
  <article class="card-surface flex items-start gap-4 p-5">
    <div class="flex min-w-0 flex-1 flex-col gap-2">
      <div class="flex items-center gap-2">
        <TypePill v-if="event.sport" :kind="sportToPillKind(event.sport)">{{ event.sport }}</TypePill>
        <span
          class="inline-flex items-center rounded-full border px-2 py-1 font-mono text-[10px] uppercase tracking-[0.08em]"
          :class="priorityClass[event.priority]"
          :aria-label="`Priority ${event.priority}`"
        >
          {{ event.priority }}
        </span>
      </div>

      <div>
        <h3 class="truncate text-[15px] font-bold tracking-[-0.02em] text-foreground">{{ event.name }}</h3>
        <p class="mt-0.5 font-mono text-xs text-muted-foreground">{{ formattedDate }}</p>
      </div>

      <!-- Notes, rendered inline at last -->
      <p v-if="event.notes" class="text-[13px] leading-relaxed text-subtle">{{ event.notes }}</p>

      <!-- Linked plans (display-only — the write path lands in a later phase) -->
      <div v-if="event.linkedPlans.length > 0" class="flex flex-wrap gap-1.5">
        <RouterLink
          v-for="plan in event.linkedPlans"
          :key="plan.id"
          :to="`/plans/${plan.id}`"
          class="inline-flex items-center gap-1 rounded-full border border-border-strong bg-muted px-2 py-1 font-mono text-[10px] text-subtle transition-colors hover:border-primary-lo hover:text-primary-hi"
        >
          <Link2 :size="11" />
          {{ plan.name }}
        </RouterLink>
      </div>
    </div>

    <ProgressRing :fraction="fraction" :size="88" class="shrink-0">
      <template #center>
        <span
          v-if="days <= 1"
          class="text-base font-bold leading-none tracking-[-0.03em] text-foreground"
        >
          {{ days < 0 ? 'Done' : days === 0 ? 'Today' : 'Tomorrow' }}
        </span>
        <template v-else>
          <span class="text-2xl font-bold leading-none tracking-[-0.04em] tabular-nums text-foreground">
            {{ animatedWeeks }}
          </span>
          <span class="eyebrow">weeks</span>
        </template>
      </template>
    </ProgressRing>
  </article>
</template>
