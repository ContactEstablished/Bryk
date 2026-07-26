<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useProfileStore } from '@/stores/profile'
import { useWellnessStore } from '@/stores/wellness'
import { invertedChange, metricSeries } from '@/lib/wellness'

const profile = useProfileStore()
const wellness = useWellnessStore()

onMounted(() => {
  if (!profile.recommended) void profile.loadRecommended()
  if (!wellness.summary) void wellness.loadSummary()
})

// Logged history first, whole bpm (a number, so MetricTile's count-up animates it).
const wellnessAvg = computed(() => {
  const average = wellness.summary?.restingHr.average
  return average == null ? null : Math.round(average)
})

// ADR-0011 §1's read-only fallback: prefer logged history, fall back to the onboarding value so a
// tile that has shipped since Phase 14 never regresses to "—" for an athlete who has not started
// logging. This is a READ. The card writes nothing back to the profile, and a wellness save never
// touches Athlete.RestingHr — the two sources stay independent.
const value = computed(() => wellnessAvg.value ?? profile.recommended?.restingHr ?? null)

const spark = computed(() => metricSeries(wellness.summary?.days ?? [], 'restingHr'))

// No `delta` prop anywhere on this tile: resting HR is inverted (ADR-0011 §5). A DROP is good news, and
// DeltaChip colours `down` red by documented convention (lib/weeklyTarget.ts:21-23). The 7-day change
// goes in the footer instead, coloured by invertedChange.
const change = computed(() =>
  wellnessAvg.value == null ? null : invertedChange(wellness.summary?.restingHr.delta, 'bpm', 0),
)

// Fetched, but neither a logged average nor an onboarding value.
const unset = computed(
  () =>
    wellnessAvg.value == null &&
    profile.recommended != null &&
    profile.recommended.restingHr == null,
)
</script>

<template>
  <MetricTile
    label="Resting HR"
    :value="value"
    unit="bpm"
    :spark="spark"
    :loading="!profile.recommended && !wellness.summary"
  >
    <template #footer>
      <!-- 1. Logged average with a prior week to compare against. -->
      <p v-if="change" class="font-mono text-[11px]" :class="change.className">{{ change.text }}</p>
      <!-- 2. Logged average, no prior-week data yet — no fabricated trend. -->
      <p v-else-if="wellnessAvg != null" class="text-xs text-muted-foreground">7-day average</p>
      <!-- 3. Falling back to the onboarding value (ADR-0011 §1) — say so, and point at logging. -->
      <p v-else-if="profile.recommended?.restingHr != null" class="text-xs text-muted-foreground">
        From profile · log RHR to see a trend
      </p>
      <!-- 4. Empty state: fetched but unset — point at the profile editor. -->
      <router-link
        v-else-if="unset"
        to="/profile"
        class="text-sm font-medium text-primary-hi hover:underline"
      >Set in profile</router-link>
    </template>
  </MetricTile>
</template>
