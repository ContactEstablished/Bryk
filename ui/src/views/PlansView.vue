<script setup lang="ts">
import { onMounted } from 'vue'
import { CalendarRange, Plus } from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
import { Button } from '@/components/ui/button'
import { useTrainingStore } from '@/stores/training'

const store = useTrainingStore()

onMounted(() => {
  void store.loadPlans()
})

function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
}
</script>

<template>
  <AppShell title="Training Plans" subtitle="Browse your plans and edit workout structure.">
    <div class="mx-auto w-full max-w-3xl space-y-5">
      <div class="flex justify-end">
        <RouterLink to="/training">
          <Button type="button" size="sm">
            <Plus :size="14" /> New plan
          </Button>
        </RouterLink>
      </div>

      <div class="card-surface">
        <p v-if="!store.plans" class="px-6 py-4 text-sm text-muted-foreground">Loading…</p>

        <template v-else>
          <ul v-if="store.plans.length > 0">
            <li
              v-for="(plan, i) in store.plans"
              :key="plan.id"
              :class="i > 0 ? 'border-t border-border' : ''"
            >
              <RouterLink
                :to="`/plans/${plan.id}`"
                class="grid grid-cols-[32px_1fr_auto] items-center gap-4 px-6 py-4 transition-colors duration-[120ms] hover:bg-muted"
              >
                <div
                  class="flex size-8 items-center justify-center rounded-lg border border-border-strong bg-raised text-subtle"
                >
                  <CalendarRange :size="16" />
                </div>
                <div class="min-w-0">
                  <p class="truncate text-sm font-semibold">{{ plan.name }}</p>
                  <p class="mt-0.5 font-mono text-[11px] text-muted-foreground">{{ plan.methodology }}</p>
                </div>
                <span class="font-mono text-[11px] text-muted-foreground">
                  {{ formatDay(plan.startDate) }} – {{ formatDay(plan.endDate) }}
                </span>
              </RouterLink>
            </li>
          </ul>

          <div v-else class="flex flex-col items-center gap-3 px-6 py-10 text-center">
            <p class="text-sm text-muted-foreground">No plans yet.</p>
            <RouterLink to="/training">
              <Button type="button" variant="outline" size="sm">
                <Plus :size="14" /> Create your first plan
              </Button>
            </RouterLink>
          </div>
        </template>
      </div>
    </div>
  </AppShell>
</template>
