<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useFieldArray, useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { CheckCircle2 } from 'lucide-vue-next'
import AppShell from '@/components/layout/AppShell.vue'
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
import WorkoutStructureBuilder from '@/components/training/WorkoutStructureBuilder.vue'
import { useProfileStore } from '@/stores/profile'
import { useTrainingStore } from '@/stores/training'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { trainingPlanSchema, type TrainingPlanFormValues } from '@/schemas/training'
import type {
  TrainingPlanRequest,
  TrainingPlanResponse,
  PlannedWorkoutResponse,
  PlannedSport,
} from '@/types/training'

const profileStore = useProfileStore()
const trainingStore = useTrainingStore()

// Planned workouts add Strength on top of the cardio sports (ADR-0003 §4).
type PlannedSportValue = 'Swim' | 'Bike' | 'Run' | 'Triathlon' | 'Strength'

interface PlannedWorkoutFormItem {
  sport: PlannedSportValue
  scheduledDate: string
  title: string
  plannedDurationMinutes: number | null
  plannedLoad: number | null
}

const form = useForm<TrainingPlanFormValues>({
  validationSchema: toTypedSchema(trainingPlanSchema),
  initialValues: {
    name: '',
    methodology: 'Polarized',
    startDate: '',
    endDate: '',
    eventId: '',
    plannedWorkouts: [],
  },
})

const { fields, push, remove } = useFieldArray<PlannedWorkoutFormItem>('plannedWorkouts')

const globalError = ref<string | null>(null)
const justCreated = ref(false)

// After a plan is created its planned workouts have ids, so the builder can be launched for each.
const createdPlan = ref<TrainingPlanResponse | null>(null)
const buildTarget = ref<{
  planId: string
  plannedWorkoutId: string
  sport: PlannedSport
  title: string
} | null>(null)

function openBuilder(pw: PlannedWorkoutResponse) {
  if (!createdPlan.value) return
  buildTarget.value = {
    planId: createdPlan.value.id,
    plannedWorkoutId: pw.id,
    sport: pw.sport,
    title: pw.title,
  }
}

onMounted(() => {
  if (!profileStore.required) void profileStore.loadRequired()
  if (!profileStore.goals) void profileStore.loadGoals()
})

// Seed the methodology select from the athlete's onboarding default once their profile
// loads, unless the user has already touched the form (ADR-0003 decision 3).
const methodologySeeded = ref(false)
watch(
  () => profileStore.required,
  (req) => {
    if (req && !methodologySeeded.value && !form.meta.value.dirty) {
      form.setFieldValue('methodology', req.methodology)
      methodologySeeded.value = true
    }
  },
  { immediate: true },
)

const eventOptions = computed(() => profileStore.goals?.events ?? [])

const methodologyOptions = [
  { value: 'Pyramidal', label: 'Pyramidal' },
  { value: 'Periodization', label: 'Periodization' },
  { value: 'Polarized', label: 'Polarized' },
  { value: 'Norwegian', label: 'Norwegian' },
]

const sportOptions = [
  { value: 'Swim', label: 'Swim' },
  { value: 'Bike', label: 'Bike' },
  { value: 'Run', label: 'Run' },
  { value: 'Triathlon', label: 'Triathlon' },
  { value: 'Strength', label: 'Strength' },
]

function addWorkout() {
  justCreated.value = false
  push({
    sport: 'Run',
    scheduledDate: '',
    title: '',
    plannedDurationMinutes: null,
    plannedLoad: null,
  })
}

function setError(e: unknown) {
  const messages = extractApiValidationMessages(e)
  if (messages) {
    globalError.value = messages.join(' ')
  } else if (e instanceof ApiError) {
    globalError.value = `Couldn't create plan: ${e.statusText} (${e.status})`
  } else if (e instanceof Error) {
    globalError.value = `Couldn't create plan: ${e.message}`
  } else {
    globalError.value = "Couldn't create plan — please try again."
  }
}

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  justCreated.value = false

  const req: TrainingPlanRequest = {
    name: values.name,
    methodology: values.methodology,
    startDate: values.startDate,
    endDate: values.endDate,
    eventId: values.eventId || null,
    plannedWorkouts: values.plannedWorkouts.map((w) => ({
      sport: w.sport,
      scheduledDate: w.scheduledDate,
      title: w.title,
      description: null,
      plannedDurationMinutes: w.plannedDurationMinutes,
      plannedLoad: w.plannedLoad,
    })),
  }

  try {
    createdPlan.value = await trainingStore.createPlan(req)
    buildTarget.value = null
    justCreated.value = true
    form.resetForm()
    methodologySeeded.value = false // allow the next plan to re-seed from the athlete default
  } catch (e) {
    setError(e)
  }
})

const isSubmitting = form.isSubmitting
</script>

