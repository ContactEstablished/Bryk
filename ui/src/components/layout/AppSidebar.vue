<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import {
  Home,
  Activity,
  TrendingUp,
  Target,
  User,
  Gauge,
  CalendarRange,
  type LucideIcon,
} from 'lucide-vue-next'
import { useProfileStore } from '@/stores/profile'

interface NavItem {
  icon: LucideIcon
  label: string
  // Navigable items carry a route target + name; inert items omit both.
  to?: string
  routeName?: string
}

const route = useRoute()
const profileStore = useProfileStore()

onMounted(() => {
  if (!profileStore.required) void profileStore.loadRequired()
})

const trainItems: NavItem[] = [
  { icon: Home, label: 'Dashboard', to: '/', routeName: 'home' },
  { icon: Gauge, label: 'Zones', to: '/zones', routeName: 'zones' },
  { icon: CalendarRange, label: 'Training', to: '/training', routeName: 'training' },
  { icon: Activity, label: 'Workouts', to: '/workouts', routeName: 'workouts' },
  { icon: TrendingUp, label: 'Progress' },
  { icon: Target, label: 'Goals' },
]

const accountItems: NavItem[] = [
  { icon: User, label: 'Profile', to: '/profile', routeName: 'profile' },
]

// The mobile tab bar only shows navigable destinations.
const mobileItems = [...trainItems, ...accountItems].filter((i) => i.to != null)

const initials = computed(() => {
  const name = profileStore.required?.name?.trim()
  if (!name) return '—'
  const parts = name.split(/\s+/)
  const first = parts[0]?.[0] ?? ''
  const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : ''
  return (first + last).toUpperCase()
})

function isActive(item: NavItem): boolean {
  return item.routeName != null && route.name === item.routeName
}

const itemBase =
  'group relative flex w-full items-center gap-3 rounded-[10px] border border-transparent px-3 py-2 text-sm font-medium transition-colors duration-[120ms]'

function itemClass(item: NavItem): string {
  if (isActive(item))
    return 'bg-[linear-gradient(180deg,#181c24,#14181f)] border-border-strong text-foreground'
  if (item.to) return 'text-subtle hover:bg-muted hover:text-foreground'
  return 'text-faint'
}

function iconClass(item: NavItem): string {
  return isActive(item) ? 'text-primary-hi' : 'text-muted-foreground'
}
</script>

<template>
  <!-- Desktop sidebar -->
  <aside
    class="sticky top-0 hidden h-screen flex-col gap-0.5 border-r border-sidebar-border bg-[linear-gradient(180deg,#0c0f14_0%,#090b10_100%)] px-3 py-5 md:flex"
  >
    <!-- Brand -->
    <div class="flex items-center gap-3 px-3 pb-4">
      <div
        class="flex size-[30px] items-center justify-center rounded-lg bg-gradient-to-br from-primary-hi to-primary-lo text-sm font-extrabold tracking-[-0.04em] text-primary-foreground shadow-[0_0_0_1px_oklch(0.68_0.19_250_/_0.4),0_6px_20px_var(--bryk-accent-glow)]"
      >
        B
      </div>
      <span class="text-[17px] font-bold tracking-[-0.03em] text-foreground">Bryk</span>
      <span
        class="ml-auto rounded border border-border-strong px-1.5 py-0.5 font-mono text-[10px] uppercase tracking-[0.08em] text-muted-foreground"
      >
        PRO
      </span>
    </div>

    <!-- TRAIN section -->
    <h2 class="eyebrow px-3 pt-3 pb-1 text-faint">Train</h2>
    <nav class="flex flex-col gap-0.5">
      <template v-for="item in trainItems" :key="item.label">
        <RouterLink
          v-if="item.to"
          :to="item.to"
          :class="[itemBase, itemClass(item)]"
          :aria-current="isActive(item) ? 'page' : undefined"
        >
          <span
            class="absolute left-0 top-1/2 h-4 w-0.5 -translate-y-1/2 rounded-full bg-primary-hi transition-[transform,opacity] duration-200"
            :class="isActive(item) ? 'scale-y-100 opacity-100' : 'scale-y-0 opacity-0'"
          />
          <component :is="item.icon" :size="16" :class="iconClass(item)" />
          <span>{{ item.label }}</span>
        </RouterLink>
        <div v-else :class="[itemBase, itemClass(item)]">
          <component :is="item.icon" :size="16" class="text-faint" />
          <span>{{ item.label }}</span>
          <span
            class="ml-auto rounded border border-border px-1 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-faint"
          >
            soon
          </span>
        </div>
      </template>
    </nav>

    <!-- ACCOUNT section -->
    <h2 class="eyebrow px-3 pt-3 pb-1 text-faint">Account</h2>
    <nav class="flex flex-col gap-0.5">
      <RouterLink
        v-for="item in accountItems"
        :key="item.label"
        :to="item.to!"
        :class="[itemBase, itemClass(item)]"
        :aria-current="isActive(item) ? 'page' : undefined"
      >
        <span
          class="absolute left-0 top-1/2 h-4 w-0.5 -translate-y-1/2 rounded-full bg-primary-hi transition-[transform,opacity] duration-200"
          :class="isActive(item) ? 'scale-y-100 opacity-100' : 'scale-y-0 opacity-0'"
        />
        <component :is="item.icon" :size="16" :class="iconClass(item)" />
        <span>{{ item.label }}</span>
      </RouterLink>
    </nav>

    <!-- Athlete footer -->
    <div class="mt-auto flex items-center gap-3 border-t border-sidebar-border px-3 pt-3">
      <div
        class="flex size-8 items-center justify-center rounded-full border border-border-strong bg-gradient-to-br from-[#2a3340] to-[#161a22] text-[11px] font-bold text-subtle"
      >
        {{ initials }}
      </div>
      <div class="flex min-w-0 flex-col">
        <span class="truncate text-[13px] font-semibold text-foreground">
          {{ profileStore.required?.name ?? 'Athlete' }}
        </span>
        <span class="text-[11px] text-muted-foreground">
          {{ profileStore.required?.methodology ?? 'Athlete' }}
        </span>
      </div>
    </div>
  </aside>

  <!-- Mobile bottom tab bar -->
  <nav
    class="fixed inset-x-0 bottom-0 z-40 flex justify-around border-t border-border bg-background/90 px-2 py-2 backdrop-blur-xl md:hidden"
  >
    <RouterLink
      v-for="item in mobileItems"
      :key="item.label"
      :to="item.to!"
      class="flex flex-col items-center gap-0.5 rounded-lg px-3 py-1 text-[10px] font-medium transition-colors duration-[120ms]"
      :class="isActive(item) ? 'text-primary-hi' : 'text-muted-foreground'"
      :aria-current="isActive(item) ? 'page' : undefined"
    >
      <component :is="item.icon" :size="18" />
      <span>{{ item.label }}</span>
    </RouterLink>
  </nav>
</template>
