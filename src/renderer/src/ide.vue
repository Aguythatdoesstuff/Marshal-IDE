<template>
  <div :class="['ide-frame', { 'layout-resizing': isResizing }]">
    
    <div class="window-titlebar">
      <span class="title-string">Marshal IDE - Workspace Template Layout</span>
    </div>

    <header class="app-header">
      <div class="meta-section">
        <h1 class="brand-title">Marshal IDE</h1>
        <div class="vertical-divider"></div>
        <span class="active-project-tag">Project: <span class="highlight">{{ activeProjectName }}</span></span>
      </div>
      
      <div class="controls-section">
        <button class="ui-btn primary-action" @click="saveActiveFile" :disabled="!activeTabPath">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M17 3H5C3.89 3 3 3.89 3 5v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>
          Save
        </button>
        <button class="ui-btn standard-action">Exit Workspace</button>
      </div>
    </header>

    <main class="workspace-viewport" id="app-workspace-split-root">
      
      <FileTreePanel 
        ref="fileTreeRef"
        :sidebar-width="sidebarWidth"
        :active-path="activeTabPath"
        @file-selected="handleFileSelection"
        @node-contextmenu="openContextMenu"
        @trigger-create="triggerCreateModal"
      />
      
      <div class="pane-divider-v" @mousedown="startWorkspaceResize"></div>

      <section class="main-editor-console-hub">
        
        <div class="editor-subview-frame">
          <div class="editor-tabs-bar" v-if="openTabs.length > 0">
            <div 
              v-for="tab in openTabs" 
              :key="tab.path" 
              :class="['editor-tab-item', { 'is-active': activeTabPath === tab.path }]"
              @click="setActiveTab(tab.path)"
            >
              <span class="tab-title-text">{{ tab.name }}</span>
              <span v-if="tab.isDirty" class="dirty-indicator-dot">●</span>
              <span class="tab-close-icon-btn" @click.stop="closeTab(tab.path)">×</span>
            </div>
          </div>

          <div id="editor-container" class="monaco-mount-target">
            <div v-if="openTabs.length === 0" class="placeholder-screen">
              <p>Select a structural project file asset from the tree browser configuration map to begin code composition.</p>
            </div>
          </div>
        </div>
        
        <div class="pane-divider-h" @mousedown="startConsoleResize"></div>

        <div class="console-subview-dock" :style="{ height: isConsoleMinimized ? '35px' : consoleHeight + 'px' }">
          <ConsolePanel 
            class="console-panel-component"
            :is-minimized="isConsoleMinimized"
            @toggle-minimize="handleToggleMinimize" 
          />
        </div>

      </section>
    </main>

    <div 
      class="floating-popover-menu" 
      v-if="contextMenu.visible" 
      :style="{ top: contextMenu.y + 'px', left: contextMenu.x + 'px', display: 'block' }"
      v-click-outside="closeContextMenu"
    >
      <div class="popover-row" @click="triggerCreateModal(contextMenu.targetNode?.isDir ? contextMenu.targetNode.path : contextMenu.targetNode?.path.substring(0, contextMenu.targetNode.path.lastIndexOf('/')))">New System Asset Element...</div>
      <div class="popover-row" @click="triggerRenameModal(contextMenu.targetNode)">Rename Selected Reference Path...</div>
      <div class="popover-divider"></div>
      <div class="popover-row alert-action" @click="triggerDeleteAction(contextMenu.targetNode)">Delete Component File Permanently</div>
    </div>

    <div class="modal-system-dimmer" v-if="modal.visible">
      <div class="modal-window-card">
        <h3 class="modal-heading">{{ modal.title }}</h3>
        <p class="modal-body">{{ modal.body }}</p>
        
        <div class="modal-input-container" v-if="modal.mode !== 'confirm'">
          <input type="text" v-model="modal.inputValue" class="modal-text-field" :placeholder="modal.placeholder" @keyup.enter="commitModalAction" />
        </div>
        
        <div class="modal-actions-row">
          <button class="ui-btn standard-action" @click="modal.visible = false">Abort</button>
          <button class="ui-btn primary-action" @click="commitModalAction">Commit Action</button>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount, nextTick } from 'vue';
import ConsolePanel from '../ide/Console.vue';
import FileTreePanel from '../ide/FileTree.vue';

