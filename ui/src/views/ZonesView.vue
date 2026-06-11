<script setup lang="ts">
import { onMounted } from 'vue'
import AppShell from '@/components/layout/AppShell.vue'
import ZoneSportCard from '@/components/zones/ZoneSportCard.vue'
import { Button } from '@/components/ui/button'
import { useZonesStore } from '@/stores/zones'

const store = useZonesStore()

onMounted(() => {
  void store.loadZones()
})
</script>

<template>
  <AppShell title="Training Zones" subtitle="Computed from your thresholds">
    <div class="max-w-2xl space-y-8">
      <p class="text-sm text-muted-foreground">
        Edit a sport's bounds to override, or reset to the computed values.
      </p>

      <template v-if="store.zones">
        <ZoneSportCard
          v-for="sport in store.zones.sports"
          :key="sport.sport"
          :sport="sport"
        />

        <div
          v-if="store.zones.sports.length === 0"
          class="rounded-lg border bg-card p-6 text-sm text-muted-foreground"
        >
          <p>No training zones yet — set your sport thresholds to see computed zones.</p>
          <RouterLink to="/profile">
            <Button class="mt-3" variant="outline">Go to profile</Button>
          </RouterLink>
        </div>
      </template>

      <div v-else-if="store.error" class="rounded-lg border bg-card p-6">
        <p class="text-sm text-destructive">Couldn't load your training zones.</p>
        <Button class="mt-3" variant="outline" @click="store.loadZones()">Retry</Button>
      </div>

      <p v-else class="text-sm text-muted-foreground">Loading…</p>
    </div>
  </AppShell>
</template>
