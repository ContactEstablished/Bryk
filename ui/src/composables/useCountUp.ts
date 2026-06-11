import { onScopeDispose, ref, toValue, watch, type MaybeRefOrGetter, type Ref } from 'vue'
import { usePreferredReducedMotion } from '@vueuse/core'

export interface UseCountUpOptions {
  /** Animation length in ms. */
  duration?: number
  /** Fraction digits in the rendered value. */
  decimals?: number
}

/**
 * Animates a displayed number toward its target with an ease-out cubic ramp.
 * Snaps instantly when the user prefers reduced motion (which the test setup's
 * matchMedia stub reports under jsdom, keeping text assertions synchronous).
 */
export function useCountUp(
  target: MaybeRefOrGetter<number | null | undefined>,
  options: UseCountUpOptions = {},
): Ref<string> {
  const { duration = 600, decimals = 0 } = options
  const display = ref('')
  const motion = usePreferredReducedMotion()
  let frame = 0

  function format(v: number): string {
    return v.toFixed(decimals)
  }

  watch(
    () => toValue(target),
    (value, previous) => {
      cancelAnimationFrame(frame)
      if (value == null) {
        display.value = ''
        return
      }
      if (motion.value === 'reduce') {
        display.value = format(value)
        return
      }
      const from = typeof previous === 'number' ? previous : 0
      const start = performance.now()
      const step = (now: number) => {
        const t = Math.min((now - start) / duration, 1)
        const eased = 1 - Math.pow(1 - t, 3)
        display.value = format(from + (value - from) * eased)
        if (t < 1) frame = requestAnimationFrame(step)
      }
      frame = requestAnimationFrame(step)
    },
    { immediate: true },
  )

  onScopeDispose(() => cancelAnimationFrame(frame))

  return display
}
