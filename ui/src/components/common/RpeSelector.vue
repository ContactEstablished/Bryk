<script setup lang="ts">
defineProps<{
  modelValue: number | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: number]
}>()

const values = Array.from({ length: 10 }, (_, i) => i + 1)
</script>

<template>
  <div>
    <div class="grid grid-cols-10 gap-1">
      <button
        v-for="v in values"
        :key="v"
        type="button"
        class="rounded-md border py-2.5 font-mono text-[13px] font-semibold transition-all duration-[120ms]"
        :class="
          modelValue === v
            ? 'border-primary bg-gradient-to-b from-primary to-primary-lo text-primary-foreground shadow-[0_4px_14px_var(--bryk-accent-glow)]'
            : 'border-border-strong bg-[#0d1015] text-subtle hover:border-[#3a4252] hover:text-foreground'
        "
        :aria-pressed="modelValue === v"
        @click="emit('update:modelValue', v)"
      >
        {{ v }}
      </button>
    </div>
    <div
      class="mt-1.5 grid grid-cols-10 gap-1 font-mono text-[9.5px] uppercase tracking-[0.1em] text-faint"
    >
      <span class="col-span-2 text-left">Easy</span>
      <span class="col-span-2 col-start-5 text-center">Steady</span>
      <span class="col-span-2 col-start-9 text-right">Max</span>
    </div>
  </div>
</template>
