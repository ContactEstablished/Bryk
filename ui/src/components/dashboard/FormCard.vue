<script setup lang="ts">
import { computed, onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useAnalyticsStore } from '@/stores/analytics'

const store = useAnalyticsStore()

onMounted(() => {
  if (!store.pmc) void store.loadPmc()
})

const tsb = computed(() => store.current?.tsb ?? null)

// ADR-0006 §8 bands: > +10 Fresh / −10..+10 Neutral / < −10 Fatigued. Null when there is no current.
const interpretation = computed(() => {
  const v = tsb.value
  if (v == null) return null
  if (v > 10) return 'Fresh'
  if (v < -10) return 'Fatigued'
  return 'Neutral'
})
</script>

<template>
  <MetricTile
    label="Form (TSB)"
    :value="tsb"
    :signed="true"
    :delta="store.tsbDeltaVs7d"
    :loading="store.loading && !store.pmc"
  >
    <template #footer>
      <p v-if="interpretation" class="text-xs text-muted-foreground">{{ interpretation }}</p>
      <!-- Fresh athlete (no history): no fabricated form number — point them at logging a workout. -->
      <p v-else-if="store.pmc" class="text-xs text-muted-foreground">Log a workout to see your form</p>
    </template>
  </MetricTile>
</template>
