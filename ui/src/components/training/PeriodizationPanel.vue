<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { CheckCircle2 } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import LoadChart from '@/components/charts/LoadChart.vue'
import { useTrainingStore } from '@/stores/training'
import { useGoalsStore } from '@/stores/goals'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { planMetadataSchema, type PlanMetadataFormValues } from '@/schemas/training'
import type { TrainingPlanResponse, TrainingPlanUpdateRequest } from '@/types/training'
import type { WeeklyLoadWeek } from '@/types/analytics'

// The plan-metadata surface (Task 18-4). This is the only place buildWeeks / recoveryWeeks /
// recoveryWeekPercentage are writable — they have existed since ADR-0003 and no UI has ever set them
// — and the only place the plan↔event link is editable (Phase 17 shipped it display-only).
const props = defineProps<{ plan: TrainingPlanResponse }>()

const store = useTrainingStore()
const goalsStore = useGoalsStore()

const editing = ref(false)
const globalError = ref<string | null>(null)
const justSaved = ref(false)

onMounted(() => {
  void store.loadWeeklyTargets(props.plan.id)
  if (!goalsStore.events) void goalsStore.loadAll()
})

const methodologyOptions = [
  { value: 'Pyramidal', label: 'Pyramidal' },
  { value: 'Periodization', label: 'Periodization' },
  { value: 'Polarized', label: 'Polarized' },
  { value: 'Norwegian', label: 'Norwegian' },
]

const events = computed(() => goalsStore.events ?? [])

// reka-ui rejects a SelectItem whose value is '' (it reserves the empty string for "cleared, show the
// placeholder"). An edit surface still needs an explicit way to UNlink a plan, so the clear option
// carries a sentinel; both it and '' map to null on submit. TrainingView's create form has no clear
// option at all — there is nothing to clear when creating.
const NO_EVENT = '__none__'

// Looked up across ALL events (not upcomingEvents) so a plan linked to a past race still resolves.
const linkedEvent = computed(() =>
  props.plan.eventId ? events.value.find((e) => e.id === props.plan.eventId) ?? null : null,
)

const cadenceLine = computed(() => {
  const { buildWeeks, recoveryWeeks, recoveryWeekPercentage } = props.plan
  if (buildWeeks == null || recoveryWeeks == null || recoveryWeekPercentage == null) {
    return 'No cadence set'
  }
  return `${buildWeeks} build : ${recoveryWeeks} recovery · ${recoveryWeekPercentage}% recovery volume`
})

function fromPlan(p: TrainingPlanResponse): PlanMetadataFormValues {
  return {
    name: p.name,
    methodology: p.methodology,
    startDate: p.startDate,
    endDate: p.endDate,
    eventId: p.eventId ?? '',
    buildWeeks: p.buildWeeks,
    recoveryWeeks: p.recoveryWeeks,
    recoveryWeekPercentage: p.recoveryWeekPercentage,
  } as PlanMetadataFormValues
}

const form = useForm<PlanMetadataFormValues>({
  validationSchema: toTypedSchema(planMetadataSchema),
  initialValues: fromPlan(props.plan),
})

function setError(e: unknown) {
  // The server's PlanWindow: / EventId: text is the most useful thing the athlete can read — it names
  // the stranded count and the date range. Surface it verbatim; never rewrite or shorten it.
  const messages = extractApiValidationMessages(e)
  if (messages) {
    globalError.value = messages.join(' ')
  } else if (e instanceof ApiError) {
    globalError.value = e.status === 404
      ? 'This plan no longer exists — it may have been removed.'
      : `Couldn't save: ${e.statusText} (${e.status})`
  } else if (e instanceof Error) {
    globalError.value = `Couldn't save: ${e.message}`
  } else {
    globalError.value = "Couldn't save — please try again."
  }
}

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const req: TrainingPlanUpdateRequest = {
    name: values.name,
    methodology: values.methodology,
    startDate: values.startDate,
    endDate: values.endDate,
    eventId: values.eventId && values.eventId !== NO_EVENT ? values.eventId : null,
    buildWeeks: values.buildWeeks ?? null,
    recoveryWeeks: values.recoveryWeeks ?? null,
    recoveryWeekPercentage: values.recoveryWeekPercentage ?? null,
  }
  try {
    await store.updatePlan(props.plan.id, req)
    form.resetForm({ values })
    justSaved.value = true
    editing.value = false
  } catch (e) {
    setError(e)
  }
})

