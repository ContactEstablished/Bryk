<script setup lang="ts">
import { onMounted } from 'vue'
import MetricTile from '@/components/common/MetricTile.vue'
import { useProfileStore } from '@/stores/profile'

const store = useProfileStore()

onMounted(() => {
  if (!store.recommended) void store.loadRecommended()
})
</script>

<template>
  <MetricTile
    label="Resting HR"
    :value="store.recommended?.restingHr ?? null"
    unit="bpm"
    :loading="!store.recommended"
  >
    <!-- Empty state: fetched but unset — point at the profile editor. -->
    <template v-if="store.recommended && store.recommended.restingHr == null" #footer>
      <router-link
        to="/profile"
        class="text-sm font-medium text-primary-hi hover:underline"
      >Set in profile</router-link>
    </template>
  </MetricTile>
</template>
