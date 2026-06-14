<script setup lang="ts">
import { computed, useId } from 'vue'
import { buildLoadGeometry, LOAD_DIMS } from '@/lib/charts/load'
import type { OptimalBand, WeeklyLoadWeek } from '@/types/analytics'

const props = defineProps<{
  weeks: WeeklyLoadWeek[]
  optimalBand: OptimalBand | null
}>()

const hatchId = useId()
const fillId = useId()

const geometry = computed(() => buildLoadGeometry(props.weeks, props.optimalBand))
</script>

<template>
  <svg
    v-if="weeks.length > 0"
    :viewBox="`0 0 ${LOAD_DIMS.width} ${LOAD_DIMS.height}`"
    width="100%"
    preserveAspectRatio="xMidYMid meet"
    class="block"
    aria-hidden="true"
  >
    <defs>
      <linearGradient :id="fillId" x1="0" x2="0" y1="0" y2="1">
        <stop offset="0%" stop-color="var(--bryk-accent-hi)" stop-opacity="0.95" />
        <stop offset="100%" stop-color="var(--bryk-accent-lo)" stop-opacity="0.8" />
      </linearGradient>
      <pattern :id="hatchId" patternUnits="userSpaceOnUse" width="6" height="6" patternTransform="rotate(45)">
        <line x1="0" y1="0" x2="0" y2="6" stroke="var(--bryk-fg-3)" stroke-width="3" />
      </pattern>
    </defs>

    <!-- gridlines + y-tick labels -->
    <g v-for="(tick, i) in geometry.yTicks" :key="i">
      <line
        :x1="LOAD_DIMS.padL"
        :x2="LOAD_DIMS.width - LOAD_DIMS.padR"
        :y1="tick.y"
        :y2="tick.y"
        stroke="var(--border)"
        :stroke-dasharray="i === 0 ? '0' : '2 4'"
      />
      <text
        :x="LOAD_DIMS.padL - 10"
        :y="tick.y + 4"
        font-size="10"
        fill="var(--bryk-fg-2)"
        font-family="var(--font-mono, monospace)"
        text-anchor="end"
      >
        {{ tick.value }}
      </text>
    </g>

    <!-- optimal band -->
    <g v-if="geometry.bandRect">
      <rect
        :x="geometry.bandRect.x"
        :y="geometry.bandRect.y"
        :width="geometry.bandRect.width"
        :height="geometry.bandRect.height"
        fill="var(--bryk-good)"
        fill-opacity="0.07"
        stroke="var(--bryk-good)"
        stroke-opacity="0.4"
        stroke-dasharray="3 3"
      />
      <text
        :x="LOAD_DIMS.width - LOAD_DIMS.padR - 6"
        :y="geometry.bandRect.y - 6"
        font-size="9"
        fill="var(--bryk-good)"
        font-family="var(--font-mono, monospace)"
        text-anchor="end"
        letter-spacing="1"
      >
        OPTIMAL BAND
      </text>
    </g>

    <!-- bars: planned hatch behind, actual fill in front -->
    <g v-for="(bar, i) in geometry.bars" :key="i">
      <rect
        :x="bar.plannedRect.x"
        :y="bar.plannedRect.y"
        :width="bar.plannedRect.width"
        :height="bar.plannedRect.height"
        :fill="`url(#${hatchId})`"
        rx="3"
      />
      <rect
        :x="bar.actualRect.x"
        :y="bar.actualRect.y"
        :width="bar.actualRect.width"
        :height="bar.actualRect.height"
        :fill="`url(#${fillId})`"
        rx="3"
      />
      <text
        :x="bar.valueLabel.x"
        :y="bar.valueLabel.y"
        font-size="11"
        :fill="bar.valueLabel.isCurrent ? 'var(--bryk-fg-0)' : 'var(--bryk-fg-1)'"
        font-family="var(--font-mono, monospace)"
        text-anchor="middle"
        :font-weight="bar.valueLabel.isCurrent ? 700 : 500"
      >
        {{ bar.valueLabel.text }}
      </text>
      <text
        :x="bar.weekLabel.x"
        :y="bar.weekLabel.y"
        font-size="10"
        :fill="bar.weekLabel.isCurrent ? 'var(--bryk-fg-0)' : 'var(--bryk-fg-2)'"
        font-family="var(--font-mono, monospace)"
        text-anchor="middle"
        letter-spacing="0.6"
      >
        {{ bar.weekLabel.text }}
      </text>
    </g>

    <!-- 4-week rolling-average trend -->
    <path
      :d="geometry.trendPath"
      fill="none"
      stroke="var(--bryk-warn)"
      stroke-width="2"
      stroke-dasharray="5 4"
      stroke-linecap="round"
    />
    <circle
      v-for="(dot, i) in geometry.trendDots"
      :key="`d${i}`"
      :cx="dot.x"
      :cy="dot.y"
      r="3"
      fill="var(--background)"
      stroke="var(--bryk-warn)"
      stroke-width="1.5"
    />
  </svg>

  <p v-else class="py-10 text-center text-sm text-muted-foreground">
    No weekly load yet — log workouts to see your training load.
  </p>
</template>