const activeProjectName = ref('Loading...');

onMounted(() => {
  // Sync the UI header text to match the project loaded via localStorage
  activeProjectName.value = localStorage.getItem('marshal_project_to_load') || 'No Active Project';
});

// Isolated layout states
const sidebarWidth = ref(260);
const consoleHeight = ref(240);
const isConsoleMinimized = ref(false);
const isResizing = ref(false);

// Active Tab and Editor Instance Registries
const fileTreeRef = ref(null);
const openTabs = ref([]);
const activeTabPath = ref(null);
let editorInstance = null;
let isUpdatingModelFromState = false;

// Overlays Context System State Management
const contextMenu = ref({ visible: false, x: 0, y: 0, targetNode: null });
const modal = ref({ visible: false, title: '', body: '', placeholder: '', inputValue: '', mode: '', targetNode: null });

// Direct access custom click directive
const vClickOutside = {
  mounted(el, binding) {
    el.clickOutsideEvent = (event) => {
      if (!(el === event.target || el.contains(event.target))) {
        binding.value(event);
      }
    };
    document.addEventListener('mousedown', el.clickOutsideEvent);
  },
  unmounted(el) {
    document.removeEventListener('mousedown', el.clickOutsideEvent);
  }
};

// Monaco Setup Framework Lifecycle Bindings
const initializeMonacoEditor = () => {
  if (!window.monaco) {
    setTimeout(initializeMonacoEditor, 100);
    return;
  }
  const container = document.getElementById('editor-container');
  if (!container) return;

  editorInstance = window.monaco.editor.create(container, {
    theme: 'vs-dark',
    automaticLayout: false, // Turned off to prevent resizing thread collisions
    minimap: { enabled: true },
    fontSize: 13,
    fontFamily: "'Fira Code', 'Cascadia Code', monospace"
  });

  editorInstance.onDidChangeModelContent(() => {
    if (isUpdatingModelFromState) return;
    const currentTab = openTabs.value.find(t => t.path === activeTabPath.value);
    if (currentTab && !currentTab.isDirty) currentTab.isDirty = true;
  });
};

const handleFileSelection = async (node) => {
  const existingTab = openTabs.value.find(t => t.path === node.path);
  if (!existingTab) {
    const result = await window.api.invoke('get-file-content', { filePath: node.path });
    if (result && result.success) {
      openTabs.value.push({ name: node.name, path: node.path, content: result.content, isDirty: false });
    } else return;
  }
  await setActiveTab(node.path);
};

const setActiveTab = async (path) => {
  activeTabPath.value = path;
  const targetTab = openTabs.value.find(t => t.path === path);
  if (!targetTab) return;

  await nextTick();
  if (!editorInstance) initializeMonacoEditor();

  if (editorInstance) {
    isUpdatingModelFromState = true;
    const ext = '.' + path.split('.').pop().toLowerCase();
    let language = 'text';
    if (['.js', '.json'].includes(ext)) language = 'javascript';
    else if (['.event', '.script', '.properties'].includes(ext)) language = 'properties';

    const currentModel = window.monaco.editor.getModel(window.monaco.Uri.file(path));
    if (currentModel) {
      editorInstance.setModel(currentModel);
    } else {
      const newModel = window.monaco.editor.createModel(targetTab.content, language, window.monaco.Uri.file(path));
      editorInstance.setModel(newModel);
    }
    isUpdatingModelFromState = false;
    editorInstance.focus();
  }
};

const closeTab = (path) => {
  const tabIndex = openTabs.value.findIndex(t => t.path === path);
  if (tabIndex === -1) return;

  const model = window.monaco.editor.getModel(window.monaco.Uri.file(path));
  if (model) model.dispose();

  openTabs.value.splice(tabIndex, 1);
  if (activeTabPath.value === path) {
    if (openTabs.value.length > 0) setActiveTab(openTabs.value[Math.max(0, tabIndex - 1)].path);
    else { activeTabPath.value = null; if (editorInstance) editorInstance.setModel(null); }
  }
};

const saveActiveFile = async () => {
  if (!activeTabPath.value || !editorInstance) return;
  const currentTab = openTabs.value.find(t => t.path === activeTabPath.value);
  if (!currentTab) return;

  const codePayload = editorInstance.getValue();
  const response = await window.api.invoke('save-file', { filePath: currentTab.path, content: codePayload });
  if (response && response.success) {
    currentTab.content = codePayload;
    currentTab.isDirty = false;
  }
};

