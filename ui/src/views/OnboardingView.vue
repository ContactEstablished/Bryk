<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, Check, Lock } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { useOnboardingStore, type OnboardingStep } from '@/stores/onboarding'
import RequiredStep from '@/components/onboarding/RequiredStep.vue'
import RecommendedStep from '@/components/onboarding/RecommendedStep.vue'
import GoalsStep from '@/components/onboarding/GoalsStep.vue'

const store = useOnboardingStore()
const router = useRouter()
const currentStep = ref<OnboardingStep>('required')

onMounted(async () => {
  await store.loadStatus()
  if (store.status) {
    currentStep.value = store.nextIncompleteStep
  }
})

type StepDefinition = { id: Exclude<OnboardingStep, 'done'>; label: string }

const stepDefinitions: StepDefinition[] = [
  { id: 'required', label: 'Required' },
  { id: 'recommended', label: 'Recommended' },
  { id: 'goals', label: 'Goals' },
]

const stepOrder = computed(
  () => stepDefinitions.map((s) => s.id) as Array<OnboardingStep>,
)

function isCompleted(stepId: OnboardingStep) {
  if (stepId === 'required') return store.requiredComplete
  if (stepId === 'recommended') return store.recommendedComplete
  if (stepId === 'goals') return store.goalsComplete
  return false
}

function isActive(stepId: OnboardingStep) {
  return currentStep.value === stepId
}

function canJumpTo(stepId: OnboardingStep) {
  return isCompleted(stepId) || stepId === store.nextIncompleteStep
}

function isLocked(stepId: OnboardingStep) {
  return !canJumpTo(stepId)
}

function jumpTo(stepId: OnboardingStep) {
  if (!canJumpTo(stepId)) return
  currentStep.value = stepId
}

function circleClass(stepId: OnboardingStep) {
  if (isCompleted(stepId)) return 'bg-primary text-primary-foreground'
  if (isActive(stepId)) return 'bg-primary text-primary-foreground ring-2 ring-ring ring-offset-2 ring-offset-background'
  return 'bg-muted text-muted-foreground'
}

function labelClass(stepId: OnboardingStep) {
  if (isActive(stepId)) return 'text-foreground font-medium'
  if (isCompleted(stepId)) return 'text-foreground'
  return 'text-muted-foreground'
}

function goBack() {
  const i = stepOrder.value.indexOf(currentStep.value)
  if (i > 0) currentStep.value = stepOrder.value[i - 1]
}

function handleStepNext() {
  currentStep.value = store.nextIncompleteStep
}

function goHome() {
  void router.push('/')
}

function goToProfile() {
  void router.push('/profile')
}
</script>

<template>
  <div class="flex min-h-screen flex-col items-center justify-start px-4 py-16">
    <!-- Heading -->
    <h1 class="text-6xl font-bold text-white">Bryk</h1>

    <!-- Loading -->
    <p
      v-if="store.loadingStatus || (!store.status && !store.error)"
      class="mt-16 text-lg text-muted-foreground"
    >
      Loading…
    </p>

    <!-- Error -->
    <div
      v-else-if="store.error && !store.status"
      class="mt-16 max-w-lg text-center"
    >
      <p class="text-destructive">
        Couldn't load onboarding status — check the API and refresh.
      </p>
      <Button class="mt-4" variant="outline" @click="store.loadStatus()">
        Retry
      </Button>
    </div>

    <!-- All set -->
    <div
      v-else-if="store.status && currentStep === 'done'"
      class="mt-16 flex flex-col items-center gap-4"
    >
      <h2 class="text-3xl font-semibold text-white">You're all set!</h2>
      <p class="text-center text-muted-foreground">
        Your profile is ready — you can update any of this later.
      </p>
      <div class="flex gap-3">
        <Button @click="goHome">
          Go to home
        </Button>
        <Button variant="outline" @click="goToProfile">
          Manage your profile
        </Button>
      </div>
    </div>

    <!-- Wizard -->
    <div
      v-else-if="store.status"
      class="mt-12 w-full max-w-3xl"
    >
      <!-- Stepper (hand-rolled) -->
      <div class="rounded-lg border bg-card p-6">
        <ol class="flex items-center">
          <template v-for="(step, idx) in stepDefinitions" :key="step.id">
            <li>
              <button
                type="button"
                :disabled="isLocked(step.id)"
                :aria-current="isActive(step.id) ? 'step' : undefined"
                :aria-label="`${step.label}${isLocked(step.id) ? ' (locked)' : ''}`"
                :class="[
                  'flex items-center gap-3 rounded-md focus:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  isLocked(step.id)
                    ? 'cursor-not-allowed opacity-50'
                    : 'cursor-pointer',
                ]"
                @click="jumpTo(step.id)"
              >
                <span
                  :class="[
                    'flex h-8 w-8 items-center justify-center rounded-full text-sm font-semibold transition-colors',
                    circleClass(step.id),
                  ]"
                >
                  <Check v-if="isCompleted(step.id)" :size="16" />
                  <Lock v-else-if="isLocked(step.id)" :size="14" />
                  <span v-else>{{ idx + 1 }}</span>
                </span>
                <span :class="['text-sm', labelClass(step.id)]">{{ step.label }}</span>
              </button>
            </li>
            <div
              v-if="idx < stepDefinitions.length - 1"
              class="mx-4 h-px flex-1 bg-border"
            />
          </template>
        </ol>
      </div>

      <!-- Step body -->
      <div class="mt-8 rounded-lg border bg-card p-6">
        <RequiredStep
          v-if="currentStep === 'required'"
          @next="handleStepNext"
        />
        <RecommendedStep
          v-else-if="currentStep === 'recommended'"
          @next="handleStepNext"
        />
        <GoalsStep
          v-else-if="currentStep === 'goals'"
          @next="handleStepNext"
        />
      </div>

      <!-- Back button -->
      <div class="mt-6">
        <Button
          variant="outline"
          :disabled="currentStep === 'required'"
          @click="goBack"
        >
          <ArrowLeft class="mr-1" :size="16" />
          Back
        </Button>
      </div>
    </div>
  </div>
</template>
