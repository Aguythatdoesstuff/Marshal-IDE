<script setup>
import { onMounted, ref } from 'vue'
import { NodeEditor, ClassicPreset } from 'rete'
import { AreaPlugin } from 'rete-area-plugin'
import { ConnectionPlugin, Presets as ConnectionPresets } from 'rete-connection-plugin'
import { VuePlugin, Presets as VuePresets } from 'rete-vue-plugin'

const props = defineProps({
  filePath: {
    type: String,
    required: true
  },
  content: {
    type: String,
    default: ''
  }
})

const emit = defineEmits(['update:content'])

const container = ref(null)
const editorReady = ref(false)

onMounted(async () => {
  const el = container.value
  
  // 1. Fetch UI Cache for the specific file
  try {
    const res = await window.api.getSimplifiedEditorCache(props.filePath)
    if (res && res.success && res.cache) {
      console.log('Loaded drag-drop editor cache for:', props.filePath, res.cache)
      // TODO: Hydrate nodes/connections from cache data if available
    }
  } catch (err) {
    console.error('Failed to fetch drag-drop editor cache:', err)
  }

  // 2. Initialize Rete Node Editor and Plugins
  const editor = new NodeEditor()
  const area = new AreaPlugin(el)
  const connection = new ConnectionPlugin()
  const render = new VuePlugin()

  // 3. Setup standard classic presets for UI rendering
  render.addPreset(VuePresets.classic.setup())
  connection.addPreset(ConnectionPresets.classic.setup())

  // Attach plugins to the editor pipeline
  editor.use(area)
  area.use(connection)
  area.use(render)

  editorReady.value = true
})
</script>

<template>
  <div class="flex flex-col h-screen w-full bg-slate-900 text-slate-100 select-none">
    <!-- Top Toolbar with Tailwind Styling -->
    <header class="flex items-center justify-between px-6 py-4 bg-slate-800 border-b border-slate-700 shadow-md">
      <div>
        <h1 class="text-lg font-bold tracking-wide text-indigo-400">Marshal Script Visual Builder</h1>
        <p class="text-xs text-slate-400">Editing: {{ filePath }}</p>
      </div>
    </header>

    <!-- Rete Canvas Container -->
    <div class="relative flex-1 w-full h-full overflow-hidden bg-slate-950">
      <!-- Loading indicator until canvas mounts -->
      <div v-if="!editorReady" class="absolute inset-0 flex items-center justify-center text-slate-500">
        Loading Node Canvas...
      </div>
      
      <!-- Actual DOM element Rete mounts onto -->
      <div ref="container" class="w-full h-full cursor-grab active:cursor-grabbing"></div>
    </div>
  </div>
</template>

<style>
/* Essential Rete CSS overrides to blend cleanly with Tailwind dark mode surfaces */
.rete-container {
  width: 100%;
  height: 100%;
}
[data-testid="node"] {
  background: #1e293b !important;
  border: 1px solid #334155 !important;
  color: #f8fafc !important;
  border-radius: 0.5rem !important;
  padding: 8px !important;
  box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.5);
  min-width: 180px;
}
[data-testid="node"].selected {
  border-color: #6366f1 !important;
}
[data-testid="input-control"] input {
  background: #0f172a !important;
  border: 1px solid #475569 !important;
  color: #f8fafc !important;
  padding: 4px 8px;
  border-radius: 4px;
  width: 100%;
  font-size: 0.85rem;
}
.connection path {
  stroke: #6366f1 !important;
  stroke-width: 3px !important;
}
.socket {
  background: #38bdf8 !important;
  border: 2px solid #bae6fd !important;
}
</style>