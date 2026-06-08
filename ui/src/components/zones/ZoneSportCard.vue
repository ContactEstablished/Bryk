<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { CheckCircle2, RotateCcw } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { useZonesStore } from '@/stores/zones'
import { ApiError } from '@/services/api'
import { sportZonesFormSchema, type SportZonesFormValues } from '@/schemas/zones'
import type { SportZonesResponse } from '@/types/zones'

const props = defineProps<{ sport: SportZonesResponse }>()

const store = useZonesStore()

const metricUnit: Record<string, string> = { Power: 'watts', Pace: 'sec', Hr: 'bpm' }

function toFormValues(data: SportZonesResponse): SportZonesFormValues {
  return {
    zones: data.zones.map((z) => ({
      lowerBound: z.lowerBound,
      upperBound: z.upperBound,
    })),
  }
}

const form = useForm<SportZonesFormValues>({
  validationSchema: toTypedSchema(sportZonesFormSchema),
  initialValues: toFormValues(props.sport),
})

const globalError = ref<string | null>(null)
const justSaved = ref(false)

// Reseed when the store reloads this sport after a save or reset.
watch(
  () => props.sport,
  (data) => form.resetForm({ values: toFormValues(data) }),
)

watch(
  () => form.meta.value.dirty,
  (dirty) => {
    if (dirty) justSaved.value = false
  },
)

const isOverridden = computed(() => props.sport.zones.some((z) => z.isOverride))

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  try {
    await store.saveZones(props.sport.sport, {
      zones: values.zones.map((z, i) => ({
        zoneNumber: props.sport.zones[i].zoneNumber,
        lowerBound: z.lowerBound,
        upperBound: z.upperBound ?? null,
      })),
    })
    justSaved.value = true
  } catch (e) {
    globalError.value =
      e instanceof ApiError
        ? `Couldn't save: ${e.statusText} (${e.status})`
        : e instanceof Error
          ? `Couldn't save: ${e.message}`
          : "Couldn't save — please try again."
  }
})

async function onReset() {
  globalError.value = null
  try {
    await store.resetZones(props.sport.sport)
    justSaved.value = false
  } catch (e) {
    globalError.value =
      e instanceof ApiError
        ? `Couldn't reset: ${e.statusText} (${e.status})`
        : "Couldn't reset — please try again."
  }
}

const isSubmitting = form.isSubmitting
</script>

<template>
  <section class="rounded-lg border bg-card p-6">
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-xl font-semibold">{{ props.sport.sport }}</h2>
        <p class="text-sm text-muted-foreground">
          {{ props.sport.zones.length }} zones &middot; {{ metricUnit[props.sport.metric] ?? props.sport.metric }}
        </p>
      </div>
      <span v-if="isOverridden" class="rounded border px-2 py-0.5 text-xs text-muted-foreground">
        Customized
      </span>
    </div>

    <form class="mt-6 space-y-4" @submit="onSubmit">
      <div class="grid grid-cols-[auto_1fr_1fr] items-end gap-x-3 gap-y-1">
        <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Zone</span>
        <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Lower</span>
        <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Upper</span>

        <template v-for="(zone, index) in props.sport.zones" :key="zone.zoneNumber">
          <span class="pb-2 text-sm font-medium">Z{{ zone.zoneNumber }}</span>
          <FormField v-slot="{ componentField }" :name="`zones[${index}].lowerBound`">
            <FormItem>
              <FormControl>
                <Input type="number" step="0.01" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>
          <FormField v-slot="{ componentField }" :name="`zones[${index}].upperBound`">
            <FormItem>
              <FormControl>
                <Input type="number" step="0.01" placeholder="—" v-bind="componentField" />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>
        </template>
      </div>

      <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ globalError }}
      </div>

      <div class="flex items-center justify-end gap-3">
        <span v-if="justSaved" class="flex items-center gap-1 text-sm text-muted-foreground">
          <CheckCircle2 :size="16" class="text-primary" />
          Saved
        </span>
        <Button
          type="button"
          variant="outline"
          :disabled="isSubmitting || !isOverridden"
          @click="onReset"
        >
          <RotateCcw :size="14" class="mr-1" />
          Reset to computed
        </Button>
        <Button type="submit" :disabled="isSubmitting">Save changes</Button>
      </div>
    </form>
  </section>
</template>
