<script setup lang="ts">
import { ref, watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
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
import { useOnboardingStore } from '@/stores/onboarding'
import { ApiError } from '@/services/api'
import { onboardingRequiredSchema } from '@/schemas/onboarding'
import type { OnboardingRequiredFormValues } from '@/schemas/onboarding'

const emit = defineEmits<{ next: [] }>()
const store = useOnboardingStore()

const form = useForm<OnboardingRequiredFormValues>({
  validationSchema: toTypedSchema(onboardingRequiredSchema),
})

const globalError = ref<string | null>(null)

// ── Unit system ───────────────────────────────────────
type UnitSystem = 'metric' | 'imperial'
const STORAGE_KEY = 'bryk:unitSystem'

const initialUnit: UnitSystem = (() => {
  const stored = localStorage.getItem(STORAGE_KEY)
  return stored === 'metric' ? 'metric' : 'imperial'
})()

const unitSystem = ref<UnitSystem>(initialUnit)

const heightFeet = ref<number | null>(null)
const heightInches = ref<number | null>(null)
const weightLb = ref<number | null>(null)

const CM_PER_INCH = 2.54
const KG_PER_LB = 0.453592

let suppressImperialSync = false

watch([heightFeet, heightInches], ([f, i]) => {
  if (suppressImperialSync) return
  if (unitSystem.value !== 'imperial') return
  if (f == null && i == null) {
    form.setFieldValue('heightCm', undefined as unknown as number)
    return
  }
  const totalInches = (f ?? 0) * 12 + (i ?? 0)
  form.setFieldValue('heightCm', Math.round(totalInches * CM_PER_INCH * 10) / 10)
}, { flush: 'sync' })

watch(weightLb, (lb) => {
  if (suppressImperialSync) return
  if (unitSystem.value !== 'imperial') return
  if (lb == null) {
    form.setFieldValue('weightKg', undefined as unknown as number)
    return
  }
  form.setFieldValue('weightKg', Math.round(lb * KG_PER_LB * 10) / 10)
}, { flush: 'sync' })

function setUnitSystem(target: UnitSystem) {
  if (unitSystem.value === target) return

  if (target === 'imperial') {
    suppressImperialSync = true
    const cm = form.values.heightCm
    if (typeof cm === 'number' && cm > 0) {
      const totalInches = cm / CM_PER_INCH
      const feet = Math.floor(totalInches / 12)
      heightFeet.value = feet
      heightInches.value = Math.round((totalInches - feet * 12) * 10) / 10
    } else {
      heightFeet.value = null
      heightInches.value = null
    }
    const kg = form.values.weightKg
    weightLb.value = typeof kg === 'number' && kg > 0
      ? Math.round((kg / KG_PER_LB) * 10) / 10
      : null
    suppressImperialSync = false
  }
  // Switching to metric: form already has heightCm/weightKg from prior imperial input, no extra work.

  unitSystem.value = target
  localStorage.setItem(STORAGE_KEY, target)
}

// ── Form options ──────────────────────────────────────
const genderOptions = [
  { value: 'Male', label: 'Male' },
  { value: 'Female', label: 'Female' },
  { value: 'Other', label: 'Other' },
  { value: 'PreferNotToSay', label: 'Prefer not to say' },
] as const

const methodologyOptions = [
  { value: 'Pyramidal', label: 'Pyramidal' },
  { value: 'Periodization', label: 'Periodization' },
  { value: 'Polarized', label: 'Polarized' },
  { value: 'Norwegian', label: 'Norwegian' },
] as const

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  try {
    await store.submitRequired(values)
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
    <div class="flex items-start justify-between gap-4">
      <h3 class="text-2xl font-semibold">Required Information</h3>
      <div class="inline-flex rounded-md border bg-muted p-0.5">
        <button
          type="button"
          class="rounded-sm px-3 py-1 text-xs font-medium transition-colors"
          :class="unitSystem === 'metric'
            ? 'bg-background text-foreground shadow-sm'
            : 'text-muted-foreground hover:text-foreground'"
          @click="setUnitSystem('metric')"
        >
          Metric
        </button>
        <button
          type="button"
          class="rounded-sm px-3 py-1 text-xs font-medium transition-colors"
          :class="unitSystem === 'imperial'
            ? 'bg-background text-foreground shadow-sm'
            : 'text-muted-foreground hover:text-foreground'"
          @click="setUnitSystem('imperial')"
        >
          Imperial
        </button>
      </div>
    </div>

    <form class="mt-6 space-y-6" @submit="onSubmit">
      <FormField v-slot="{ componentField }" name="name">
        <FormItem>
          <FormLabel>Name</FormLabel>
          <FormControl>
            <Input type="text" placeholder="Your name" v-bind="componentField" />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="gender">
        <FormItem>
          <FormLabel>Gender</FormLabel>
          <Select v-bind="componentField">
            <FormControl>
              <SelectTrigger>
                <SelectValue placeholder="Select a gender" />
              </SelectTrigger>
            </FormControl>
            <SelectContent>
              <SelectItem
                v-for="opt in genderOptions"
                :key="opt.value"
                :value="opt.value"
              >
                {{ opt.label }}
              </SelectItem>
            </SelectContent>
          </Select>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="dateOfBirth">
        <FormItem>
          <FormLabel>Date of Birth</FormLabel>
          <FormControl>
            <Input type="date" v-bind="componentField" />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <div class="grid grid-cols-1 gap-6 sm:grid-cols-2">
        <FormField v-slot="{ componentField }" name="heightCm">
          <FormItem>
            <FormLabel>
              Height ({{ unitSystem === 'metric' ? 'cm' : 'ft / in' }})
            </FormLabel>
            <FormControl v-if="unitSystem === 'metric'">
              <Input type="number" placeholder="0" v-bind="componentField" />
            </FormControl>
            <div v-else class="grid grid-cols-2 gap-2">
              <Input
                type="number"
                placeholder="ft"
                :model-value="heightFeet ?? ''"
                @update:model-value="(v) => heightFeet = v === '' ? null : Number(v)"
              />
              <Input
                type="number"
                placeholder="in"
                step="0.1"
                :model-value="heightInches ?? ''"
                @update:model-value="(v) => heightInches = v === '' ? null : Number(v)"
              />
            </div>
            <FormMessage />
          </FormItem>
        </FormField>

        <FormField v-slot="{ componentField }" name="weightKg">
          <FormItem>
            <FormLabel>
              Weight ({{ unitSystem === 'metric' ? 'kg' : 'lb' }})
            </FormLabel>
            <FormControl v-if="unitSystem === 'metric'">
              <Input type="number" placeholder="0" v-bind="componentField" />
            </FormControl>
            <Input
              v-else
              type="number"
              placeholder="lb"
              step="0.1"
              :model-value="weightLb ?? ''"
              @update:model-value="(v) => weightLb = v === '' ? null : Number(v)"
            />
            <FormMessage />
          </FormItem>
        </FormField>
      </div>

      <div class="grid grid-cols-1 gap-6 sm:grid-cols-2">
        <FormField v-slot="{ componentField }" name="yearsTraining">
          <FormItem>
            <FormLabel>Years Training</FormLabel>
            <FormControl>
              <Input type="number" placeholder="0" v-bind="componentField" />
            </FormControl>
            <FormMessage />
          </FormItem>
        </FormField>

        <FormField v-slot="{ componentField }" name="typicalWeeklyHours">
          <FormItem>
            <FormLabel>Typical Weekly Hours</FormLabel>
            <FormControl>
              <Input type="number" placeholder="0" v-bind="componentField" />
            </FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
      </div>

      <FormField v-slot="{ componentField }" name="methodology">
        <FormItem>
          <FormLabel>Training Methodology</FormLabel>
          <Select v-bind="componentField">
            <FormControl>
              <SelectTrigger>
                <SelectValue placeholder="Select a methodology" />
              </SelectTrigger>
            </FormControl>
            <SelectContent>
              <SelectItem
                v-for="opt in methodologyOptions"
                :key="opt.value"
                :value="opt.value"
              >
                {{ opt.label }}
              </SelectItem>
            </SelectContent>
          </Select>
          <p class="mt-1 text-xs text-muted-foreground">
            *Don't worry, you can change this later
          </p>
          <FormMessage />
        </FormItem>
      </FormField>

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
