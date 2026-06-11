<script setup lang="ts">
import MetricTile from '@/components/common/MetricTile.vue'

defineProps<{
  title: string
  subtitle?: string
}>()
</script>

<template>
  <MetricTile :label="title" placeholder>
    <template v-if="subtitle" #footer>
      <p class="mt-auto font-mono text-[11px] leading-relaxed text-muted-foreground">
        {{ subtitle }}
      </p>
    </template>
  </MetricTile>
</template>
