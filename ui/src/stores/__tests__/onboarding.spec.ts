import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOnboardingStore } from '@/stores/onboarding'

describe('useOnboardingStore — nextIncompleteStep', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it("returns 'required' when no flags are set (status null)", () => {
    const store = useOnboardingStore()
    expect(store.nextIncompleteStep).toBe('required')
  })

  it("returns 'recommended' when only required is complete", () => {
    const store = useOnboardingStore()
    store.status = {
      requiredComplete: true,
      recommendedComplete: false,
      goalsComplete: false,
    }
    expect(store.nextIncompleteStep).toBe('recommended')
  })

  it("returns 'goals' when required and recommended are complete", () => {
    const store = useOnboardingStore()
    store.status = {
      requiredComplete: true,
      recommendedComplete: true,
      goalsComplete: false,
    }
    expect(store.nextIncompleteStep).toBe('goals')
  })

  it("returns 'done' when all flags are complete", () => {
    const store = useOnboardingStore()
    store.status = {
      requiredComplete: true,
      recommendedComplete: true,
      goalsComplete: true,
    }
    expect(store.nextIncompleteStep).toBe('done')
  })
})
