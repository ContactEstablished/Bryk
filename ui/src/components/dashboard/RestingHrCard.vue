<script setup lang="ts">
import { onMounted } from 'vue'
import { useProfileStore } from '@/stores/profile'

const store = useProfileStore()

onMounted(() => {
  if (!store.recommended) void store.loadRecommended()
})
</script>

<template>
  <div class="rounded-lg border bg-card p-5">
    <h3 class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
      Resting HR
    </h3>

    <!-- Populated -->
    <div v-if="store.recommended && store.recommended.restingHr != null" class="mt-3">
      <span class="font-mono text-3xl font-semibold">{{ store.recommended.restingHr }}</span>
      <span class="ml-1 text-sm text-muted-foreground">bpm</span>
    </div>

    <!-- Loading (recommended not yet fetched) -->
    <p v-else-if="!store.recommended" class="mt-3 text-sm text-muted-foreground">
      Loading…
    </p>

    <!-- Empty (fetched, no value set) -->
    <div v-else class="mt-3">
      <span class="font-mono text-3xl font-semibold text-muted-foreground/40">—</span>
      <router-link
        to="/profile"
        class="mt-2 block text-sm font-medium text-primary hover:underline"
      >
        Set in profile
      </router-link>
    </div>
  </div>
</template>
