<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Activity } from 'lucide-vue-next'
import { useOnboardingStore } from '@/stores/onboarding'
import AppShell from '@/components/layout/AppShell.vue'
import PlaceholderCard from '@/components/dashboard/PlaceholderCard.vue'
import PrimaryGoalCard from '@/components/dashboard/PrimaryGoalCard.vue'
import RestingHrCard from '@/components/dashboard/RestingHrCard.vue'
import ThisWeekCard from '@/components/dashboard/ThisWeekCard.vue'
import WeeklyLoadCard from '@/components/dashboard/WeeklyLoadCard.vue'
import FormCard from '@/components/dashboard/FormCard.vue'
import RecentActivityCard from '@/components/dashboard/RecentActivityCard.vue'

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
    <div class="flex flex-col items-center gap-5">
      <div
        class="flex size-12 items-center justify-center rounded-xl bg-gradient-to-br from-primary-hi to-primary-lo text-xl font-extrabold tracking-[-0.04em] text-primary-foreground shadow-[0_0_0_1px_oklch(0.68_0.19_250_/_0.4),0_10px_30px_var(--bryk-accent-glow)]"
      >
        B
      </div>
      <div class="flex flex-col items-center gap-2">
        <h1 class="text-6xl font-bold tracking-[-0.04em] text-foreground">Bryk</h1>
        <p class="eyebrow">Performance Training</p>
      </div>
    </div>

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
  <AppShell v-else title="Dashboard" :subtitle="formattedDate">
    <!-- Top stat row -->
    <div class="stagger-in grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <WeeklyLoadCard />
      <RestingHrCard />
      <PlaceholderCard
        title="Sleep Avg"
        subtitle="Post-v1 — needs a device or health-app integration."
      />
      <FormCard />
    </div>

    <!-- Middle row: training plan + primary goal -->
    <div class="stagger-in grid grid-cols-1 gap-6 lg:grid-cols-3">
      <div class="lg:col-span-2">
        <ThisWeekCard />
      </div>
      <PrimaryGoalCard />
    </div>

    <!-- Bottom: recent activity -->
    <div class="stagger-in">
      <RecentActivityCard />
    </div>
  </AppShell>
</template>
