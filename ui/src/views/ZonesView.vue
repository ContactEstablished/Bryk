<script setup lang="ts">
import { onMounted } from 'vue'
import DashboardSidebar from '@/components/dashboard/DashboardSidebar.vue'
import ZoneSportCard from '@/components/zones/ZoneSportCard.vue'
import { Button } from '@/components/ui/button'
import { useZonesStore } from '@/stores/zones'

const store = useZonesStore()

onMounted(() => {
  void store.loadZones()
})
</script>

<template>
  <!-- Dashboard shell, mirroring ProfileView. -->
  <div class="flex min-h-screen">
    <DashboardSidebar />

    <main class="flex-1 px-10 py-8">
      <header class="border-b border-border pb-6">
        <h1 class="text-2xl font-semibold tracking-tight">Training Zones</h1>
        <p class="mt-1 text-sm text-muted-foreground">
          Computed from your thresholds. Edit a sport's bounds to override, or reset to the computed values.
        </p>
      </header>

      <div class="mt-8 max-w-2xl space-y-8">
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
    </main>
  </div>
</template>
