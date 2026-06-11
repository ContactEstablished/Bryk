<script setup lang="ts">
import AppShell from '@/components/layout/AppShell.vue'
import ProfileRequiredSection from '@/components/profile/ProfileRequiredSection.vue'
import ProfileRecommendedSection from '@/components/profile/ProfileRecommendedSection.vue'
import ProfileGoalsSection from '@/components/profile/ProfileGoalsSection.vue'
</script>

<template>
  <AppShell title="Profile" subtitle="View and edit your athlete profile.">
    <div class="max-w-2xl space-y-8">
      <ProfileRequiredSection />
      <ProfileRecommendedSection />
      <ProfileGoalsSection />
    </div>
  </AppShell>
</template>
