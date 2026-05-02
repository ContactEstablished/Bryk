<script setup lang="ts">
import { ref, watch } from 'vue'
import { useFieldArray, useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from '@/components/ui/select'
import { useOnboardingStore } from '@/stores/onboarding'
import { ApiError } from '@/services/api'
import {
  onboardingGoalsSchema,
  type OnboardingGoalsFormValues,
} from '@/schemas/onboarding'
import type { OnboardingGoalsRequest } from '@/types/onboarding'

const emit = defineEmits<{ next: [] }>()
const store = useOnboardingStore()

const form = useForm<OnboardingGoalsFormValues>({
  validationSchema: toTypedSchema(onboardingGoalsSchema),
  initialValues: {
    events: [],
    goals: [],
  },
})

interface EventFormItem {
  name: string
  eventDate: string
  sport: 'Swim' | 'Bike' | 'Run' | 'Triathlon' | null
  triathlonDistance: 'Sprint' | 'Olympic' | 'HalfIronman' | 'Ironman' | 'Custom' | null
  customDistanceName: string | null
  priority: 'A' | 'B' | 'C'
  notes: string | null
}

interface GoalFormItem {
  description: string
  targetDate: string | null
}

const { fields: eventFields, push: pushEvent, remove: removeEvent } = useFieldArray<EventFormItem>('events')
const { fields: goalFields, push: pushGoal, remove: removeGoal } = useFieldArray<GoalFormItem>('goals')

const globalError = ref<string | null>(null)

function addEvent() {
  pushEvent({
    name: '',
    eventDate: '',
    sport: null,
    triathlonDistance: null,
    customDistanceName: null,
    priority: 'B' as const,
    notes: null,
  })
}

function addGoal() {
  pushGoal({
    description: '',
    targetDate: null,
  })
}

// Reset dependent fields when parent selections change
watch(
  () => form.values.events,
  (events) => {
    let changed = false
    const updated = events.map((event) => {
      let e = { ...event }
      if (e.sport !== 'Triathlon' && e.triathlonDistance !== null) {
        e = { ...e, triathlonDistance: null }
        changed = true
      }
      if (e.triathlonDistance !== 'Custom' && e.customDistanceName !== null) {
        e = { ...e, customDistanceName: null }
        changed = true
      }
      return e
    })
    if (changed) {
      form.setFieldValue('events', updated)
    }
  },
  { deep: true },
)

const sportOptions = [
  { value: '', label: 'None' },
  { value: 'Swim', label: 'Swim' },
  { value: 'Bike', label: 'Bike' },
  { value: 'Run', label: 'Run' },
  { value: 'Triathlon', label: 'Triathlon' },
]

const priorityOptions = [
  { value: 'A', label: 'A' },
  { value: 'B', label: 'B' },
  { value: 'C', label: 'C' },
]

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const payload: OnboardingGoalsRequest = {
    events: values.events.map((e) => ({
      name: e.name,
      eventDate: e.eventDate,
      sport: e.sport || null,
      triathlonDistance: e.sport === 'Triathlon' ? e.triathlonDistance : null,
      customDistanceName: e.triathlonDistance === 'Custom' ? e.customDistanceName : null,
      priority: e.priority,
      notes: e.notes ?? null,
    })),
    goals: values.goals.map((g) => ({
      type: 'General' as const,
      description: g.description,
      targetDate: g.targetDate || null,
    })),
  }
  try {
    await store.submitGoals(payload)
    emit('next')
  } catch (e) {
    if (e instanceof ApiError) {
      globalError.value = `Couldn't save: ${e.statusText} (${e.status})`
    } else if (e instanceof Error) {
      globalError.value = `Couldn't save: ${e.message}`
    } else {
      globalError.value = "Couldn't save — please try again."
    }
  }
})

const isSubmitting = form.isSubmitting
</script>

