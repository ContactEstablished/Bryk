<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Activity } from 'lucide-vue-next'
import { useOnboardingStore } from '@/stores/onboarding'

const router = useRouter()
const store = useOnboardingStore()

onMounted(() => {
  void store.loadStatus()
})

function goToOnboarding() {
  void router.push('/onboarding')
}
</script>

<template>
  <div class="flex min-h-screen flex-col items-center justify-center gap-8">
    <h1 class="text-6xl font-bold text-white">Bryk</h1>

    <p
      v-if="store.loadingStatus"
      class="text-lg text-muted-foreground"
    >
      Loading…
    </p>

    <div
      v-else-if="store.error && !store.status"
      class="max-w-lg text-center"
    >
      <p class="text-destructive">
        Couldn't load onboarding status — check the API and refresh.
      </p>
      <Button class="mt-4" variant="outline" @click="store.loadStatus()">
        Retry
      </Button>
    </div>

    <p
      v-else-if="store.status && store.requiredComplete && store.recommendedComplete && store.goalsComplete"
      class="text-xl font-semibold text-white"
    >
      Welcome back
    </p>

    <Button v-else @click="goToOnboarding">
      <Activity />
      Get started
    </Button>
  </div>
</template>