function toggleEdit() {
  if (editing.value) {
    editing.value = false
    globalError.value = null
    return
  }
  form.resetForm({ values: fromPlan(props.plan) })
  globalError.value = null
  editing.value = true
}

watch(
  () => form.meta.value.dirty,
  (dirty) => {
    if (dirty) justSaved.value = false
  },
)

const isSubmitting = form.isSubmitting

// ── Target ramp ─────────────
const targets = computed(() => store.weeklyTargets)
const targetWeeks = computed(() => targets.value?.weeks ?? [])

// Adapter onto the Phase-15 LoadChart, consumed unchanged: targets take the hatched "planned"
// channel, the athlete's real weeks take the filled "actual" channel, and the dashed trend traces
// the ramp itself. optimalBand is null — the plan's own targets replace the ACWR band here.
// Known cosmetic artifact: lib/charts/load.ts:65 labels the LAST bar "· NOW", which in this context
// is the plan's final week rather than the current week. Forking the chart is out of scope.
const chartWeeks = computed<WeeklyLoadWeek[]>(() =>
  targetWeeks.value.map((w) => ({
    weekStart: w.weekStart,
    plannedLoad: w.targetLoad,
    actualLoad: w.actualLoad,
    rollingAverage: w.targetLoad,
  })),
)

const baselineLabel = computed(() => {
  const t = targets.value
  if (!t || t.baseline == null) return null
  const source =
    t.baselineSource === 'TrailingActual' ? 'your last 4 weeks' : "this plan's first week"
  return `Ramping from ${t.baseline} TSS/wk · ${source}`
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

function formatShortDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  })
}
</script>