// Global Hotkey Interception Hook (Preventing Save Fighting)
const handleGlobalShortcuts = (e) => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
    e.preventDefault();
    saveActiveFile();
  }
};

// Context Overlay Hooks
const openContextMenu = (e, node) => {
  contextMenu.value = { visible: true, x: e.clientX, y: e.clientY, targetNode: node };
};
const closeContextMenu = () => { contextMenu.value.visible = false; };

const triggerCreateModal = (parentPath = '') => {
  closeContextMenu();
  modal.value = { visible: true, title: 'Create File Asset', body: `Add asset link inside directory path: /Root ${parentPath}`, placeholder: 'filename.script', inputValue: '', mode: 'create', targetNode: parentPath };
};

const triggerRenameModal = (node) => {
  closeContextMenu();
  modal.value = { visible: true, title: 'Rename Asset Reference', body: `Provide target handle key for: ${node.name}`, placeholder: node.name, inputValue: node.name, mode: 'rename', targetNode: node };
};

const triggerDeleteAction = (node) => {
  closeContextMenu();
  modal.value = { visible: true, title: 'Confirm Component Purge', body: `Permanently delete "${node.name}" from storage array?`, placeholder: '', inputValue: '', mode: 'confirm', targetNode: node };
};

const commitModalAction = async () => {
  const { mode, targetNode, inputValue } = modal.value;
  const input = inputValue.trim();

  if (mode === 'create' && input) {
    const finalPath = targetNode ? `${targetNode}/${input}` : input;
    const res = await window.api.invoke('create-file', { filePath: finalPath });
    if (res && res.success) fileTreeRef.value?.refreshTree();
  } else if (mode === 'rename' && input && targetNode) {
    const lastSlash = targetNode.path.lastIndexOf('/');
    const parent = lastSlash !== -1 ? targetNode.path.substring(0, lastSlash + 1) : '';
    const newPath = `${parent}${input}`;
    const res = await window.api.invoke('rename-file', { oldFilePath: targetNode.path, newFilePath: newPath });
    if (res && res.success) {
      const tab = openTabs.value.find(t => t.path === targetNode.path);
      if (tab) { tab.name = input; tab.path = newPath; }
      if (activeTabPath.value === targetNode.path) activeTabPath.value = newPath;
      fileTreeRef.value?.refreshTree();
    }
  } else if (mode === 'confirm' && targetNode) {
    const res = await window.api.invoke('delete-file-or-dir', { path: targetNode.path });
    if (res && res.success) { closeTab(targetNode.path); fileTreeRef.value?.refreshTree(); }
  }
  modal.value.visible = false;
};

onMounted(() => {
  window.addEventListener('keydown', handleGlobalShortcuts);
  window.addEventListener('resize', () => editorInstance?.layout());
});
onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleGlobalShortcuts);
  window.removeEventListener('resize', () => editorInstance?.layout());
  if (editorInstance) editorInstance.dispose();
});


const startWorkspaceResize = (e) => {
  isResizing.value = true;
  const processMouseMove = (moveEvent) => {
    sidebarWidth.value = Math.min(Math.max(moveEvent.clientX, 180), window.innerWidth * 0.6);
    editorInstance?.layout(); // Forces canvas updates in unison with sizing handles
  };
  const processMouseUp = () => {
    isResizing.value = false;
    window.removeEventListener('mousemove', processMouseMove);
    window.removeEventListener('mouseup', processMouseUp);
  };
  window.addEventListener('mousemove', processMouseMove);
  window.addEventListener('mouseup', processMouseUp);
};

const startConsoleResize = (e) => {
  isResizing.value = true;
  const startHeight = consoleHeight.value;
  const startY = e.clientY;

  const processMouseMove = (moveEvent) => {
    const deltaY = moveEvent.clientY - startY;
    const targetHeight = startHeight - deltaY;
    
    // Auto-minimize if dragged lower than 45px threshold height
    if (targetHeight < 45) {
      isConsoleMinimized.value = true;
    } else {
      if (isConsoleMinimized.value) isConsoleMinimized.value = false;
      consoleHeight.value = Math.min(Math.max(targetHeight, 45), window.innerHeight * 0.7);
    }
  };

  const processMouseUp = () => {
    isResizing.value = false;
    window.removeEventListener('mousemove', processMouseMove);
    window.removeEventListener('mouseup', processMouseUp);
  };

  window.addEventListener('mousemove', processMouseMove);
  window.addEventListener('mouseup', processMouseUp);
};

