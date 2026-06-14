<script setup lang="ts">
import { watch } from 'vue'
import { storeToRefs } from 'pinia'
import ChartRangeToggle from '@/components/charts/ChartRangeToggle.vue'
import PMCChart from '@/components/charts/PMCChart.vue'
import { useAnalyticsStore } from '@/stores/analytics'
import { PMC_RANGES, type PmcRange } from '@/services/analytics'

const props = defineProps<{ modelValue: PmcRange }>()
defineEmits<{ 'update:modelValue': [value: PmcRange] }>()

const store = useAnalyticsStore()
const { progressPmc, pmcSeries, progressLoading } = storeToRefs(store)

const rangeLabel: Record<PmcRange, string> = { '6w': '6w ago', '3m': '3m ago', '6m': '6m ago' }

// Single source of truth: props.modelValue. immediate covers mount; the parent (ProgressView) updates the
// value via the toggle's v-model and the URL, which flows back here and reloads.
watch(() => props.modelValue, (range) => store.loadProgressPmc(range), { immediate: true })

function fmt(n: number | undefined): string {
  return n == null ? '—' : Math.round(n).toString()
}
</script>

<template>
  <section class="card-surface flex flex-col gap-4 p-5">
    <header class="flex flex-wrap items-center gap-3">
      <div class="flex flex-col">
        <h2 class="text-[15px] font-semibold tracking-[-0.02em] text-foreground">Performance Manager</h2>
        <span class="eyebrow text-faint">CTL · ATL · daily load</span>
      </div>
      <div class="ml-auto">
        <ChartRangeToggle
          :model-value="modelValue"
          :options="PMC_RANGES"
          aria-label="PMC range"
          @update:model-value="(v) => $emit('update:modelValue', v as PmcRange)"
        />
      </div>
    </header>

    <p v-if="progressLoading && !progressPmc" class="py-10 text-center text-sm text-muted-foreground">
      Loading…
    </p>
    <PMCChart v-else :series="pmcSeries" :start-label="rangeLabel[modelValue]" />

    <!-- Accessible legend (the SVG is aria-hidden). -->
    <div class="flex flex-wrap gap-4 font-mono text-[11px] text-muted-foreground">
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-accent)" />
        Fitness (CTL) · {{ fmt(progressPmc?.current?.ctl) }}
      </span>
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-warn)" />
        Fatigue (ATL) · {{ fmt(progressPmc?.current?.atl) }}
      </span>
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-fg-3)" />
        Daily load
      </span>
    </div>
  </section>
</template>
