<script setup lang="ts">
import { computed, useId } from 'vue'
import { buildRingGeometry } from '@/lib/progressRing'

const props = withDefaults(
  defineProps<{
    /** Fill fraction in [0, 1]. Clamped/NaN-guarded by buildRingGeometry. */
    fraction: number
    centerValue?: string | number
    centerLabel?: string
    size?: number
    animate?: boolean
  }>(),
  { size: 160, animate: true },
)

// Unique per instance so multiple rings on a page don't share gradients.
const gradientId = useId()

const geometry = computed(() => buildRingGeometry(props.fraction, { size: props.size }))
</script>

<template>
  <div
    class="relative inline-flex items-center justify-center"
    :style="{ width: `${geometry.size}px`, height: `${geometry.size}px` }"
  >
    <svg
      :viewBox="`0 0 ${geometry.size} ${geometry.size}`"
      :width="geometry.size"
      :height="geometry.size"
      class="block"
      aria-hidden="true"
    >
      <defs>
        <linearGradient :id="gradientId" x1="0" x2="1" y1="0" y2="1">
          <stop offset="0%" stop-color="var(--bryk-accent-hi)" />
          <stop offset="100%" stop-color="var(--bryk-accent-lo)" />
        </linearGradient>
      </defs>

      <!-- Track -->
      <circle
        :cx="geometry.cx"
        :cy="geometry.cy"
        :r="geometry.radius"
        fill="none"
        stroke="var(--bryk-fg-3)"
        stroke-opacity="0.25"
        :stroke-width="geometry.stroke"
        vector-effect="non-scaling-stroke"
      />

      <!-- Tick marks, straddling the track -->
      <line
        v-for="(t, i) in geometry.ticks"
        :key="i"
        :x1="t.x1"
        :y1="t.y1"
        :x2="t.x2"
        :y2="t.y2"
        stroke="var(--bryk-fg-3)"
        stroke-opacity="0.4"
        stroke-width="1"
        vector-effect="non-scaling-stroke"
      />

      <!-- Progress arc: dash-based fill, drawn clockwise from 12 o'clock -->
      <circle
        class="progress-ring-arc"
        :class="{ 'progress-ring-arc--animate': animate }"
        :cx="geometry.cx"
        :cy="geometry.cy"
        :r="geometry.radius"
        fill="none"
        :stroke="`url(#${gradientId})`"
        :stroke-width="geometry.stroke"
        stroke-linecap="round"
        :stroke-dasharray="geometry.dashArray"
        :stroke-dashoffset="geometry.dashOffset"
        :style="{ '--ring-circumference': geometry.circumference }"
        vector-effect="non-scaling-stroke"
        :transform="`rotate(-90 ${geometry.cx} ${geometry.cy})`"
      />
    </svg>

    <div class="absolute inset-0 flex flex-col items-center justify-center gap-1">
      <slot name="center">
        <span
          v-if="centerValue !== undefined"
          class="bg-[linear-gradient(180deg,var(--bryk-fg-0),var(--bryk-fg-2))] bg-clip-text text-4xl font-bold leading-[0.9] tracking-[-0.05em] tabular-nums text-transparent"
        >{{ centerValue }}</span>
        <span v-if="centerLabel" class="eyebrow">{{ centerLabel }}</span>
      </slot>
    </div>
  </div>
</template>

<style scoped>
/* Draw-in: the arc sweeps from empty (offset = full circumference) to its rendered
   stroke-dashoffset. Only the `from` keyframe is declared so the implicit `to` picks up
   whatever offset the element currently specifies — no need to thread the target through CSS.
   Mirrors Sparkline's draw-line keyframe rather than a transition, which would not run on mount. */
@keyframes ring-draw {
  from {
    stroke-dashoffset: var(--ring-circumference);
  }
}
.progress-ring-arc--animate {
  animation: ring-draw 600ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
}

@media (prefers-reduced-motion: reduce) {
  .progress-ring-arc--animate {
    animation: none;
  }
}
</style>
