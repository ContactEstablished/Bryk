<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import AppShell from '@/components/layout/AppShell.vue'
import MetricTile from '@/components/common/MetricTile.vue'
import PmcChartSection from '@/components/charts/PmcChartSection.vue'
import LoadChartSection from '@/components/charts/LoadChartSection.vue'
import TimeInZoneSection from '@/components/analytics/TimeInZoneSection.vue'
import PeaksSection from '@/components/analytics/PeaksSection.vue'
import { useAnalyticsStore } from '@/stores/analytics'
import type { PmcRange } from '@/services/analytics'

const route = useRoute()
const router = useRouter()
const store = useAnalyticsStore()
const { progressPmc } = storeToRefs(store)

function updateQuery(patch: Record<string, string | undefined>) {
  void router.replace({ query: { ...route.query, ...patch } })
}

// Query-bound toggles (ADR-0007 §5) — validated, defaulted, written back via router.replace.
const pmcRange = computed<PmcRange>({
  get: () => {
    const v = route.query.pmc
    return v === '6w' || v === '3m' || v === '6m' ? v : '3m'
  },
  set: (v) => updateQuery({ pmc: v }),
})

const weeks = computed<number>({
  get: () => {
    const n = Number(route.query.weeks)
    return Number.isInteger(n) && n >= 1 && n <= 26 ? n : 8
  },
  set: (v) => updateQuery({ weeks: String(v) }),
})

const sport = computed<string>({
  get: () => {
    const v = route.query.sport
    return typeof v === 'string' && ['Bike', 'Run', 'Swim'].includes(v) ? v : ''
  },
  set: (v) => updateQuery({ sport: v || undefined }),
})

const current = computed(() => progressPmc.value?.current ?? null)

// TSB interpretation band (ADR-0006 §8).
const tsbLabel = computed(() => {
  const tsb = current.value?.tsb
  if (tsb == null) return null
  return tsb > 10 ? 'Fresh' : tsb < -10 ? 'Fatigued' : 'Neutral'
})

const acwrInBand = computed(() => {
  const a = current.value?.acwr
  return a != null && a >= 0.8 && a <= 1.3
})
</script>

<template>
  <AppShell title="Progress" subtitle="Fitness, load, intensity & records">
    <!-- Headline metrics (reuse the pmc current summary; "—" for a fresh athlete) -->
    <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <MetricTile label="Fitness · CTL" :value="current?.ctl ?? null" />
      <MetricTile label="Fatigue · ATL" :value="current?.atl ?? null" />
      <MetricTile label="Form · TSB" :value="current?.tsb ?? null" signed>
        <template v-if="tsbLabel" #footer>
          <span class="font-mono text-[11px] text-muted-foreground">{{ tsbLabel }}</span>
        </template>
      </MetricTile>
      <MetricTile label="ACWR" :value="current?.acwr ?? null">
        <template #footer>
          <span
            class="font-mono text-[11px]"
            :class="current?.acwr == null ? 'text-muted-foreground' : acwrInBand ? 'text-good' : 'text-warn'"
          >
            {{ current?.acwr == null ? 'Need 28 days' : acwrInBand ? 'In range 0.8–1.3' : 'Out of range' }}
          </span>
        </template>
      </MetricTile>
    </div>

    <LoadChartSection v-model="weeks" />
    <PmcChartSection v-model="pmcRange" />
    <TimeInZoneSection v-model="sport" />
    <PeaksSection :sport="sport" />
  </AppShell>
</template>
