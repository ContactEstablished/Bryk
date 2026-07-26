<script setup lang="ts">
import { computed, ref, watch } from 'vue'
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
import { useGoalsStore } from '@/stores/goals'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { eventItemSchema } from '@/schemas/onboarding'
import type { EventDto } from '@/types/onboarding'
import type { EventListItem } from '@/types/goals'

// One event's form on the Goals page. `event` present = existing row (Save updates, Delete removes
// server-side); absent = unsaved draft (Save creates, Remove discards locally). Mirrors
// ProfileEventCard's field set and validation so both surfaces behave identically; the plan link
// (event.linkedPlans) is display-only and is never edited here.
const props = defineProps<{ event?: EventListItem | null }>()
const emit = defineEmits<{ remove: []; created: [] }>()

const store = useGoalsStore()

interface EventFormItem {
  name: string
  eventDate: string
  sport: 'Swim' | 'Bike' | 'Run' | 'Triathlon' | null
  triathlonDistance: 'Sprint' | 'Olympic' | 'HalfIronman' | 'Ironman' | 'Custom' | null
  customDistanceName: string | null
  priority: 'A' | 'B' | 'C'
  notes: string | null
}

const isDraft = computed(() => props.event == null)

function toFormItem(e: EventListItem): EventFormItem {
  return {
    name: e.name,
    eventDate: e.eventDate,
    sport: e.sport,
    triathlonDistance: e.triathlonDistance,
    customDistanceName: e.customDistanceName,
    priority: e.priority,
    notes: e.notes,
  }
}

function emptyEvent(): EventFormItem {
  return {
    name: '',
    eventDate: '',
    sport: null,
    triathlonDistance: null,
    customDistanceName: null,
    priority: 'B',
    notes: null,
  }
}

const form = useForm<EventFormItem>({
  validationSchema: toTypedSchema(eventItemSchema),
  initialValues: props.event ? toFormItem(props.event) : emptyEvent(),
})

const globalError = ref<string | null>(null)
const justSaved = ref(false)
const deleting = ref(false)

const sportOptions = [
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

function setError(e: unknown) {
  const messages = extractApiValidationMessages(e)
  if (messages) {
    globalError.value = messages.join(' ')
  } else if (e instanceof ApiError) {
    globalError.value = e.status === 404
      ? 'This event no longer exists — it may have been removed.'
      : `Couldn't save: ${e.statusText} (${e.status})`
  } else if (e instanceof Error) {
    globalError.value = `Couldn't save: ${e.message}`
  } else {
    globalError.value = "Couldn't save — please try again."
  }
}

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  const dto: EventDto = {
    name: values.name,
    eventDate: values.eventDate,
    sport: values.sport,
    triathlonDistance: values.sport === 'Triathlon' ? values.triathlonDistance : null,
    customDistanceName: values.triathlonDistance === 'Custom' ? values.customDistanceName : null,
    priority: values.priority,
    notes: values.notes ?? null,
  }
  try {
    if (props.event) {
      await store.updateEvent(props.event.id, dto)
      // Re-baseline so the form is pristine and "Saved" persists until the next edit.
      form.resetForm({ values: dto })
      justSaved.value = true
    } else {
      await store.createEvent(dto)
      // The new row now comes from the store; ask the parent to drop this draft.
      emit('created')
    }
  } catch (e) {
    setError(e)
  }
})

async function onDelete() {
  if (!props.event) return
  globalError.value = null
  deleting.value = true
  try {
    await store.deleteEvent(props.event.id)
    // The re-fetch drops this row from the list, unmounting the form.
  } catch (e) {
    deleting.value = false
    setError(e)
  }
}

watch(
  () => form.meta.value.dirty,
  (dirty) => {
    if (dirty) justSaved.value = false
  },
)

const isSubmitting = form.isSubmitting
</script>

<template>
  <div class="rounded-md border p-4 space-y-4">
    <div class="flex items-center justify-between">
      <span class="text-sm font-medium">{{ isDraft ? 'New event' : 'Edit event' }}</span>
      <Button
        v-if="isDraft"
        type="button"
        variant="ghost"
        size="sm"
        @click="emit('remove')"
      >
        Remove
      </Button>
      <Button
        v-else
        type="button"
        variant="ghost"
        size="sm"
        :disabled="deleting"
        @click="onDelete"
      >
        Delete
      </Button>
    </div>

    <form class="space-y-4" @submit="onSubmit">
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <FormField v-slot="{ componentField }" name="name">
          <FormItem>
            <FormLabel>Name</FormLabel>
            <FormControl>
              <Input v-bind="componentField" />
            </FormControl>
            <FormMessage />
          </FormItem>
        </FormField>

        <FormField v-slot="{ componentField }" name="eventDate">
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
        <FormField v-slot="{ componentField }" name="sport">
          <FormItem>
            <FormLabel>Sport</FormLabel>
            <Select v-bind="componentField">
              <FormControl>
                <SelectTrigger>
                  <SelectValue placeholder="Select sport (optional)" />
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
          v-if="form.values.sport === 'Triathlon'"
          v-slot="{ componentField }"
          name="triathlonDistance"
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
        v-if="form.values.triathlonDistance === 'Custom'"
        v-slot="{ componentField }"
        name="customDistanceName"
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
        <FormField v-slot="{ componentField }" name="priority">
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

        <FormField v-slot="{ componentField }" name="notes">
          <FormItem>
            <FormLabel>Notes</FormLabel>
            <FormControl>
              <Input v-bind="componentField" />
            </FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
      </div>

      <div v-if="globalError" class="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {{ globalError }}
      </div>

      <div class="flex items-center justify-end gap-3">
        <span v-if="justSaved" class="flex items-center gap-1 text-sm text-muted-foreground">
          <CheckCircle2 :size="16" class="text-primary" />
          Saved
        </span>
        <Button type="submit" :disabled="isSubmitting">Save</Button>
      </div>
    </form>
  </div>
</template>
