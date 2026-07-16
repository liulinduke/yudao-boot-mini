<template>
  <div class="message-manager-launcher" />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'

onMounted(async () => {
  const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
  if (bridge?.OpenMessageManagerWindow) bridge.OpenMessageManagerWindow()
})
</script>

<style scoped>
.message-manager-launcher {
  display: none;
}
</style>
