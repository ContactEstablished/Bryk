<script setup lang="ts">
import { ArrowDown, ArrowUp } from 'lucide-vue-next'

defineProps<{
  dir: 'up' | 'down' | 'flat'
}>()

const colorClass = {
  up: 'text-good',
  down: 'text-bad',
  flat: 'text-muted-foreground',
} as const
</script>

<template>
  <span
    class="inline-flex items-center gap-1 rounded bg-muted px-1.5 py-0.5 font-mono text-[11px]"
    :class="colorClass[dir]"
  >
    <ArrowUp v-if="dir === 'up'" :size="11" />
    <ArrowDown v-else-if="dir === 'down'" :size="11" />
    <slot />
  </span>
</template>