<template>
  <div class="space-y-5">
    <!-- Plan metadata: read summary + the edit form -->
    <div class="card-surface p-6">
      <div class="flex items-start justify-between gap-4">
        <div class="min-w-0">
          <h2 class="text-lg font-semibold">{{ plan.name }}</h2>
          <p class="mt-1 flex flex-wrap gap-x-3 font-mono text-[12px] text-muted-foreground">
            <span>{{ plan.methodology }}</span>
            <span>{{ formatDay(plan.startDate) }} – {{ formatDay(plan.endDate) }}</span>
            <span>{{ linkedEvent ? linkedEvent.name : 'No target event' }}</span>
          </p>
          <p class="mt-1 font-mono text-[12px] text-muted-foreground">{{ cadenceLine }}</p>
        </div>
        <div class="flex shrink-0 items-center gap-3">
          <span v-if="justSaved && !editing" class="flex items-center gap-1 text-sm text-muted-foreground">
            <CheckCircle2 :size="16" class="text-primary" />
            Saved
          </span>
          <Button type="button" variant="outline" size="sm" @click="toggleEdit">
            {{ editing ? 'Cancel' : 'Edit' }}
          </Button>
        </div>
      </div>

      <form v-if="editing" class="mt-5 space-y-4 border-t border-border pt-5" @submit="onSubmit">
        <FormField v-slot="{ componentField }" name="name">
          <FormItem>
            <FormLabel>Plan name</FormLabel>
            <FormControl>
              <Input v-bind="componentField" />
            </FormControl>
            <FormMessage />
          </FormItem>
        </FormField>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <FormField v-slot="{ componentField }" name="methodology">
            <FormItem>
              <FormLabel>Methodology</FormLabel>
              <Select v-bind="componentField">
                <FormControl>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  <SelectItem v-for="opt in methodologyOptions" :key="opt.value" :value="opt.value">
                    {{ opt.label }}
                  </SelectItem>
                </SelectContent>
              </Select>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField v-slot="{ componentField }" name="eventId">
            <FormItem>
              <FormLabel>Target event</FormLabel>
              <Select v-bind="componentField">
                <FormControl>
                  <SelectTrigger>
                    <SelectValue placeholder="No target event" />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  <SelectItem :value="NO_EVENT">No target event</SelectItem>
                  <SelectItem v-for="e in events" :key="e.id" :value="e.id">
                    {{ e.name }}
                  </SelectItem>
                </SelectContent>
              </Select>
              <FormMessage />
            </FormItem>
          </FormField>
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <FormField v-slot="{ componentField }" name="startDate">
            <FormItem>
              <FormLabel>Start date</FormLabel>
              <FormControl>
                <Input type="date" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField v-slot="{ componentField }" name="endDate">
            <FormItem>
              <FormLabel>End date</FormLabel>
              <FormControl>
                <Input type="date" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <FormField v-slot="{ componentField }" name="buildWeeks">
            <FormItem>
              <FormLabel>Build weeks</FormLabel>
              <FormControl>
                <Input type="number" min="1" max="8" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField v-slot="{ componentField }" name="recoveryWeeks">
            <FormItem>
              <FormLabel>Recovery weeks</FormLabel>
              <FormControl>
                <Input type="number" min="1" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField v-slot="{ componentField }" name="recoveryWeekPercentage">
            <FormItem>
              <FormLabel>Recovery volume (% of a build week)</FormLabel>
              <FormControl>
                <Input type="number" min="30" max="90" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>
        </div>

        <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {{ globalError }}
        </div>

        <div class="flex items-center justify-end gap-3">
          <Button type="submit" :disabled="isSubmitting">Save</Button>
        </div>
      </form>
    </div>

    <!-- Target ramp -->
    <section class="card-surface flex flex-col gap-4 p-5">
      <header class="flex flex-col">
        <h2 class="text-[15px] font-semibold tracking-[-0.02em] text-foreground">Weekly target ramp</h2>
        <span class="eyebrow text-faint">{{ baselineLabel ?? 'Computed on read · TSS' }}</span>
      </header>

      <p v-if="store.loadingTargets && !targets" class="py-10 text-center text-sm text-muted-foreground">
        Loading…
      </p>

      <p v-else-if="targetWeeks.length === 0" class="py-10 text-center text-sm text-muted-foreground">
        No targets yet — log four weeks of training or plan your first week, and the ramp appears.
      </p>

      <template v-else>
        <LoadChart :weeks="chartWeeks" :optimal-band="null" />

        <ul class="flex flex-wrap gap-2 text-[11px] text-muted-foreground">
          <li class="flex items-center gap-1.5">
            <span class="inline-block size-2.5 rounded-[2px] bg-muted-foreground/40" aria-hidden="true" />
            Target
          </li>
          <li class="flex items-center gap-1.5">
            <span class="inline-block size-2.5 rounded-[2px] bg-primary" aria-hidden="true" />
            Actual
          </li>
          <li class="flex items-center gap-1.5">
            <span class="inline-block size-2.5 rounded-[2px] bg-warn" aria-hidden="true" />
            Ramp
          </li>
        </ul>

        <!-- The accessible rendering of the cadence: the SVG above is aria-hidden. -->
        <ul class="divide-y divide-border border-t border-border">
          <li
            v-for="w in targetWeeks"
            :key="w.weekStart"
            class="flex items-center justify-between gap-3 py-2 font-mono text-[12px]"
          >
            <span class="text-muted-foreground">{{ formatShortDay(w.weekStart) }}</span>
            <span class="flex items-center gap-2">
              <span class="text-primary-hi">{{ w.targetLoad }} TSS</span>
              <span v-if="w.isTaperWeek" class="rounded-sm bg-warn/15 px-1.5 py-0.5 text-[10px] text-warn">
                Taper
              </span>
              <span
                v-else-if="w.isRecoveryWeek"
                class="rounded-sm bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground"
              >
                Recovery
              </span>
            </span>
          </li>
        </ul>
      </template>
    </section>
  </div>
</template>
