<script setup lang="ts">
import AppSidebar from '@/components/layout/AppSidebar.vue'

defineProps<{
  title: string
  subtitle?: string
}>()
</script>

<template>
  <div class="min-h-screen md:grid md:grid-cols-[232px_1fr]">
    <AppSidebar />

    <main class="flex min-w-0 flex-col">
      <div
        class="sticky top-0 z-30 flex items-center gap-5 border-b border-border bg-background/80 px-4 py-3 backdrop-blur-xl backdrop-saturate-150 md:px-8 md:py-4"
      >
        <div>
          <h1 class="text-[21px] font-bold tracking-[-0.025em]">{{ title }}</h1>
          <p v-if="subtitle" class="font-mono text-xs text-muted-foreground">{{ subtitle }}</p>
        </div>
        <div class="flex-1" />
        <slot name="actions" />
      </div>

      <!-- pb-24 keeps content clear of the fixed mobile tab bar. -->
      <div class="mx-auto flex w-full max-w-[1440px] flex-1 flex-col gap-6 p-4 pb-24 md:p-8">
        <slot />
      </div>
    </main>
  </div>
</template>
