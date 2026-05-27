<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
}>()
</script>

<template>
  <div class="rounded-lg border bg-card p-5">
    <h3 class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
      {{ title }}
    </h3>
    <div class="mt-3">
      <span class="font-mono text-3xl font-semibold text-muted-foreground/40">—</span>
    </div>
    <p v-if="subtitle" class="mt-3 text-xs text-muted-foreground">
      {{ subtitle }}
    </p>
  </div>
</template>
