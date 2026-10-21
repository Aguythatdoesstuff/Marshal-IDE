<script setup>
import { onMounted, ref, watch } from 'vue'
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

let editor = null
let area = null

// Helper to load and hydrate nodes/connections from cache data
async function loadCache() {
  try {
    const res = await window.api.getSimplifiedEditorCache(props.filePath)
    if (res && res.success && res.cache) {
      console.log('Loaded drag-drop editor cache for:', props.filePath, res.cache)
      
      // If your cache contains nodes and connections structures, hydrate them here:
      if (editor && res.cache.nodes && Array.isArray(res.cache.nodes)) {
        for (const node of res.cache.nodes) {
          await editor.addNode(node)
        }
      }
      if (editor && res.cache.connections && Array.isArray(res.cache.connections)) {
        for (const conn of res.cache.connections) {
          await editor.addConnection(conn)
        }
      }
    }
  } catch (err) {
    console.error('Failed to fetch drag-drop editor cache:', err)
  }
}

// Function to save current editor state to cache
async function saveCache() {
  if (!editor) return
  try {
    const nodes = editor.getNodes()
    const connections = editor.getConnections()
    const cacheData = { nodes, connections }

    const res = await window.api.saveSimplifiedEditorCache({
      filePath: props.filePath,
      cacheData
    })

    if (!res || !res.success) {
      console.error('Failed to save drag-drop editor cache:', res?.message)
    }
  } catch (err) {
    console.error('Error saving drag-drop editor cache:', err)
  }
}

onMounted(async () => {
  const el = container.value
  
  // 1. Initialize Rete Node Editor and Plugins
  editor = new NodeEditor()
  area = new AreaPlugin(el)
  const connection = new ConnectionPlugin()
  const render = new VuePlugin()

  // 2. Setup standard classic presets
  render.addPreset(VuePresets.classic.setup())
  connection.addPreset(ConnectionPresets.classic.setup())

  // Attach plugins to the editor pipeline
  editor.use(area)
  area.use(connection)
  area.use(render)

  // 3. Add Custom Rete Grid Background
  const background = document.createElement('div')
  background.classList.add('rete-grid-background', 'rete-fill-area')
  area.area.content.add(background)

  // 4. Fetch and apply UI Cache
  console.log('Fetching drag-drop editor cache for:', props.filePath)
  await loadCache()

  editorReady.value = true
})

// Watcher to save cache when component unmounts or filePath changes
watch(() => props.filePath, async (newPath, oldPath) => {
  if (oldPath && editorReady.value) {
    await saveCache()
  }
})
</script>

<template>
  <div class="flex flex-col h-full w-full select-none" style="background-color: var(--vscode-editor-background, #1e1e1e); color: var(--vscode-editor-foreground, #bbbbbb);">

    <!-- Rete Canvas Container -->
    <div class="relative flex-1 w-full h-full overflow-hidden">
      <!-- Loading indicator until canvas mounts -->
      <div v-if="!editorReady" class="absolute inset-0 flex items-center justify-center z-20" style="color: var(--vscode-descriptionForeground, #858585);">
        Loading Node Canvas...
      </div>
      
      <!-- Actual DOM element Rete mounts onto -->
      <div ref="container" class="w-full h-full cursor-grab active:cursor-grabbing rete-theme-vscode"></div>
    </div>
  </div>
</template>

<style scoped>
.rete-fill-area {
  display: table;
  z-index: -1;
  position: absolute;
  top: -320000px;
  left: -320000px;
  width: 640000px;
  height: 640000px;
  pointer-events: none;
}

.rete-grid-background {
  background-color: var(--vscode-editor-background, #1e1e1e);
  background-image: 
    linear-gradient(90deg, var(--border-color) 1px, transparent 1px),
    linear-gradient(0deg, var(--border-color) 1px, transparent 1px);
  background-size: 32px 32px;
}

.rete-theme-vscode {
  --background: var(--vscode-editor-background, #1e1e1e);
  --node-color: var(--vscode-editorWidget-background, #252526);
  --node-border: var(--border-color);
  --connection-color: var(--border-color);
}

.rete-theme-vscode :deep(.node) {
  background: var(--node-color);
  border: 1px solid var(--node-border);
  color: var(--vscode-editor-foreground, #bbbbbb);
  border-radius: 4px;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

.rete-theme-vscode :deep(.node.selected) {
  border-color: var(--primary-blue);
}

.rete-theme-vscode :deep(.connection) path {
  stroke: var(--primary-blue);
  stroke-width: 2px;
}

.rete-theme-vscode :deep(.socket) {
  background: var(--vscode-editorWidget-background, #252526);
  border: 2px solid var(--border-color);
}

.rete-theme-vscode :deep(.socket:hover) {
  border-color: var(--primary-blue);
  background: var(--primary-blue);
}
</style>