<template>
  <AppShell
    title="Create Training Plan"
    subtitle="Name your plan, set its window, and schedule planned workouts."
  >
    <div class="mx-auto w-full max-w-3xl">
    <form class="space-y-8" @submit="onSubmit">
      <!-- Plan details -->
      <section class="rounded-lg border bg-card p-6 space-y-4">
        <h2 class="text-lg font-semibold">Plan details</h2>

        <FormField v-slot="{ componentField }" name="name">
          <FormItem>
            <FormLabel>Name</FormLabel>
            <FormControl>
              <Input v-bind="componentField" placeholder="e.g. Spring Base Block" />
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
              <FormLabel>Target event (optional)</FormLabel>
              <Select v-bind="componentField">
                <FormControl>
                  <SelectTrigger>
                    <SelectValue placeholder="No target event" />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  <SelectItem v-for="ev in eventOptions" :key="ev.id" :value="ev.id">
                    {{ ev.name }}
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
      </section>

      <!-- Planned workouts -->
      <section class="rounded-lg border bg-card p-6">
        <h2 class="text-lg font-semibold">Planned workouts</h2>
        <p class="mt-1 text-sm text-muted-foreground">
          Schedule the sessions for this plan. You can add more later.
        </p>

        <div v-if="fields.length === 0" class="mt-4 text-sm text-muted-foreground/60">
          No planned workouts added yet.
        </div>

        <div
          v-for="(field, index) in fields"
          :key="field.key"
          class="mt-4 space-y-4 rounded-md border p-4"
        >
          <div class="flex items-center justify-between">
            <span class="text-sm font-medium">Workout {{ index + 1 }}</span>
            <Button type="button" variant="ghost" size="sm" @click="remove(index)">Remove</Button>
          </div>

          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField v-slot="{ componentField }" :name="`plannedWorkouts[${index}].sport`">
              <FormItem>
                <FormLabel>Sport</FormLabel>
                <Select v-bind="componentField">
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    <SelectItem v-for="opt in sportOptions" :key="opt.value" :value="opt.value">
                      {{ opt.label }}
                    </SelectItem>
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            </FormField>

            <FormField v-slot="{ componentField }" :name="`plannedWorkouts[${index}].scheduledDate`">
              <FormItem>
                <FormLabel>Scheduled date</FormLabel>
                <FormControl>
                  <Input type="date" v-bind="componentField" />
                </FormControl>
                <FormMessage />
              </FormItem>
            </FormField>
          </div>

          <FormField v-slot="{ componentField }" :name="`plannedWorkouts[${index}].title`">
            <FormItem>
              <FormLabel>Title</FormLabel>
              <FormControl>
                <Input v-bind="componentField" placeholder="e.g. Threshold intervals" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField v-slot="{ componentField }" :name="`plannedWorkouts[${index}].plannedDurationMinutes`">
              <FormItem>
                <FormLabel>Duration (min, optional)</FormLabel>
                <FormControl>
                  <Input type="number" min="0" v-bind="componentField" />
                </FormControl>
                <FormMessage />
              </FormItem>
            </FormField>

            <FormField v-slot="{ componentField }" :name="`plannedWorkouts[${index}].plannedLoad`">
              <FormItem>
                <FormLabel>Planned load (TSS, optional)</FormLabel>
                <FormControl>
                  <Input type="number" min="0" step="0.01" v-bind="componentField" />
                </FormControl>
                <FormMessage />
              </FormItem>
            </FormField>
          </div>
        </div>

        <Button type="button" variant="outline" class="mt-4" @click="addWorkout">
          Add Planned Workout
        </Button>
      </section>

      <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ globalError }}
      </div>

      <div
        v-if="justCreated"
        class="flex items-center gap-2 rounded-md bg-primary/10 px-4 py-3 text-sm"
      >
        <CheckCircle2 :size="16" class="text-primary" />
        <span>Plan created.</span>
        <router-link to="/" class="font-medium text-primary hover:underline">
          View on your dashboard
        </router-link>
      </div>

      <div class="flex justify-end">
        <Button type="submit" :disabled="isSubmitting">Create Plan</Button>
      </div>
    </form>

    <!-- Structured-workout builder (Task 10-5), launched from the just-created plan's workouts. -->
    <section v-if="createdPlan && !buildTarget" class="mt-8 rounded-lg border bg-card p-6">
      <h2 class="text-lg font-semibold">Build structured workouts</h2>
      <p class="mt-1 text-sm text-muted-foreground">
        Add interval blocks and steps to the workouts you just scheduled.
      </p>
      <ul class="mt-4 divide-y">
        <li
          v-for="pw in createdPlan.plannedWorkouts"
          :key="pw.id"
          class="flex items-center justify-between gap-4 py-3"
        >
          <span class="text-sm">
            <span class="font-medium">{{ pw.title }}</span>
            &middot; {{ pw.sport }} &middot; {{ pw.scheduledDate }}
          </span>
          <Button type="button" variant="outline" size="sm" @click="openBuilder(pw)">
            Build structure
          </Button>
        </li>
        <li
          v-if="createdPlan.plannedWorkouts.length === 0"
          class="py-3 text-sm text-muted-foreground/60"
        >
          This plan has no planned workouts to build.
        </li>
      </ul>
    </section>

    <WorkoutStructureBuilder
      v-if="buildTarget"
      :key="buildTarget.plannedWorkoutId"
      class="mt-8"
      :plan-id="buildTarget.planId"
      :planned-workout-id="buildTarget.plannedWorkoutId"
      :sport="buildTarget.sport"
      :title="buildTarget.title"
      @close="buildTarget = null"
    />
    </div>
  </AppShell>
</template>
