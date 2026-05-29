<script setup lang="ts">
import DashboardSidebar from '@/components/dashboard/DashboardSidebar.vue'
import ProfileRequiredSection from '@/components/profile/ProfileRequiredSection.vue'
import ProfileRecommendedSection from '@/components/profile/ProfileRecommendedSection.vue'
import ProfileGoalsSection from '@/components/profile/ProfileGoalsSection.vue'
</script>

<template>
  <!-- Dashboard shell, duplicated from HomeView (Decision 3 — no extracted layout yet). -->
  <div class="flex min-h-screen">
    <DashboardSidebar />

    <main class="flex-1 px-10 py-8">
      <header class="border-b border-border pb-6">
        <h1 class="text-2xl font-semibold tracking-tight">Profile</h1>
        <p class="mt-1 text-sm text-muted-foreground">View and edit your athlete profile.</p>
      </header>

      <div class="mt-8 max-w-2xl space-y-8">
        <ProfileRequiredSection />
        <ProfileRecommendedSection />
        <ProfileGoalsSection />
      </div>
    </main>
  </div>
</template>
