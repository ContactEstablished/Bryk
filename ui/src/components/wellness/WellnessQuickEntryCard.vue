<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import ScaleSelector from '@/components/common/ScaleSelector.vue'
import { useWellnessStore } from '@/stores/wellness'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { wellnessEntrySchema, type WellnessFormValues } from '@/schemas/wellness'
import type { WellnessEntryRequest } from '@/types/wellness'

// No props and no emits by design: every surface that shows wellness reads this same Pinia store,
// and saveToday re-fetches both the day and the summary, so the tiles refresh without an event to
// plumb. There is deliberately NO date picker either - the card is "Today" by definition; back-dating
// is out of Phase 20's scope.
const store = useWellnessStore()

onMounted(() => {
  if (!store.today) void store.loadToday()
})

const expanded = ref(false)
const formError = ref<string | null>(null)

// Server field prefix -> form field. A LITERAL record: Task 20-2's messages are field-prefixed
// ("RestingHr: ...") because ValidateOrThrowAsync collects ErrorMessage only and drops the property
// name, so the prefix is the only handle we have. "Date:" and "Entry:" have no field to land on and
// fall through to the form-level line.
const SERVER_FIELD_MAP: Record<string, keyof WellnessFormValues> = {
  SleepHours: 'sleepHours',
  SleepQuality: 'sleepQuality',
  RestingHr: 'restingHr',
  WeightKg: 'weightKg',
  Soreness: 'soreness',
  HrvMs: 'hrvMs',
  Notes: 'notes',
}

// PUT replaces the whole day, so the form always starts from the stored day in full (or all-null).
function valuesFromStore(): WellnessFormValues {
  const t = store.today
  return {
    sleepHours: t?.sleepHours ?? null,
    sleepQuality: t?.sleepQuality ?? null,
    restingHr: t?.restingHr ?? null,
    weightKg: t?.weightKg ?? null,
    soreness: t?.soreness ?? null,
    hrvMs: t?.hrvMs ?? null,
    notes: t?.notes ?? null,
  }
}

const form = useForm<WellnessFormValues>({
  validationSchema: toTypedSchema(wellnessEntrySchema),
  initialValues: valuesFromStore(),
})

const summaryLine = computed(() => {
  const t = store.today
  if (!t) return ''
  const parts: string[] = []
  if (t.sleepHours != null) parts.push(`${t.sleepHours} h`)
  if (t.sleepQuality != null) parts.push(`Q${t.sleepQuality}`)
  if (t.restingHr != null) parts.push(`${t.restingHr} bpm`)
  if (t.weightKg != null) parts.push(`${t.weightKg} kg`)
  if (t.soreness != null) parts.push(`Sore ${t.soreness}`)
  if (t.hrvMs != null) parts.push(`HRV ${t.hrvMs}`)
  return parts.join(' · ')
})

function expand() {
  // loadToday may resolve after setup ran, so re-seed from the store on open
  // (mirrors PeriodizationPanel.vue:133).
  form.resetForm({ values: valuesFromStore() })
  formError.value = null
  expanded.value = true
}

function cancel() {
  expanded.value = false
  formError.value = null
}

const onSubmit = form.handleSubmit(async (values) => {
  formError.value = null
  const request: WellnessEntryRequest = {
    sleepHours: values.sleepHours ?? null,
    sleepQuality: values.sleepQuality ?? null,
    restingHr: values.restingHr ?? null,
    weightKg: values.weightKg ?? null,
    soreness: values.soreness ?? null,
    hrvMs: values.hrvMs ?? null,
    notes: values.notes ?? null,
  }

  try {
    await store.saveToday(request)
    expanded.value = false
  } catch (e) {
    const messages = extractApiValidationMessages(e)
    if (!messages) {
      formError.value = "Couldn't save that. Try again."
      return
    }
    const unmapped: string[] = []
    for (const message of messages) {
      const colon = message.indexOf(':')
      const field = colon > 0 ? SERVER_FIELD_MAP[message.slice(0, colon)] : undefined
      if (field) {
        form.setFieldError(field, message)
      } else {
        unmapped.push(message)
      }
    }
    if (unmapped.length > 0) {
      formError.value = unmapped.join(' ')
    }
  }
})
</script>

<template>
  <div class="card-surface p-6">
    <h3 class="eyebrow">Today</h3>

    <!-- Collapsed: the prompt (no entry) or the summary line (entry exists). -->
    <div v-if="!expanded" class="mt-3 flex flex-wrap items-center justify-between gap-3">
      <p v-if="store.today" class="font-mono text-sm">{{ summaryLine }}</p>
      <p v-else class="text-sm text-muted-foreground">No wellness logged today.</p>
      <Button type="button" variant="outline" size="sm" @click="expand">
        {{ store.today ? 'Edit' : 'Log today' }}
      </Button>
    </div>

    <!-- Expanded: the entry form. Field order is the entry order the store and the tiles use. -->
    <form v-else class="mt-3 space-y-4" @submit="onSubmit">
      <FormField v-slot="{ componentField }" name="sleepHours">
        <FormItem>
          <FormLabel>Sleep (h)</FormLabel>
          <FormControl><Input type="number" step="0.25" v-bind="componentField" /></FormControl>
          <!-- Also renders the at-least-one-metric message (schemas/wellness.ts pins it here). -->
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ value, handleChange }" name="sleepQuality">
        <FormItem>
          <FormLabel>Sleep quality</FormLabel>
          <FormControl>
            <ScaleSelector
              :model-value="(value as number | null) ?? null"
              :max="5"
              :labels="['Poor', 'OK', 'Great']"
              @update:model-value="handleChange"
            />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="restingHr">
        <FormItem>
          <FormLabel>Resting HR (bpm)</FormLabel>
          <FormControl><Input type="number" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="weightKg">
        <FormItem>
          <FormLabel>Weight (kg)</FormLabel>
          <FormControl><Input type="number" step="0.1" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ value, handleChange }" name="soreness">
        <FormItem>
          <FormLabel>Soreness</FormLabel>
          <FormControl>
            <ScaleSelector
              :model-value="(value as number | null) ?? null"
              :max="10"
              :labels="['None', 'Sore', 'Severe']"
              @update:model-value="handleChange"
            />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="hrvMs">
        <FormItem>
          <FormLabel>HRV (ms)</FormLabel>
          <FormControl><Input type="number" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="notes">
        <FormItem>
          <FormLabel>Notes</FormLabel>
          <FormControl><Input v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <p v-if="formError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ formError }}
      </p>

      <div class="flex items-center justify-end gap-3">
        <Button type="button" variant="ghost" size="sm" @click="cancel">Cancel</Button>
        <Button type="submit" :disabled="store.saving">Save</Button>
      </div>
    </form>
  </div>
</template>
