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
    }
  } catch (err) {
    console.error('Failed to fetch drag-drop editor cache:', err)
  }

  // 2. Initialize Rete Node Editor and Plugins
  const editor = new NodeEditor()
  const area = new AreaPlugin(el)
  const connection = new ConnectionPlugin()
  const render = new VuePlugin()

  // 3. Setup standard classic presets
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
  <div class="flex flex-col h-full w-full bg-marshal-editor text-marshal-text select-none">
    <!-- Rete Canvas Container -->
    <div class="relative flex-1 w-full h-full overflow-hidden bg-marshal-editor">
      <!-- Loading indicator until canvas mounts -->
      <div v-if="!editorReady" class="absolute inset-0 flex items-center justify-center text-marshal-muted">
        Loading Node Canvas...
      </div>
      
      <!-- Actual DOM element Rete mounts onto -->
      <div ref="container" class="w-full h-full cursor-grab active:cursor-grabbing rete-workspace"></div>
    </div>
  </div>
</template>

<style scoped>
.flex { display: flex; }
.flex-col { flex-direction: column; }
.h-full { height: 100%; }
.w-full { width: 100%; }
.select-none { user-select: none; }
.relative { position: relative; }
.flex-1 { flex: 1 1 0%; }
.overflow-hidden { overflow: hidden; }
.absolute { position: absolute; }
.inset-0 { top: 0; right: 0; bottom: 0; left: 0; }
.items-center { align-items: center; }
.justify-center { justify-content: center; }
.cursor-grab { cursor: grab; }
.active\:cursor-grabbing { cursor: grabbing; }

.rete-workspace {
  background-color: var(--marshal-editor, #1e1e1e);
  background-image: 
    linear-gradient(90deg, var(--border-color, #334155) 1px, transparent 1px),
    linear-gradient(0deg, var(--border-color, #334155) 1px, transparent 1px);
  background-size: 32px 32px;
}

.rete-workspace :deep(.node) {
  background: var(--marshal-sidebar, #252526);
  border: 1px solid var(--border-color, #334155);
  color: var(--marshal-text, #f8fafc);
  border-radius: 4px;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

.rete-workspace :deep(.node.selected) {
  border-color: var(--primary-blue);
}

.rete-workspace :deep(.connection) path {
  stroke: var(--primary-blue);
  stroke-width: 2px;
}

.rete-workspace :deep(.socket) {
  background: var(--marshal-sidebar, #252526);
  border: 2px solid var(--border-color, #334155);
}

.rete-workspace :deep(.socket:hover) {
  border-color: var(--primary-blue);
  background: var(--primary-blue);
}
</style>