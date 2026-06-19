<script setup lang="ts">
defineProps<{
  modelValue: 'month' | 'week'
}>()

defineEmits<{ 'update:modelValue': [value: 'month' | 'week'] }>()
</script>

<template>
  <div
    role="tablist"
    aria-label="Calendar view"
    class="inline-flex gap-0.5 rounded-[10px] border border-border-strong bg-[#0d1015] p-0.5"
  >
    <button
      type="button"
      role="tab"
      :aria-selected="modelValue === 'month'"
      class="rounded-lg px-3 py-1.5 text-[13px] font-medium transition-colors duration-[120ms]"
      :class="
        modelValue === 'month'
          ? 'bg-primary-hi text-primary-foreground'
          : 'text-subtle hover:text-foreground'
      "
      @click="$emit('update:modelValue', 'month')"
    >
      Month
    </button>
    <button
      type="button"
      role="tab"
      :aria-selected="modelValue === 'week'"
      class="rounded-lg px-3 py-1.5 text-[13px] font-medium transition-colors duration-[120ms]"
      :class="
        modelValue === 'week'
          ? 'bg-primary-hi text-primary-foreground'
          : 'text-subtle hover:text-foreground'
      "
      @click="$emit('update:modelValue', 'week')"
    >
      Week
    </button>
  </div>
</template>
