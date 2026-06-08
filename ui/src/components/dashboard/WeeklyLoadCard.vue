<script setup lang="ts">
import { onMounted } from 'vue'
import { useTrainingStore } from '@/stores/training'

const store = useTrainingStore()

onMounted(() => {
  if (!store.thisWeek) void store.loadThisWeek()
})
</script>

<template>
  <div class="rounded-lg border bg-card p-5">
    <h3 class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
      Weekly Load
    </h3>

    <!-- Loading (this week not yet fetched) -->
    <p v-if="!store.thisWeek" class="mt-3 text-sm text-muted-foreground">
      Loading…
    </p>

    <!-- Populated: the summed effective load for the current week. -->
    <div v-else class="mt-3">
      <span class="font-mono text-3xl font-semibold">{{ store.thisWeek.weeklyLoad ?? 0 }}</span>
      <span class="ml-1 text-sm text-muted-foreground">TSS</span>
      <p class="mt-1 text-xs text-muted-foreground">planned this week</p>
    </div>
  </div>
</template>
