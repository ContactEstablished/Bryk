<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { CheckCircle2 } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { useGoalsStore } from '@/stores/goals'
import { ApiError } from '@/services/api'
import { extractApiValidationMessages } from '@/services/apiErrors'
import { goalItemSchema } from '@/schemas/onboarding'
import type { GoalDto } from '@/types/onboarding'
import type { GoalListItem } from '@/types/goals'

// One goal's form on the Goals page. `goal` present = existing row (Save updates, Delete removes
// server-side); absent = unsaved draft (Save creates, Remove discards locally). Mirrors
// ProfileGoalCard's field set and validation.
const props = defineProps<{ goal?: GoalListItem | null }>()
const emit = defineEmits<{ remove: []; created: [] }>()

const store = useGoalsStore()

interface GoalFormItem {
  description: string
  targetDate: string | null
}

const isDraft = computed(() => props.goal == null)

function toFormItem(g: GoalListItem): GoalFormItem {
  return { description: g.description, targetDate: g.targetDate }
}

function emptyGoal(): GoalFormItem {
  return { description: '', targetDate: null }
}

const form = useForm<GoalFormItem>({
  validationSchema: toTypedSchema(goalItemSchema),
  initialValues: props.goal ? toFormItem(props.goal) : emptyGoal(),
})

const globalError = ref<string | null>(null)
const justSaved = ref(false)
const deleting = ref(false)

function setError(e: unknown) {
  const messages = extractApiValidationMessages(e)
  if (messages) {
    globalError.value = messages.join(' ')
  } else if (e instanceof ApiError) {
    globalError.value = e.status === 404
      ? 'This goal no longer exists — it may have been removed.'
      : `Couldn't save: ${e.statusText} (${e.status})`
  } else if (e instanceof Error) {
    globalError.value = `Couldn't save: ${e.message}`
  } else {
    globalError.value = "Couldn't save — please try again."
  }
}

const onSubmit = form.handleSubmit(async (values) => {
  globalError.value = null
  // The editor doesn't expose GoalType; every goal created here is General (Decision 5),
  // matching ProfileGoalCard.
  const dto: GoalDto = {
    type: 'General',
    description: values.description,
    targetDate: values.targetDate || null,
  }
  try {
    if (props.goal) {
      await store.updateGoal(props.goal.id, dto)
      form.resetForm({ values: { description: dto.description, targetDate: dto.targetDate } })
      justSaved.value = true
    } else {
      await store.createGoal(dto)
      emit('created')
    }
  } catch (e) {
    setError(e)
  }
})

async function onDelete() {
  if (!props.goal) return
  globalError.value = null
  deleting.value = true
  try {
    await store.deleteGoal(props.goal.id)
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
      <span class="text-sm font-medium">{{ isDraft ? 'New goal' : 'Edit goal' }}</span>
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
      <FormField v-slot="{ componentField }" name="description">
        <FormItem>
          <FormLabel>Description</FormLabel>
          <FormControl>
            <Input v-bind="componentField" />
          </FormControl>
          <FormMessage />
        </FormItem>
      </FormField>

      <FormField v-slot="{ componentField }" name="targetDate">
        <FormItem>
          <FormLabel>Target Date</FormLabel>
          <FormControl>
            <Input type="date" v-bind="componentField" />
          </FormControl>
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
        <Button type="submit" :disabled="isSubmitting">Save</Button>
      </div>
    </form>
  </div>
</template>
