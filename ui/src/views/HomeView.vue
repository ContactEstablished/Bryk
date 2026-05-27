<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Activity } from 'lucide-vue-next'
import { useOnboardingStore } from '@/stores/onboarding'
import DashboardSidebar from '@/components/dashboard/DashboardSidebar.vue'
import PlaceholderCard from '@/components/dashboard/PlaceholderCard.vue'

const router = useRouter()
const store = useOnboardingStore()

onMounted(() => {
  void store.loadStatus()
})

function goToOnboarding() {
  void router.push('/onboarding')
}

const onboarded = computed(
  () => store.status && store.requiredComplete && store.recommendedComplete && store.goalsComplete,
)

const formattedDate = computed(() => {
  const now = new Date()
  const weekday = now.toLocaleDateString(undefined, { weekday: 'short' })
  const month = now.toLocaleDateString(undefined, { month: 'short' })
  const day = now.getDate()
  const start = new Date(now.getFullYear(), 0, 1)
  const week = Math.ceil(((now.getTime() - start.getTime()) / 86400000 + start.getDay() + 1) / 7)
  return `${weekday}, ${month} ${day} · Week ${week}`
})
</script>

<template>
  <!-- Loading / error / get-started: keep the centered hero layout -->
  <div
    v-if="!onboarded"
    class="flex min-h-screen flex-col items-center justify-center gap-8"
  >
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

    <Button v-else @click="goToOnboarding">
      <Activity />
      Get started
    </Button>
  </div>

  <!-- Dashboard shell -->
  <div v-else class="flex min-h-screen">
    <DashboardSidebar />

    <main class="flex-1 px-10 py-8">
      <header class="border-b border-border pb-6">
        <h1 class="text-2xl font-semibold tracking-tight">Dashboard</h1>
        <p class="mt-1 text-sm text-muted-foreground">{{ formattedDate }}</p>
      </header>

      <!-- Top stat row -->
      <div class="mt-8 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <PlaceholderCard
          title="Weekly Load"
          subtitle="Will populate once workout execution and TSS calculation ship (Phase 9)."
        />
        <PlaceholderCard
          title="Resting HR"
          subtitle="Will show your saved resting HR once the profile editor ships."
        />
        <PlaceholderCard
          title="Sleep Avg"
          subtitle="Post-v1 — needs a device or health-app integration."
        />
        <PlaceholderCard
          title="Form (TSB)"
          subtitle="Will populate with the Performance Management Chart (Phase 11)."
        />
      </div>

      <!-- Middle row: training plan + primary goal -->
      <div class="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div class="lg:col-span-2">
          <PlaceholderCard
            title="This Week"
            subtitle="Your training plan and weekly sessions land with the TrainingPlan domain (Phase 7) and structured workout builder (Phase 8)."
          />
        </div>
        <PlaceholderCard
          title="Primary Goal"
          subtitle="Your highest-priority event will surface here once goal-fetching ships."
        />
      </div>

      <!-- Bottom: recent activity -->
      <div class="mt-6">
        <PlaceholderCard
          title="Recent Activity"
          subtitle="Completed workouts will appear here when execution capture ships (Phase 9)."
        />
      </div>
    </main>
  </div>
</template>
