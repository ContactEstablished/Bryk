<script setup lang="ts">
import { computed, useId } from 'vue'

const props = withDefaults(
  defineProps<{
    data: number[]
    width?: number
    height?: number
    accent?: boolean
  }>(),
  { width: 120, height: 36, accent: true },
)

// Unique per instance so multiple sparklines on a page don't share gradients.
const gradientId = useId()

// Path math ported from the design prototype (charts.jsx Sparkline):
// min/max normalize with a 1px inset so the stroke never clips.
const points = computed(() => {
  const min = Math.min(...props.data)
  const max = Math.max(...props.data)
  const range = max - min || 1
  return props.data.map((v, i) => {
    const x = (i / (props.data.length - 1)) * (props.width - 2) + 1
    const y = props.height - 2 - ((v - min) / range) * (props.height - 4)
    return [x, y] as const
  })
})

const linePath = computed(() =>
  points.value.map((p, i) => (i ? 'L' : 'M') + p[0].toFixed(1) + ' ' + p[1].toFixed(1)).join(' '),
)

const areaPath = computed(
  () => `${linePath.value} L ${props.width - 1} ${props.height - 1} L 1 ${props.height - 1} Z`,
)

const stroke = computed(() => (props.accent ? 'var(--bryk-accent-hi)' : 'var(--bryk-fg-2)'))

const lastPoint = computed(() => points.value[points.value.length - 1])
</script>

<template>
  <svg
    v-if="data.length >= 2"
    :viewBox="`0 0 ${width} ${height}`"
    :style="{ height: `${height}px` }"
    class="block w-full"
    preserveAspectRatio="none"
    aria-hidden="true"
  >
    <defs>
      <linearGradient :id="gradientId" x1="0" x2="0" y1="0" y2="1">
        <stop offset="0%" :stop-color="stroke" stop-opacity="0.35" />
        <stop offset="100%" :stop-color="stroke" stop-opacity="0" />
      </linearGradient>
    </defs>
    <path class="sparkline-fill" :d="areaPath" :fill="`url(#${gradientId})`" />
    <path
      class="sparkline-line"
      :d="linePath"
      pathLength="1"
      fill="none"
      :stroke="stroke"
      stroke-width="1.6"
      stroke-linecap="round"
      stroke-linejoin="round"
      vector-effect="non-scaling-stroke"
    />
    <circle class="sparkline-dot" :cx="lastPoint[0]" :cy="lastPoint[1]" r="2.5" :fill="stroke" />
  </svg>
</template>