<template>
  <div>
    <h3 class="text-2xl font-semibold">Set Your Goals</h3>
    <p class="mt-2 text-sm text-muted-foreground">
      Tell us about your upcoming events and training goals so we can plan your season.
    </p>

    <form class="mt-6 space-y-10" @submit="onSubmit">
      <!-- Events -->
      <fieldset>
        <legend class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Events</legend>

        <div v-if="eventFields.length === 0" class="mt-3 text-sm text-muted-foreground/60">
          No events added yet.
        </div>

        <div v-for="(field, index) in eventFields" :key="field.key" class="mt-4 p-4 border rounded-md space-y-4">
          <div class="flex items-center justify-between">
            <span class="text-sm font-medium">Event {{ index + 1 }}</span>
            <Button type="button" variant="ghost" size="sm" @click="removeEvent(index)">Remove</Button>
          </div>

          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField v-slot="{ componentField }" :name="`events[${index}].name`">
              <FormItem>
                <FormLabel>Name</FormLabel>
                <FormControl>
                  <Input v-bind="componentField" />
                </FormControl>
                <FormMessage />
              </FormItem>
            </FormField>

            <FormField v-slot="{ componentField }" :name="`events[${index}].eventDate`">
              <FormItem>
                <FormLabel>Date</FormLabel>
                <FormControl>
                  <Input type="date" v-bind="componentField" />
                </FormControl>
                <FormMessage />
              </FormItem>
            </FormField>
          </div>

          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField v-slot="{ componentField }" :name="`events[${index}].sport`">
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

            <FormField
              v-if="form.values.events[index]?.sport === 'Triathlon'"
              v-slot="{ componentField }"
              :name="`events[${index}].triathlonDistance`"
            >
              <FormItem>
                <FormLabel>Triathlon Distance</FormLabel>
                <Select v-bind="componentField">
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    <SelectItem value="Sprint">Sprint</SelectItem>
                    <SelectItem value="Olympic">Olympic</SelectItem>
                    <SelectItem value="HalfIronman">Half Ironman</SelectItem>
                    <SelectItem value="Ironman">Ironman</SelectItem>
                    <SelectItem value="Custom">Other</SelectItem>
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            </FormField>
          </div>

          <FormField
            v-if="form.values.events[index]?.triathlonDistance === 'Custom'"
            v-slot="{ componentField }"
            :name="`events[${index}].customDistanceName`"
          >
            <FormItem>
              <FormLabel>Describe the distance</FormLabel>
              <FormControl>
                <Input v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField v-slot="{ componentField }" :name="`events[${index}].priority`">
              <FormItem>
                <FormLabel>Priority</FormLabel>
                <Select v-bind="componentField">
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    <SelectItem v-for="opt in priorityOptions" :key="opt.value" :value="opt.value">
                      {{ opt.label }}
                    </SelectItem>
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            </FormField>

            <FormField v-slot="{ componentField }" :name="`events[${index}].notes`">
              <FormItem>
                <FormLabel>Notes</FormLabel>
                <FormControl>
                  <Input v-bind="componentField" />
                </FormControl>
                <FormMessage />
              </FormItem>
            </FormField>
          </div>
        </div>

        <Button type="button" variant="outline" class="mt-4" @click="addEvent">Add Event</Button>
      </fieldset>

      <!-- Goals -->
      <fieldset>
        <legend class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Goals</legend>

        <div v-if="goalFields.length === 0" class="mt-3 text-sm text-muted-foreground/60">
          No goals added yet.
        </div>

        <div v-for="(field, index) in goalFields" :key="field.key" class="mt-4 p-4 border rounded-md space-y-4">
          <div class="flex items-center justify-between">
            <span class="text-sm font-medium">Goal {{ index + 1 }}</span>
            <Button type="button" variant="ghost" size="sm" @click="removeGoal(index)">Remove</Button>
          </div>

          <FormField v-slot="{ componentField }" :name="`goals[${index}].description`">
            <FormItem>
              <FormLabel>Description</FormLabel>
              <FormControl>
                <Input v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField v-slot="{ componentField }" :name="`goals[${index}].targetDate`">
            <FormItem>
              <FormLabel>Target Date</FormLabel>
              <FormControl>
                <Input type="date" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>
        </div>

        <Button type="button" variant="outline" class="mt-4" @click="addGoal">Add Goal</Button>
      </fieldset>

      <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ globalError }}
      </div>

      <div class="flex justify-end">
        <Button type="submit" :disabled="isSubmitting || store.submitting">
          Continue
        </Button>
      </div>
    </form>
  </div>
</template>
