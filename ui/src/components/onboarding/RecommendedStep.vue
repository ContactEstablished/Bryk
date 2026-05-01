<script setup lang="ts">
import { ref } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { useOnboardingStore } from '@/stores/onboarding'
import { ApiError } from '@/services/api'
import {
  onboardingRecommendedSchema,
  type OnboardingRecommendedFormValues,
} from '@/schemas/onboarding'
import type { OnboardingRecommendedRequest } from '@/types/onboarding'

const emit = defineEmits<{ next: [] }>()
const store = useOnboardingStore()

const sportLabels = [
  { sport: 'Bike' as const, label: 'Bike', thresholdLabel: 'FTP (watts)', lt1Label: 'LT1 (watts)', lt2Label: 'LT2 (watts)' },
  { sport: 'Run' as const, label: 'Run', thresholdLabel: 'Threshold pace (min/km)', lt1Label: 'LT1 pace', lt2Label: 'LT2 pace' },
  { sport: 'Swim' as const, label: 'Swim', thresholdLabel: 'Threshold pace (min/100m)', lt1Label: 'LT1 pace', lt2Label: 'LT2 pace' },
]

const form = useForm<OnboardingRecommendedFormValues>({
  validationSchema: toTypedSchema(onboardingRecommendedSchema),
  initialValues: {
    restingHr: null,
    maxHr: null,
    sportThresholds: [
      { sport: 'Bike', isActive: false, thresholdValue: null, lt1: null, lt2: null, customZonesJson: null },
      { sport: 'Run', isActive: false, thresholdValue: null, lt1: null, lt2: null, customZonesJson: null },
      { sport: 'Swim', isActive: false, thresholdValue: null, lt1: null, lt2: null, customZonesJson: null },
    ],
  },
})

const globalError = ref<string | null>(null)

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const payload: OnboardingRecommendedRequest = {
    restingHr: values.restingHr ?? null,
    maxHr: values.maxHr ?? null,
    sportThresholds: values.sportThresholds
      .filter((s) => s.isActive)
      .map((s) => ({ ...s, customZonesJson: s.customZonesJson ?? null })),
  }
  try {
    await store.submitRecommended(payload)
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
    <h3 class="text-2xl font-semibold">Recommended Profile</h3>
    <p class="mt-2 text-sm text-muted-foreground">
      Optional &mdash; fill out what you have. You can update any of this later.
    </p>

    <form class="mt-6 space-y-8" @submit="onSubmit">
      <!-- Heart Rate -->
      <fieldset>
        <legend class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Heart Rate</legend>
        <div class="mt-3 grid grid-cols-1 gap-6 sm:grid-cols-2">
          <FormField v-slot="{ componentField }" name="restingHr">
            <FormItem>
              <FormLabel>Resting HR (bpm)</FormLabel>
              <FormControl>
                <Input type="number" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField v-slot="{ componentField }" name="maxHr">
            <FormItem>
              <FormLabel>Max HR (bpm)</FormLabel>
              <FormControl>
                <Input type="number" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>
        </div>
      </fieldset>

      <!-- Sports -->
      <fieldset>
        <legend class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Sports</legend>
        <div class="mt-3 space-y-4">
          <div v-for="(sport, index) in sportLabels" :key="sport.sport">
            <!-- Sport checkbox row -->
            <FormField
              v-slot="{ value, handleChange }"
              :name="`sportThresholds[${index}].isActive`"
              type="checkbox"
              :value="true"
              :unchecked-value="false"
            >
              <FormItem class="flex flex-row items-center gap-3 space-y-0">
                <FormControl>
                  <Checkbox :model-value="value" @update:model-value="handleChange" />
                </FormControl>
                <FormLabel class="text-base font-medium">{{ sport.label }}</FormLabel>
              </FormItem>
            </FormField>

            <!-- Sub-fields (visible when active) -->
            <div
              v-if="form.values.sportThresholds[index]?.isActive"
              class="mt-3 space-y-4 border-l-2 border-muted pl-8"
            >
              <FormField
                v-slot="{ componentField }"
                :name="`sportThresholds[${index}].thresholdValue`"
              >
                <FormItem>
                  <FormLabel>{{ sport.thresholdLabel }}</FormLabel>
                  <FormControl>
                    <Input type="number" step="0.01" v-bind="componentField" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              </FormField>

              <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <FormField
                  v-slot="{ componentField }"
                  :name="`sportThresholds[${index}].lt1`"
                >
                  <FormItem>
                    <FormLabel class="text-xs">{{ sport.lt1Label }}</FormLabel>
                    <FormControl>
                      <Input type="number" step="0.01" v-bind="componentField" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                </FormField>

                <FormField
                  v-slot="{ componentField }"
                  :name="`sportThresholds[${index}].lt2`"
                >
                  <FormItem>
                    <FormLabel class="text-xs">{{ sport.lt2Label }}</FormLabel>
                    <FormControl>
                      <Input type="number" step="0.01" v-bind="componentField" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                </FormField>
              </div>

              <p class="text-xs text-muted-foreground">
                Optional &mdash; only if you track lactate thresholds
              </p>
            </div>
          </div>
        </div>
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
