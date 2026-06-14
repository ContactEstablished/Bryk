<script setup lang="ts">
// Reusable segmented range control (PMC 6w/3m/6m, Load 8/12/26-week, time-in-zone sport). Styled like the
// ZonesView sport toggle. Presentational — the parent owns the value (and any URL sync).
defineProps<{
  modelValue: string
  options: { value: string; label: string }[]
  ariaLabel?: string
}>()

defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div
    role="tablist"
    :aria-label="ariaLabel"
    class="inline-flex gap-0.5 rounded-[10px] border border-border-strong bg-[#0d1015] p-0.5"
  >
    <button
      v-for="opt in options"
      :key="opt.value"
      type="button"
      role="tab"
      :aria-selected="opt.value === modelValue"
      class="rounded-lg px-3 py-1.5 text-[13px] font-medium transition-colors duration-[120ms]"
      :class="
        opt.value === modelValue
          ? 'bg-raised text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]'
          : 'text-subtle hover:text-foreground'
      "
      @click="$emit('update:modelValue', opt.value)"
    >
      {{ opt.label }}
    </button>
  </div>
</template>
