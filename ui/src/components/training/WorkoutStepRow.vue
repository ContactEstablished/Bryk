<script setup lang="ts">
import { computed, ref } from 'vue'
import { Button } from '@/components/ui/button'
import { FormField, FormItem, FormLabel, FormControl, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useZonesStore } from '@/stores/zones'
import type { PlannedSport } from '@/types/training'

const props = defineProps<{
  blockIndex: number
  stepIndex: number
  sport: PlannedSport
  setField: (path: string, value: unknown) => void
}>()
const emit = defineEmits<{ remove: [] }>()

const zonesStore = useZonesStore()

const isStrength = computed(() => props.sport === 'Strength')
const showPower = computed(() => props.sport === 'Bike' || props.sport === 'Triathlon')
const showPace = computed(() => ['Run', 'Swim', 'Triathlon'].includes(props.sport))

function path(field: string): string {
  return `blocks[${props.blockIndex}].steps[${props.stepIndex}].${field}`
}

const intentOptions = [
  { value: 'Warmup', label: 'Warm-up' },
  { value: 'Work', label: 'Work' },
  { value: 'Recovery', label: 'Recovery' },
  { value: 'Cooldown', label: 'Cool-down' },
  { value: 'Rest', label: 'Rest' },
]

// Zone picker, scoped to this workout's sport (bike power / run-swim pace). Picking a zone
// pre-fills the primary-metric raw range from the athlete's computed zones (editable after).
const sportZones = computed(() => zonesStore.zones?.sports.find((s) => s.sport === props.sport))
const zoneOptions = computed(() =>
  (sportZones.value?.zones ?? []).map((z) => ({ value: String(z.zoneNumber), label: `Z${z.zoneNumber}` })),
)
const pickedZone = ref('')

function onPickZone(value: unknown) {
  pickedZone.value = String(value)
  const zone = sportZones.value?.zones.find((z) => z.zoneNumber === Number(value))
  if (!zone) return
  props.setField(path('targetZone'), zone.zoneNumber)
  if (sportZones.value?.metric === 'Power') {
    props.setField(path('targetPowerLow'), zone.lowerBound)
    props.setField(path('targetPowerHigh'), zone.upperBound)
  } else if (sportZones.value?.metric === 'Pace') {
    props.setField(path('targetPaceLow'), zone.lowerBound)
    props.setField(path('targetPaceHigh'), zone.upperBound)
  }
}
</script>

<template>
  <div class="rounded-md border border-dashed p-3 space-y-3">
    <div class="flex items-center justify-between">
      <span class="text-xs font-medium text-muted-foreground">Step {{ stepIndex + 1 }}</span>
      <Button type="button" variant="ghost" size="sm" @click="emit('remove')">Remove</Button>
    </div>

    <FormField v-slot="{ componentField }" :name="path('intent')">
      <FormItem>
        <FormLabel class="text-xs">Intent</FormLabel>
        <Select v-bind="componentField">
          <FormControl>
            <SelectTrigger><SelectValue placeholder="Select" /></SelectTrigger>
          </FormControl>
          <SelectContent>
            <SelectItem v-for="opt in intentOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </SelectItem>
          </SelectContent>
        </Select>
        <FormMessage />
      </FormItem>
    </FormField>

    <!-- Strength: sets / reps / load / RPE -->
    <div v-if="isStrength" class="grid grid-cols-2 gap-3 sm:grid-cols-4">
      <FormField v-slot="{ componentField }" :name="path('sets')">
        <FormItem>
          <FormLabel class="text-xs">Sets</FormLabel>
          <FormControl><Input type="number" min="1" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>
      <FormField v-slot="{ componentField }" :name="path('reps')">
        <FormItem>
          <FormLabel class="text-xs">Reps</FormLabel>
          <FormControl><Input type="number" min="1" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>
      <FormField v-slot="{ componentField }" :name="path('loadKg')">
        <FormItem>
          <FormLabel class="text-xs">Load (kg)</FormLabel>
          <FormControl><Input type="number" step="0.01" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>
      <FormField v-slot="{ componentField }" :name="path('rpe')">
        <FormItem>
          <FormLabel class="text-xs">RPE</FormLabel>
          <FormControl><Input type="number" step="0.1" v-bind="componentField" /></FormControl>
          <FormMessage />
        </FormItem>
      </FormField>
    </div>

    <!-- Cardio: duration/distance + zone picker + raw target ranges -->
    <template v-else>
      <div class="grid grid-cols-2 gap-3">
        <FormField v-slot="{ componentField }" :name="path('durationSeconds')">
          <FormItem>
            <FormLabel class="text-xs">Duration (sec)</FormLabel>
            <FormControl><Input type="number" min="1" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" :name="path('distanceMeters')">
          <FormItem>
            <FormLabel class="text-xs">Distance (m)</FormLabel>
            <FormControl><Input type="number" min="1" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
      </div>
      <p class="text-[11px] text-muted-foreground">Set exactly one of duration or distance.</p>

      <div v-if="zoneOptions.length > 0" class="space-y-1">
        <span class="text-xs font-medium">Zone (pre-fills the range)</span>
        <Select :model-value="pickedZone" @update:model-value="onPickZone">
          <SelectTrigger><SelectValue placeholder="Pick a zone" /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="opt in zoneOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div v-if="showPower" class="grid grid-cols-2 gap-3">
        <FormField v-slot="{ componentField }" :name="path('targetPowerLow')">
          <FormItem>
            <FormLabel class="text-xs">Power low (W)</FormLabel>
            <FormControl><Input type="number" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" :name="path('targetPowerHigh')">
          <FormItem>
            <FormLabel class="text-xs">Power high (W)</FormLabel>
            <FormControl><Input type="number" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
      </div>

      <div v-if="showPace" class="grid grid-cols-2 gap-3">
        <FormField v-slot="{ componentField }" :name="path('targetPaceLow')">
          <FormItem>
            <FormLabel class="text-xs">Pace low (sec)</FormLabel>
            <FormControl><Input type="number" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" :name="path('targetPaceHigh')">
          <FormItem>
            <FormLabel class="text-xs">Pace high (sec)</FormLabel>
            <FormControl><Input type="number" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
      </div>

      <div class="grid grid-cols-2 gap-3">
        <FormField v-slot="{ componentField }" :name="path('targetHrLow')">
          <FormItem>
            <FormLabel class="text-xs">HR low (bpm)</FormLabel>
            <FormControl><Input type="number" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
        <FormField v-slot="{ componentField }" :name="path('targetHrHigh')">
          <FormItem>
            <FormLabel class="text-xs">HR high (bpm)</FormLabel>
            <FormControl><Input type="number" v-bind="componentField" /></FormControl>
            <FormMessage />
          </FormItem>
        </FormField>
      </div>
    </template>
  </div>
</template>
