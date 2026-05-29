<script setup lang="ts">
import { useRoute } from 'vue-router'
import { Home, Activity, TrendingUp, Target, User, type LucideIcon } from 'lucide-vue-next'

interface NavItem {
  icon: LucideIcon
  label: string
  // Navigable items carry a route target + name; inert items omit both.
  to?: string
  routeName?: string
}

const route = useRoute()

const trainItems: NavItem[] = [
  { icon: Home, label: 'Dashboard', to: '/', routeName: 'home' },
  { icon: Activity, label: 'Workouts' },
  { icon: TrendingUp, label: 'Progress' },
  { icon: Target, label: 'Goals' },
]

const accountItems: NavItem[] = [
  { icon: User, label: 'Profile', to: '/profile', routeName: 'profile' },
]

const baseClass = 'flex items-center gap-3 rounded-md px-2 py-2 text-sm'

function isActive(item: NavItem): boolean {
  return item.routeName != null && route.name === item.routeName
}

function itemClass(item: NavItem): string {
  if (isActive(item)) return 'bg-sidebar-accent font-medium text-sidebar-foreground'
  if (item.to) return 'text-sidebar-foreground/50 hover:text-sidebar-foreground'
  return 'text-sidebar-foreground/50'
}
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
        <template v-for="item in trainItems" :key="item.label">
          <RouterLink
            v-if="item.to"
            :to="item.to"
            :class="[baseClass, itemClass(item)]"
          >
            <component :is="item.icon" :size="16" />
            <span>{{ item.label }}</span>
          </RouterLink>
          <div v-else :class="[baseClass, itemClass(item)]">
            <component :is="item.icon" :size="16" />
            <span>{{ item.label }}</span>
          </div>
        </template>
      </nav>
    </div>

    <!-- ACCOUNT section -->
    <div class="mt-6">
      <h2 class="px-2 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
        Account
      </h2>
      <nav class="mt-2 space-y-1">
        <template v-for="item in accountItems" :key="item.label">
          <RouterLink
            v-if="item.to"
            :to="item.to"
            :class="[baseClass, itemClass(item)]"
          >
            <component :is="item.icon" :size="16" />
            <span>{{ item.label }}</span>
          </RouterLink>
          <div v-else :class="[baseClass, itemClass(item)]">
            <component :is="item.icon" :size="16" />
            <span>{{ item.label }}</span>
          </div>
        </template>
      </nav>
    </div>
  </aside>
</template>
