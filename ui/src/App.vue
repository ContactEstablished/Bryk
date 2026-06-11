<script setup lang="ts">
</script>

<template>
  <!-- Route transition classes live in style.css (.page-*); the global
       reduced-motion override disables them for users who opt out.
       The keyed wrapper div guarantees Transition a single element child —
       views with root-level comments/branches compile to fragments, which
       <Transition> cannot animate reliably. -->
  <RouterView v-slot="{ Component, route }">
    <Transition name="page" mode="out-in">
      <div :key="route.path">
        <component :is="Component" />
      </div>
    </Transition>
  </RouterView>
</template>
