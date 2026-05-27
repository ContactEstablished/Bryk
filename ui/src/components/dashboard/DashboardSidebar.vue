<script setup lang="ts">
import { Home, Activity, TrendingUp, Target, User, type LucideIcon } from 'lucide-vue-next'

interface NavItem {
  icon: LucideIcon
  label: string
  active: boolean
}

const trainItems: NavItem[] = [
  { icon: Home, label: 'Dashboard', active: true },
  { icon: Activity, label: 'Workouts', active: false },
  { icon: TrendingUp, label: 'Progress', active: false },
  { icon: Target, label: 'Goals', active: false },
]

const accountItems: NavItem[] = [
  { icon: User, label: 'Profile', active: false },
]
</script>

<template>
  <aside class="flex w-56 shrink-0 flex-col border-r border-sidebar-border bg-sidebar px-4 py-6">
    <!-- Brand -->
    <div class="flex items-center gap-2 px-2">
      <div class="flex size-8 items-center justify-center rounded-md bg-sidebar-primary text-sm font-semibold text-sidebar-primary-foreground">
        B
      </div>
      <span class="text-base font-semibold text-sidebar-foreground">Bryk</span>
      <span class="ml-auto rounded border border-sidebar-border px-1.5 py-0.5 text-[10px] font-medium tracking-wider text-muted-foreground">
        PRO
      </span>
    </div>

    <!-- TRAIN section -->
    <div class="mt-10">
      <h2 class="px-2 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        Train
      </h2>
      <nav class="mt-2 space-y-1">
        <div
          v-for="item in trainItems"
          :key="item.label"
          :class="[
            'flex items-center gap-3 rounded-md px-2 py-2 text-sm',
            item.active
              ? 'bg-sidebar-accent font-medium text-sidebar-foreground'
              : 'text-sidebar-foreground/50',
          ]"
        >
          <component :is="item.icon" :size="16" />
          <span>{{ item.label }}</span>
        </div>
      </nav>
    </div>

    <!-- ACCOUNT section -->
    <div class="mt-6">
      <h2 class="px-2 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        Account
      </h2>
      <nav class="mt-2 space-y-1">
        <div
          v-for="item in accountItems"
          :key="item.label"
          class="flex items-center gap-3 rounded-md px-2 py-2 text-sm text-sidebar-foreground/50"
        >
          <component :is="item.icon" :size="16" />
          <span>{{ item.label }}</span>
        </div>
      </nav>
    </div>
  </aside>
</template>
