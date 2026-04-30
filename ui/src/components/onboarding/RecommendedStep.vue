<script setup lang="ts">
import { Button } from '@/components/ui/button'

defineEmits<{ next: [] }>()
</script>

<template>
  <div class="text-center">
    <h3 class="text-2xl font-semibold">Recommended Profile</h3>
    <p class="mt-2 text-muted-foreground">
      Set your heart rate zones and sport-specific thresholds so your workouts are calibrated to your physiology.
    </p>
    <p class="mt-6 text-sm text-muted-foreground/60">Form fields coming in the next prompt.</p>
    <Button class="mt-8" @click="$emit('next')">Continue</Button>
  </div>
</template>
