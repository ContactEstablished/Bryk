<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Button } from '@/components/ui/button'
import {
  Stepper,
  StepperIndicator,
  StepperItem,
  StepperSeparator,
  StepperTitle,
  StepperTrigger,
} from '@/components/ui/stepper'
import { useOnboardingStore, type OnboardingStep } from '@/stores/onboarding'
import RequiredStep from '@/components/onboarding/RequiredStep.vue'
import RecommendedStep from '@/components/onboarding/RecommendedStep.vue'
import GoalsStep from '@/components/onboarding/GoalsStep.vue'
import { ArrowLeft } from 'lucide-vue-next'

const store = useOnboardingStore()
const currentStep = ref<OnboardingStep>('required')

onMounted(async () => {
  await store.loadStatus()
  if (store.status) {
    currentStep.value = store.nextIncompleteStep
  }
})

const stepToNumber: Record<string, number> = {
  required: 1,
  recommended: 2,
  goals: 3,
  done: 0,
}

const activeStep = computed(() => stepToNumber[currentStep.value])

function goBack() {
  if (currentStep.value === 'goals') {
    currentStep.value = 'recommended'
  } else if (currentStep.value === 'recommended') {
    currentStep.value = 'required'
  }
}

function handleStepNext() {
  if (currentStep.value === 'required') {
    currentStep.value = 'recommended'
  } else if (currentStep.value === 'recommended') {
    currentStep.value = 'goals'
  } else if (currentStep.value === 'goals') {
    currentStep.value = 'done'
  }
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
      <p class="text-xl font-semibold">You're all set!</p>
    </div>

    <!-- Wizard -->
    <div
      v-else-if="store.status"
      class="mt-12 w-full max-w-3xl"
    >
      <!-- Stepper -->
      <div class="rounded-lg border bg-card p-6">
        <Stepper :model-value="activeStep">
          <StepperItem
            :step="1"
            :completed="store.requiredComplete"
          >
            <StepperTrigger as="div">
              <StepperIndicator>1</StepperIndicator>
              <StepperTitle>Required</StepperTitle>
            </StepperTrigger>
          </StepperItem>

          <StepperSeparator />

          <StepperItem
            :step="2"
            :completed="store.recommendedComplete"
          >
            <StepperTrigger as="div">
              <StepperIndicator>2</StepperIndicator>
              <StepperTitle>Recommended</StepperTitle>
            </StepperTrigger>
          </StepperItem>

          <StepperSeparator />

          <StepperItem
            :step="3"
            :completed="store.goalsComplete"
          >
            <StepperTrigger as="div">
              <StepperIndicator>3</StepperIndicator>
              <StepperTitle>Goals</StepperTitle>
            </StepperTrigger>
          </StepperItem>
        </Stepper>
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
