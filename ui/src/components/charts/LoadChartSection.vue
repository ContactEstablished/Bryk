<script setup lang="ts">
import { computed, watch } from 'vue'
import { storeToRefs } from 'pinia'
import ChartRangeToggle from '@/components/charts/ChartRangeToggle.vue'
import LoadChart from '@/components/charts/LoadChart.vue'
import { useAnalyticsStore } from '@/stores/analytics'
import { WEEKLY_LOAD_RANGES } from '@/services/analytics'

const props = defineProps<{ modelValue: number }>()
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

const store = useAnalyticsStore()
const { weeklyLoad, weeklyLoadLoading } = storeToRefs(store)

// Single source of truth: props.modelValue (the week span). immediate covers mount; the toggle/URL drive it.
watch(() => props.modelValue, (weeks) => store.loadWeeklyLoad(weeks), { immediate: true })

const toggleValue = computed(() => String(props.modelValue))
</script>

<template>
  <section class="card-surface flex flex-col gap-4 p-5">
    <header class="flex flex-wrap items-center gap-3">
      <div class="flex flex-col">
        <h2 class="text-[15px] font-semibold tracking-[-0.02em] text-foreground">Weekly Training Load</h2>
        <span class="eyebrow text-faint">Planned vs actual · TSS</span>
      </div>
      <div class="ml-auto">
        <ChartRangeToggle
          :model-value="toggleValue"
          :options="WEEKLY_LOAD_RANGES"
          aria-label="Weekly-load span"
          @update:model-value="(v) => emit('update:modelValue', Number(v))"
        />
      </div>
    </header>

    <p v-if="weeklyLoadLoading && !weeklyLoad" class="py-10 text-center text-sm text-muted-foreground">
      Loading…
    </p>
    <LoadChart v-else :weeks="weeklyLoad?.weeks ?? []" :optimal-band="weeklyLoad?.optimalBand ?? null" />

    <!-- Accessible legend (the SVG is aria-hidden). -->
    <div class="flex flex-wrap gap-4 font-mono text-[11px] text-muted-foreground">
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-accent-hi)" />
        Actual
      </span>
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-fg-3)" />
        Planned
      </span>
      <span class="inline-flex items-center gap-1.5">
        <i class="size-2 rounded-full" style="background: var(--bryk-warn)" />
        4-wk avg
      </span>
    </div>
  </section>
</template>
