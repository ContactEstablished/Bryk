<script setup lang="ts">
import { computed, useId } from 'vue'
import { buildPmcGeometry, PMC_DIMS } from '@/lib/charts/pmc'
import type { PmcPoint } from '@/types/analytics'

const props = withDefaults(
  defineProps<{
    series: PmcPoint[]
    startLabel?: string
    endLabel?: string
  }>(),
  { startLabel: '', endLabel: 'today' },
)

// Unique per instance so multiple charts on a page don't share the gradient id (Sparkline precedent).
const gradientId = useId()

const geometry = computed(() => buildPmcGeometry(props.series))
const baselineY = PMC_DIMS.padT + (PMC_DIMS.height - PMC_DIMS.padT - PMC_DIMS.padB)
</script>

<template>
  <svg
    v-if="series.length >= 2"
    :viewBox="`0 0 ${PMC_DIMS.width} ${PMC_DIMS.height}`"
    width="100%"
    preserveAspectRatio="xMidYMid meet"
    class="block"
    aria-hidden="true"
  >
    <defs>
      <linearGradient :id="gradientId" x1="0" x2="0" y1="0" y2="1">
        <stop offset="0%" stop-color="var(--bryk-accent)" stop-opacity="0.35" />
        <stop offset="100%" stop-color="var(--bryk-accent)" stop-opacity="0" />
      </linearGradient>
    </defs>

    <!-- daily-load tick bars -->
    <line
      v-for="(bar, i) in geometry.loadBars"
      :key="i"
      :x1="bar.x"
      :x2="bar.x"
      :y1="bar.y0"
      :y2="bar.y1"
      stroke="var(--bryk-fg-3)"
      stroke-width="1"
      opacity="0.6"
    />

    <!-- CTL fill + line -->
    <path :d="geometry.ctlFillPath" :fill="`url(#${gradientId})`" />
    <path :d="geometry.ctlPath" fill="none" stroke="var(--bryk-accent-hi)" stroke-width="2" />

    <!-- ATL line -->
    <path
      :d="geometry.atlPath"
      fill="none"
      stroke="var(--bryk-warn)"
      stroke-width="1.6"
      stroke-dasharray="4 3"
    />

    <!-- baseline axis + y-tick labels -->
    <line
      :x1="PMC_DIMS.padL"
      :x2="PMC_DIMS.width - PMC_DIMS.padR"
      :y1="baselineY"
      :y2="baselineY"
      stroke="var(--border)"
    />
    <text
      v-for="(tick, i) in geometry.yTicks"
      :key="i"
      :x="PMC_DIMS.padL - 8"
      :y="tick.y + 4"
      font-size="10"
      fill="var(--bryk-fg-2)"
      font-family="var(--font-mono, monospace)"
      text-anchor="end"
    >
      {{ tick.value }}
    </text>

    <!-- x-axis labels -->
    <text
      :x="PMC_DIMS.padL"
      :y="PMC_DIMS.height - 8"
      font-size="10"
      fill="var(--bryk-fg-2)"
      font-family="var(--font-mono, monospace)"
    >
      {{ startLabel }}
    </text>
    <text
      :x="PMC_DIMS.width - PMC_DIMS.padR"
      :y="PMC_DIMS.height - 8"
      font-size="10"
      fill="var(--bryk-fg-2)"
      font-family="var(--font-mono, monospace)"
      text-anchor="end"
    >
      {{ endLabel }}
    </text>

    <!-- end markers -->
    <template v-if="geometry.endMarkers">
      <circle
        :cx="geometry.endMarkers.ctl.x"
        :cy="geometry.endMarkers.ctl.y"
        r="4"
        fill="var(--bryk-accent)"
        stroke="var(--background)"
        stroke-width="2"
      />
      <circle
        :cx="geometry.endMarkers.atl.x"
        :cy="geometry.endMarkers.atl.y"
        r="4"
        fill="var(--bryk-warn)"
        stroke="var(--background)"
        stroke-width="2"
      />
    </template>
  </svg>

  <p v-else class="py-10 text-center text-sm text-muted-foreground">
    Not enough history yet — log workouts to see your fitness trend.
  </p>
</template>
