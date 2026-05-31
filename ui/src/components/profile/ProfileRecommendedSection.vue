<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { CheckCircle2 } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { useProfileStore } from '@/stores/profile'
import { ApiError } from '@/services/api'
import { mapApiValidationToFields } from '@/services/apiErrors'
import {
  onboardingRecommendedSchema,
  type OnboardingRecommendedFormValues,
} from '@/schemas/onboarding'
import type { OnboardingRecommendedRequest } from '@/types/onboarding'
import type { ProfileRecommendedResponse } from '@/types/profile'

const store = useProfileStore()

// Fixed 3-row order — must match RecommendedStep so the validation-error mapper's
// sport-index logic resolves to the right row.
const sportLabels = [
  { sport: 'Bike' as const, label: 'Bike', thresholdLabel: 'FTP (watts)', lt1Label: 'LT1 (watts)', lt2Label: 'LT2 (watts)' },
  { sport: 'Run' as const, label: 'Run', thresholdLabel: 'Threshold pace (min/km)', lt1Label: 'LT1 pace', lt2Label: 'LT2 pace' },
  { sport: 'Swim' as const, label: 'Swim', thresholdLabel: 'Threshold pace (min/100m)', lt1Label: 'LT1 pace', lt2Label: 'LT2 pace' },
]

// Fixed 3-row shape from the start so the template can index sportThresholds[index]
// before loadRecommended resolves; the watch below overlays the loaded values.
const form = useForm<OnboardingRecommendedFormValues>({
  validationSchema: toTypedSchema(onboardingRecommendedSchema),
  initialValues: {
    restingHr: null,
    maxHr: null,
    sportThresholds: sportLabels.map((s) => ({
      sport: s.sport,
      isActive: false,
      thresholdValue: null,
      lt1: null,
      lt2: null,
      customZonesJson: null,
    })),
  },
})

const globalError = ref<string | null>(null)
const justSaved = ref(false)

// The read endpoint returns only the sports that have a stored profile (each with its
// IsActive flag); overlay them onto the fixed 3-row template so inactive sports still
// render their (collapsed) row.
function toFormValues(data: ProfileRecommendedResponse): OnboardingRecommendedFormValues {
  const bySport = new Map(data.sportThresholds.map((s) => [s.sport, s]))
  return {
    restingHr: data.restingHr,
    maxHr: data.maxHr,
    sportThresholds: sportLabels.map((s) => {
      const found = bySport.get(s.sport)
      return found
        ? {
            sport: s.sport,
            isActive: found.isActive,
            thresholdValue: found.thresholdValue,
            lt1: found.lt1,
            lt2: found.lt2,
            customZonesJson: found.customZonesJson,
          }
        : { sport: s.sport, isActive: false, thresholdValue: null, lt1: null, lt2: null, customZonesJson: null }
    }),
  }
}

watch(
  () => store.recommended,
  (data: ProfileRecommendedResponse | null) => {
    if (data) form.resetForm({ values: toFormValues(data) })
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
  void store.loadRecommended()
})

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const activeSportIndices = values.sportThresholds
    .map((s, i) => (s.isActive ? i : -1))
    .filter((i) => i >= 0)
  const payload: OnboardingRecommendedRequest = {
    restingHr: values.restingHr ?? null,
    maxHr: values.maxHr ?? null,
    sportThresholds: values.sportThresholds
      .filter((s) => s.isActive)
      .map((s) => ({ ...s, customZonesJson: s.customZonesJson ?? null })),
  }
  try {
    await store.saveRecommended(payload)
    justSaved.value = true
  } catch (e) {
    const validation = mapApiValidationToFields(e, 'recommended', {
      activeSportCount: activeSportIndices.length,
      activeSportIndices,
    })
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
    <h2 class="text-2xl font-semibold">Recommended Profile</h2>
    <p class="mt-2 text-sm text-muted-foreground">
      Optional &mdash; fill out what you have. You can update any of this later.
    </p>

    <form v-if="store.recommended" class="mt-6 space-y-8" @submit="onSubmit">
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
              <FormField v-slot="{ componentField }" :name="`sportThresholds[${index}].thresholdValue`">
                <FormItem>
                  <FormLabel>{{ sport.thresholdLabel }}</FormLabel>
                  <FormControl>
                    <Input type="number" step="0.01" v-bind="componentField" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              </FormField>

              <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <FormField v-slot="{ componentField }" :name="`sportThresholds[${index}].lt1`">
                  <FormItem>
                    <FormLabel class="text-xs">{{ sport.lt1Label }}</FormLabel>
                    <FormControl>
                      <Input type="number" step="0.01" v-bind="componentField" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                </FormField>

                <FormField v-slot="{ componentField }" :name="`sportThresholds[${index}].lt2`">
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

      <div class="flex items-center justify-end gap-3">
        <span v-if="justSaved" class="flex items-center gap-1 text-sm text-muted-foreground">
          <CheckCircle2 :size="16" class="text-primary" />
          Saved
        </span>
        <Button type="submit" :disabled="isSubmitting">Save changes</Button>
      </div>
    </form>

    <div v-else-if="store.recommendedError" class="mt-4">
      <p class="text-sm text-destructive">Couldn't load your recommended profile.</p>
      <Button class="mt-3" variant="outline" @click="store.loadRecommended()">Retry</Button>
    </div>

    <p v-else class="mt-4 text-sm text-muted-foreground">
      Loading…
    </p>
  </section>
</template>
