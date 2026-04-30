<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Activity } from 'lucide-vue-next'
</script>

<template>
  <div class="flex min-h-screen flex-col items-center justify-center gap-8">
    <h1 class="text-6xl font-bold text-white">Bryk</h1>
    <Button>
      <Activity />
      Get started
    </Button>
  </div>
</template>
