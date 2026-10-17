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

  // 3. Setup standard classic presets
  render.addPreset(VuePresets.classic.setup())
  connection.addPreset(ConnectionPresets.classic.setup())

  // Attach plugins to the editor pipeline
  editor.use(area)
  area.use(connection)
  area.use(render)

  // 4. Add Custom Rete Grid Background (matching the Rete.js expected pattern)
  const background = document.createElement('div')
  background.classList.add('rete-grid-background', 'rete-fill-area')
  area.area.content.add(background)

  editorReady.value = true
})
</script>

<template>
  <div class="flex flex-col h-screen w-full select-none" style="background-color: var(--vscode-editor-background, #1e1e1e); color: var(--vscode-editor-foreground, #bbbbbb);">
    <!-- Top Toolbar with VSCode Variables -->
    <header class="flex items-center justify-between px-6 py-4 border-b shadow-md z-10" style="background-color: var(--vscode-editorGroupHeader-tabsBackground, #252526); border-color: var(--border-color);">
      <div>
        <h1 class="text-lg font-bold tracking-wide" style="color: var(--vscode-editor-foreground, #ffffff);">Marshal Script Visual Builder</h1>
        <p class="text-xs" style="color: var(--vscode-descriptionForeground, #858585);">Editing: {{ filePath }}</p>
      </div>
    </header>

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
/* Rete.js Native Grid Background Styles using VSCode variables */
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

/* Rete VSCode Theme Customizations */
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