const handleToggleMinimize = () => {
  isConsoleMinimized.value = !isConsoleMinimized.value;
};
</script>

<style lang="scss" scoped>
// Safely inherit alias mappings from main.scss
$sidebar-bg: var(--sidebar-bg);
$editor-bg: var(--editor-bg);
$card-bg: var(--card-bg);
$text-color: var(--text-color);
$primary-blue: var(--primary-blue);
$border-color: var(--border-color);
$text-muted: var(--text-muted);

.ide-frame {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
  overflow: hidden;
  background-color: $sidebar-bg;
  color: $text-color;
  font-family: 'Inter', sans-serif;

  &.layout-resizing {
    cursor: col-resize !important;
    user-select: none !important;

    // Neutralizes Monaco's canvas tracking logic to prevent mouse frame freezing during drags
    .monaco-mount-target {
      pointer-events: none !important;
    }
  }
}

// Interactive Tab Line SASS Stylings
.editor-tabs-bar {
  display: flex; height: 35px; background-color: rgba(0, 0, 0, 0.15); border-bottom: 1px solid $border-color; overflow-x: auto; overflow-y: hidden;
  &::-webkit-scrollbar { height: 3px; }
  &::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.1); }

  .editor-tab-item {
    display: inline-flex; align-items: center; height: 100%; padding: 0 14px; border-right: 1px solid $border-color; background-color: rgba(0, 0, 0, 0.1); font-size: 12px; color: $text-muted; cursor: pointer; user-select: none; transition: background-color 0.1s, color 0.1s;
    &:hover { background-color: rgba(255, 255, 255, 0.02); color: $text-color; }
    &.is-active { background-color: $editor-bg; color: #ffffff; border-top: 2px solid $primary-blue; font-weight: 500; }
    .tab-title-text { max-width: 120px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .dirty-indicator-dot { margin-left: 6px; color: #e11d48; font-size: 10px; }
    .tab-close-icon-btn { margin-left: 8px; width: 14px; height: 14px; display: flex; align-items: center; justify-content: center; border-radius: 2px; font-size: 14px; &:hover { background-color: rgba(255, 255, 255, 0.1); color: #ffffff; } }
  }
}

.window-titlebar {
  height: 32px;
  background-color: $editor-bg; 
  color: $text-muted;
  display: flex;
  justify-content: center; 
  align-items: center;
  -webkit-app-region: drag;
  width: 100%;           
  flex-shrink: 0;        
  border-bottom: 1px solid $border-color;
  margin-bottom: 0;

  .title-string {
    font-size: 0.75rem; 
    font-weight: 500;   
    opacity: 0.8;
  }
}

.app-header {
  height: 52px;
  background-color: rgba(0, 0, 0, 0.2); 
  border-bottom: 1px solid $border-color;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  flex-shrink: 0;

  .meta-section {
    display: flex;
    align-items: center;
    
    .brand-title {
      font-size: 14px;
      font-weight: 700;
      letter-spacing: -0.01em;
      margin: 0;
      color: #ffffff;
    }

    .vertical-divider {
      width: 1px;
      height: 16px;
      background-color: $border-color;
      margin: 0 16px;
    }

    .active-project-tag {
      font-size: 12px;
      color: $text-muted;
      
      .highlight { 
        color: $text-color; 
        font-weight: 600; 
      }
    }
  }

  .controls-section {
    display: flex;
    gap: 8px;
  }
}

.workspace-viewport {
  display: flex;
  flex: 1;
  overflow: hidden;
  width: 100%;
}

.sidebar-column {
  background-color: $sidebar-bg;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  flex-shrink: 0;

  .column-header {
    height: 38px;
    border-bottom: 1px solid $border-color;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 14px;
    background-color: rgba(0, 0, 0, 0.1);

    .header-label {
      font-size: 11px;
      font-weight: 600;
      color: $text-muted;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .header-action-btn {
      background: rgba(255, 255, 255, 0.06); 
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: $text-color;
      cursor: pointer;
      padding: 3px 4px; 
      border-radius: 4px;
      display: flex;
      align-items: center;
      transition: background-color 0.15s, color 0.15s, border-color 0.15s;

      svg { 
        width: 18px; 
        height: 18px; 
      }
      &:hover { 
        color: #ffffff; 
        background-color: rgba(0, 122, 204, 0.25); 
        border-color: $primary-blue;
      }
    }
  }

  .tree-inner-scroller {
    flex: 1;
    overflow-y: auto;
  }
}

.pane-divider-v {
  width: 4px;
  background-color: $border-color;
  cursor: col-resize;
  z-index: 20;
  transition: background-color 0.15s;
  flex-shrink: 0;
  
  &:hover {
    background-color: $primary-blue;
  }
}

.main-editor-console-hub {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
  background-color: $editor-bg;
}

.editor-subview-frame {
  flex: 1;
  position: relative;
  overflow: hidden;
}

.monaco-mount-target {
  width: 100%;
  height: 100%;
  position: relative;

  .placeholder-screen {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: $text-muted;
    font-size: 13px;
    padding: 24px;
    text-align: center;
  }
}

.pane-divider-h {
  height: 4px;
  background-color: $border-color;
  cursor: row-resize;
  z-index: 20;
  transition: background-color 0.15s;
  flex-shrink: 0;
  
  &:hover {
    background-color: $primary-blue;
  }
}

.console-subview-dock {
  border-top: 1px solid $border-color;
  flex-shrink: 0;
  overflow: hidden;
  display: flex !important;          
  flex-direction: column !important; 
  width: 100%;
  background-color: $editor-bg;

  /* Force the locally imported component layout to follow structural dimensions */
  .console-panel-component {
    display: flex !important;
    flex-direction: column !important;
    flex: 1 !important;
    height: 100% !important;
    width: 100% !important;
  }
}

.floating-popover-menu {
  position: fixed;
  background-color: $card-bg;
  border: 1px solid $border-color;
  border-radius: 4px;
  padding: 4px 0;
  min-width: 180px;
  box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5);
  z-index: 100;

  .popover-row {
    padding: 8px 14px;
    font-size: 12px;
    cursor: pointer;
    color: $text-color;
    transition: background-color 0.1s, color 0.1s;

    &:hover { 
      background-color: $primary-blue; 
      color: #ffffff; 
    }
    &.alert-action { 
      color: #f43f5e; 
      &:hover { 
        background-color: #e11d48; 
        color: #ffffff; 
      } 
    }
  }

  .popover-divider {
    height: 1px;
    background-color: $border-color;
    margin: 4px 0;
  }
}

.modal-system-dimmer {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(2px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 200;

  .modal-window-card {
    background-color: $card-bg;
    border: 1px solid $border-color;
    border-radius: 6px;
    padding: 24px;
    width: 100%;
    max-width: 380px;
    box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.7);

    .modal-heading { 
      font-size: 15px; 
      font-weight: 600; 
      margin: 0 0 10px 0; 
      color: #ffffff;
    }
    .modal-body { 
      font-size: 13px; 
      color: $text-muted; 
      line-height: 1.5; 
      margin: 0 0 16px 0; 
    }
    
    .modal-input-container {
      margin-bottom: 20px;
      
      .modal-text-field {
        width: 100%; 
        background-color: rgba(0, 0, 0, 0.2); 
        border: 1px solid $border-color;
        color: #ffffff; 
        padding: 8px 12px; 
        font-size: 13px; 
        border-radius: 4px; 
        box-sizing: border-box;
        
        &:focus { 
          outline: none; 
          border-color: $primary-blue; 
        }
      }
    }

    .modal-actions-row {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
  }
}

.ui-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 6px 14px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  border: none;
  cursor: pointer;
  transition: opacity 0.1s, background-color 0.1s;

  &:hover { 
    opacity: 0.9; 
  }
  
  &.primary-action { 
    background-color: $primary-blue; 
    color: #ffffff; 
  }
  &.standard-action { 
    background-color: rgba(255, 255, 255, 0.05); 
    color: $text-color; 
    border: 1px solid $border-color;
    
    &:hover {
      background-color: rgba(255, 255, 255, 0.1);
    }
  }

  .btn-icon { 
    width: 14px; 
    height: 14px; 
    margin-right: 6px; 
  }
}
</style>