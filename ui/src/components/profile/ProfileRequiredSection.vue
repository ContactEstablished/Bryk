<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
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
import { useProfileStore } from '@/stores/profile'
import { ApiError } from '@/services/api'
import { mapApiValidationToFields } from '@/services/apiErrors'
import {
  onboardingRequiredSchema,
  type OnboardingRequiredFormValues,
} from '@/schemas/onboarding'
import type { ProfileRequiredResponse } from '@/types/profile'

const store = useProfileStore()

const form = useForm<OnboardingRequiredFormValues>({
  validationSchema: toTypedSchema(onboardingRequiredSchema),
})

const globalError = ref<string | null>(null)
const justSaved = ref(false)

// Seed the form whenever the loaded profile changes (initial load + post-save re-fetch).
// resetForm leaves the form pristine, so the "Saved" flag stays visible until the next edit.
watch(
  () => store.required,
  (data: ProfileRequiredResponse | null) => {
    if (data) form.resetForm({ values: { ...data } })
  },
  { immediate: true },
)

watch(
  () => form.meta.value.dirty,
  (dirty) => {
    if (dirty) justSaved.value = false
  },
)

onMounted(() => {
  void store.loadRequired()
})

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
    await store.saveRequired(values)
    justSaved.value = true
  } catch (e) {
    const validation = mapApiValidationToFields(e, 'required')
    if (validation) {
      for (const { path, message } of validation.fieldErrors) {
        form.setFieldError(path as Parameters<typeof form.setFieldError>[0], message)
      }
      globalError.value = validation.globalMessages.length > 0
        ? validation.globalMessages.join(' ')
        : validation.fieldErrors.length === 0
          ? "Couldn't save — please review the highlighted fields."
          : null
    } else if (e instanceof ApiError) {
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
  <section class="rounded-lg border bg-card p-6">
    <h2 class="text-2xl font-semibold">Required Information</h2>

    <form v-if="store.required" class="mt-6 space-y-6" @submit="onSubmit">
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
              <SelectItem v-for="opt in genderOptions" :key="opt.value" :value="opt.value">
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
            <FormLabel>Height (cm)</FormLabel>
            <FormControl>
              <Input type="number" placeholder="0" v-bind="componentField" />
            </FormControl>
            <FormMessage />
          </FormItem>
        </FormField>

        <FormField v-slot="{ componentField }" name="weightKg">
          <FormItem>
            <FormLabel>Weight (kg)</FormLabel>
            <FormControl>
              <Input type="number" placeholder="0" v-bind="componentField" />
            </FormControl>
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
              <SelectItem v-for="opt in methodologyOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </SelectItem>
            </SelectContent>
          </Select>
          <FormMessage />
        </FormItem>
      </FormField>

      <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ globalError }}
      </div>

      <div class="flex items-center justify-end gap-3">
        <span v-if="justSaved" class="flex items-center gap-1 text-sm text-muted-foreground">
          <CheckCircle2 :size="16" class="text-primary" />
          Saved
        </span>
        <Button type="submit" :disabled="isSubmitting">Save changes</Button>
      </div>
    </form>

    <div v-else-if="store.requiredError" class="mt-4">
      <p class="text-sm text-destructive">Couldn't load your required profile.</p>
      <Button class="mt-3" variant="outline" @click="store.loadRequired()">Retry</Button>
    </div>

    <p v-else class="mt-4 text-sm text-muted-foreground">
      Loading…
    </p>
  </section>
</template>
