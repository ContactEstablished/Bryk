<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useWellnessStore } from '@/stores/wellness'
import { invertedChange, metricSeries } from '@/lib/wellness'

const store = useWellnessStore()

onMounted(() => {
  if (!store.summary) void store.loadSummary()
})

const weight = computed(() => store.summary?.weightKg ?? null)

// One decimal as a STRING — see SleepCard: MetricTile's count-up formats numbers with 0 decimals.
const value = computed(() => {
  const average = weight.value?.average
  return average == null ? null : average.toFixed(1)
})

const spark = computed(() => metricSeries(store.summary?.days ?? [], 'weightKg'))

// No `delta` prop — weight is inverted (ADR-0011 §5): a drop is good news and DeltaChip would render
// it red. The change goes in the footer with invertedChange's own colour.
const change = computed(() =>
  value.value == null ? null : invertedChange(weight.value?.delta, 'kg', 1),
)

// DELIBERATE ASYMMETRY WITH RestingHrCard, and the one place it will read as an oversight:
// there is NO fallback to Athlete.WeightKg (ADR-0011 §1). The profile number is a one-off onboarding
// self-report, and this is a TREND tile — seeding a trend from a single stale self-report would show a
// number the athlete never logged, above a sparkline that cannot move. Resting HR gets the fallback
// because its tile has shipped that exact profile value since Phase 14 and losing it would be a
// regression; weight never had one to lose. An athlete who has never logged sees "—" and the prompt.
</script>

<template>
  <MetricTile
    label="Weight"
    :value="value"
    unit="kg"
    :spark="spark"
    :loading="store.loadingSummary && !store.summary"
  >
    <template #footer>
      <p v-if="change" class="font-mono text-[11px]" :class="change.className">{{ change.text }}</p>
      <p v-else-if="value != null" class="text-xs text-muted-foreground">7-day average</p>
      <p v-else-if="store.summary" class="text-xs text-muted-foreground">
        Log weight to see a trend
      </p>
    </template>
  </MetricTile>
</template>
