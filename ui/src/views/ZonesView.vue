<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import AppShell from '@/components/layout/AppShell.vue'
import ZoneSportCard from '@/components/zones/ZoneSportCard.vue'
import { Button } from '@/components/ui/button'
import { useZonesStore } from '@/stores/zones'

const store = useZonesStore()

onMounted(() => {
  void store.loadZones()
})

// Fixed display order; anything unexpected sorts after.
const sportOrder: Record<string, number> = { Swim: 0, Bike: 1, Run: 2 }

const sortedSports = computed(() =>
  [...(store.zones?.sports ?? [])].sort(
    (a, b) => (sportOrder[a.sport] ?? 99) - (sportOrder[b.sport] ?? 99),
  ),
)

const activeSport = ref<string | null>(null)

watch(
  sortedSports,
  (sports) => {
    if (!sports.some((s) => s.sport === activeSport.value)) {
      activeSport.value = sports[0]?.sport ?? null
    }
  },
  { immediate: true },
)
</script>

<template>
  <AppShell title="Training Zones" subtitle="Computed from your thresholds">
    <div class="max-w-2xl space-y-6">
      <p class="text-sm text-muted-foreground">
        Edit a sport's bounds to override, or reset to the computed values.
      </p>

      <template v-if="store.zones">
        <template v-if="sortedSports.length > 0">
          <div
            role="tablist"
            aria-label="Sport"
            class="inline-flex gap-0.5 rounded-[10px] border border-border-strong bg-[#0d1015] p-0.5"
          >
            <button
              v-for="sport in sortedSports"
              :key="sport.sport"
              type="button"
              role="tab"
              :aria-selected="sport.sport === activeSport"
              class="rounded-lg px-4 py-1.5 text-[13px] font-medium transition-colors duration-[120ms]"
              :class="
                sport.sport === activeSport
                  ? 'bg-raised text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]'
                  : 'text-subtle hover:text-foreground'
              "
              @click="activeSport = sport.sport"
            >
              {{ sport.sport }}
            </button>
          </div>

          <!-- v-show (not v-if) keeps unsaved edits alive when switching tabs. -->
          <ZoneSportCard
            v-for="sport in sortedSports"
            v-show="sport.sport === activeSport"
            :key="sport.sport"
            :sport="sport"
          />
        </template>

        <div
          v-else
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
