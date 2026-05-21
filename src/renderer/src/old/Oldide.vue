<template>
  <div :class="['ide-wrapper', { 'resizing': isResizing }]">
    <div class="titlebar">
      <span class="titlebar-text">{{ windowTitle }}</span>
    </div>

    <header class="ide-header">
      <div class="brand-group">
        <h1>Marshal IDE</h1>
        <span class="project-display">
          Project: {{ projectName || 'Loading...' }}
        </span>
      </div>
      
      <div class="action-group">
        <button 
          class="btn btn-primary" 
          :disabled="!isDirty"
          @click="saveActiveFile"
        >
          <svg class="icon" viewBox="0 0 24 24" fill="currentColor">
            <path d="M17 3H5C3.89 3 3 3.89 3 5v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/>
          </svg>
          Save
        </button>
        <button class="btn btn-secondary" @click="goBackToMods">
          Back to Mods
        </button>
      </div>
    </header>

    <div class="main-layout">
      <div class="sidebar" :style="{ width: sidebarWidth + 'px' }">
        <div class="sidebar-header">
          <span class="title">Explorer</span>
          <button class="btn-icon" title="Import Image (.dds)" @click="handleImageImport">
            <svg class="icon-svg" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"></path>
            </svg>
          </button>
        </div>
        
        <div id="file-tree-container" class="file-tree-container">
          <p v-if="!projectName" class="tree-placeholder">Loading file structure...</p>
        </div>
      </div>
      
      <div class="resizer resizer-v" @mousedown="startSidebarResize"></div>

      <div class="editor-console-container" ref="editorConsoleContainer">
        <div id="editor-container" class="editor-container">
          <div v-if="!currentFilePath" class="editor-placeholder">
            <p>Select a file to begin editing.</p>
          </div>
        </div>
        
        <div class="resizer resizer-h" @mousedown="startConsoleResize">
          <button class="console-toggle-btn" title="Toggle Console" @click.stop="toggleConsole">
            <svg :class="['icon-arrow', { 'collapsed': consoleMinimized }]" fill="currentColor" viewBox="0 0 24 24">
              <path d="M7 14l5-5 5 5z"/>
            </svg>
          </button>
        </div>
        
        <div class="console-container" :style="{ height: consoleMinimized ? '30px' : consoleHeight + 'px' }">
          <div class="console-header">
            <span class="clear-hook" @click="clearLogConsole">Console</span>
          </div>
          <div ref="consoleOutputElement" class="console-output">
            <div 
              v-for="(log, idx) in consoleLogs" 
              :key="idx" 
              :class="['p-1 border-b border-gray-800 text-sm', log.isError ? 'text-red-400' : 'text-gray-300']"
            >
              <span class="text-gray-500 mr-2">[{{ log.timestamp }}]</span> {{ log.message }}
            </div>
          </div>
        </div>
      </div>
    </div>

    <div 
      v-if="contextMenu.visible" 
      class="context-menu"
      :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }"
    >
      <div v-if="contextMenu.isDir" class="context-item" @click="triggerAction('new-file')">
        <svg class="icon-small" fill="currentColor" viewBox="0 0 24 24"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zM15 11h-2v2h-2v-2H9V9h2V7h2v2h2v2z"/></svg>
        <span>New File...</span>
      </div>
      <div class="context-item" @click="triggerAction('refresh')">
        <svg class="icon-small" fill="currentColor" viewBox="0 0 24 24"><path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.7 1.95-2.59 3.39-4.82 3.39-2.97 0-5.38-2.41-5.38-5.38S9.03 6.62 12 6.62c1.45 0 2.76.6 3.7 1.54l-2.7 2.7h6.5V4l-2.35 2.35z"/></svg>
        <span>Refresh</span>
      </div>
      <div v-if="!contextMenu.isDir" class="context-item" @click="triggerAction('rename')">
        <svg class="icon-small" fill="currentColor" viewBox="0 0 24 24"><path d="M7 19h10v-1H7v1zm2-6h6v-1H9v1zm-2-4h10V8H7v1zm12-4H5a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2V5a2 2 0 00-2-2zm-3 14H8V5h8v12z"/></svg>
        <span>Rename...</span>
      </div>
      <div v-if="!contextMenu.isDir" class="context-item danger" @click="triggerAction('delete')">
        <svg class="icon-small" fill="currentColor" viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg>
        <span>Delete...</span>
      </div>
    </div>

    <div v-if="modal.visible" class="modal-backdrop">
      <div class="modal-card">
        <h2 class="modal-title">{{ modal.title }}</h2>
        <p class="modal-message" v-html="modal.message"></p>
        
        <div v-if="modal.requiresInput" class="modal-input-wrapper">
          <input 
            type="text" 
            v-model="modal.inputValue"
            ref="modalInput"
            class="modal-input"
            :placeholder="modal.placeholder"
            @keydown.enter="confirmModalAction"
          />
        </div>
        
        <div class="modal-actions">
          <button class="btn btn-secondary" @click="closeModal">Cancel</button>
          <button class="btn btn-primary" @click="confirmModalAction">Confirm</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue';

// Configuration and Mapping Utilities
import { FILE_EXTENSION_MAP } from './components/hoi4/hoi4Config.js';

// Core State and Workspace Process Management Engines
import { 
  STATE, 
  ELEMENTS,
  normalizePath, 
  setDirtyState, 
  loadProject, 
  saveActiveFile, 
  triggerSaveShortcut,
  initializeElements 
} from './projectManager.js';

// Directory Tree and Dynamic Virtual Element Generation Handlers
import { 
  loadDirectoryTree, 
  refreshFolder, 
  loadFileContent 
} from './fileTree.js';

// Integrated System Log Console Listeners and Stream Handlers
import { 
  initializeConsole, 
  startLogListener, 
  stopLogListener 
} from './consoleModule.js';

// --- State Reactivity ---
const projectName = ref(null);
const currentFilePath = ref(null);
const isDirty = ref(false);
const windowTitle = ref('Marshal IDE - Mod');

// Layout Sizing Controls
const sidebarWidth = ref(250);
const consoleHeight = ref(150);
const consoleMinimized = ref(true);
const isResizing = ref(false);
let currentResizeType = null;
let lastConsoleHeight = 150;
const MINIMIZED_HEIGHT = 30;

// UI DOM Container Elements References
const editorConsoleContainer = ref(null);
const consoleOutputElement = ref(null);

// Reactive Console Logs Structure
const consoleLogs = ref([
  { timestamp: new Date().toLocaleTimeString(), message: 'Console Initialized.', isError: false }
]);

// Submenu States
const contextMenu = reactive({ visible: false, x: 0, y: 0, path: '', isDir: false });
const modal = reactive({ visible: false, type: '', title: '', message: '', requiresInput: false, inputValue: '', placeholder: '', targetPath: '', forcedExtension: '', originalExtension: '' });
const modalInput = ref(null);

// --- Dynamic Title Calculation Logic Hook ---
const computeWindowTitle = () => {
  let base = 'Marshal IDE - Mod';
  if (projectName.value) base = `Marshal IDE - ${projectName.value}`;
  if (currentFilePath.value) base = `${base} - ${currentFilePath.value.split('/').pop()}`;
  if (isDirty.value) base = `* ${base}`;
  windowTitle.value = base;
};



// --- Action Methods ---
const goBackToMods = () => {
  if (isDirty.value && !confirm("You have unsaved changes. Go back?")) return;
  window.api.send('switch-page', 'index');
};

const handleImageImport = async () => {
  const result = await window.api.invoke('import-image');
  if (result && result.success) loadDirectoryTree(projectName.value);
};

// --- Context Menu Management ---
window.showContextMenuHook = (e, path, isDir) => {
  contextMenu.visible = true;
  contextMenu.x = e.clientX;
  contextMenu.y = e.clientY;
  contextMenu.path = path;
  contextMenu.isDir = isDir;

  const autoClose = (event) => {
    contextMenu.visible = false;
    document.removeEventListener('click', autoClose);
  };
  setTimeout(() => document.addEventListener('click', autoClose), 100);
};





// --- Key Capture Binding Listener Hook ---
const handleGlobalKeybinds = (e) => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
    triggerSaveShortcut(e);
  }
};

// --- Lifecycle Anchors ---
onMounted(() => {
  initializeElements();
    
  window.api.log.info("IDE Mounted successfully, references mapped.", "ide-Renderer");
  injectCoreStateBridges();
  document.addEventListener('keydown', handleGlobalKeybinds, true);

  // Extract setup configuration settings from CSS custom properties if present
  const rootStyle = getComputedStyle(document.documentElement);
  const cssHeightFull = parseInt(rootStyle.getPropertyValue('--console-height-full')) || 150;
  lastConsoleHeight = cssHeightFull;
  consoleHeight.value = lastConsoleHeight;

  // Mount the Electron log listeners reactively
  if (window.api?.logBroadcaster) {
    window.api.logBroadcaster.addListener('log', handleIncomingLog);
  }

  // Extract workspace bootstrap config values
  const modName = localStorage.getItem('marshal_project_to_load');
  localStorage.removeItem('marshal_project_to_load');

  if (modName) {
    if (typeof initMonaco === 'function') initMonaco();
    loadProject(modName);
  } else {
    window.api.send('switch-page', 'index');
  }
});

onUnmounted(() => {
  document.removeEventListener('keydown', handleGlobalKeybinds, true);
  if (window.api?.logBroadcaster) {
    window.api.logBroadcaster.removeListener('log', handleIncomingLog);
  }
});
</script>

<style lang="scss" scoped>
@use "sass:color";

/* IDE Theme System Configurations */
$sidebar-bg: #18181c;
$editor-bg: #1e1e24;
$border-color: #2e2e38;
$primary-blue: #3b82f6;
$text-color: #e2e8f0;
$text-muted: #8a99ad;

// Layout Wrappers
.ide-wrapper {
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  margin: 0;
  padding: 0;
  height: 100vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background-color: $sidebar-bg;
  color: $text-color;

  &.resizing {
    cursor: col-resize !important;
    user-select: none !important;
    -webkit-user-select: none !important;
  }
}

// Native Electron Custom Titlebar styling
.titlebar {
  height: 32px;
  background-color: $editor-bg;
  color: $text-muted;
  display: flex;
  justify-content: center;
  align-items: center;
  -webkit-app-region: drag;
  width: 100%;
  flex-shrink: 0;
  box-sizing: border-box;
  border-bottom: 1px solid $border-color;

  .titlebar-text {
    font-size: 0.75rem;
    font-weight: 500;
    opacity: 0.8;
  }
}

// Upper Control Header Nav bar 
.ide-header {
  background-color: color.adjust($sidebar-bg, $lightness: -3%);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1rem;
  height: 3rem;
  flex-shrink: 0;
  border-bottom: 1px solid $border-color;

  .brand-group {
    display: flex;
    align-items: center;
    gap: 1rem;

    h1 {
      font-size: 1.125rem;
      font-weight: 700;
      margin: 0;
    }
    .project-display {
      font-size: 0.875rem;
      color: $text-muted;
    }
  }

  .action-group {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }
}

// Component Buttons Structural Rules
.btn {
  display: inline-flex;
  align-items: center;
  font-weight: 600;
  padding: 0.35rem 0.85rem;
  border-radius: 0.25rem;
  font-size: 0.875rem;
  border: none;
  cursor: pointer;
  transition: background-color 0.15s ease;

  &.btn-primary {
    background-color: $primary-blue;
    color: #ffffff;
    &:hover:not(:disabled) { background-color: color.adjust($primary-blue, $lightness: -8%); }
  }
  &.btn-secondary {
    background-color: color.adjust($sidebar-bg, $lightness: 12%);
    color: #ffffff;
    border: 1px solid $border-color;
    &:hover:not(:disabled) { background-color: color.adjust($sidebar-bg, $lightness: 18%); }
  }

  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .icon {
    width: 1rem;
    height: 1rem;
    margin-right: 0.25rem;
  }
}

// Core split layout split frame blocks
.main-layout {
  display: flex;
  flex: 1;
  overflow: hidden;
  width: 100%;
}

// Left File Explorer Tree Panel Window
.sidebar {
  background-color: $sidebar-bg;
  display: flex;
  flex-direction: column;
  min-width: 150px;
  overflow: hidden;
  flex-shrink: 0;

  .sidebar-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem;
    border-bottom: 1px solid $border-color;

    .title {
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      color: $text-muted;
      letter-spacing: 0.05em;
    }

    .btn-icon {
      background: transparent;
      border: none;
      color: $text-muted;
      cursor: pointer;
      padding: 0.25rem;
      border-radius: 0.25rem;
      display: flex;
      align-items: center;
      justify-content: center;

      &:hover {
        color: #ffffff;
        background-color: color.adjust($sidebar-bg, $lightness: 8%);
      }
      .icon-svg { width: 1.25rem; height: 1.25rem; }
    }
  }

  .file-tree-container {
    overflow-y: auto;
    padding: 0.25rem 0 0.5rem 0;
    font-size: 0.875rem;
    flex-grow: 1;

    .tree-placeholder {
      color: $text-muted;
      font-size: 0.75rem;
      padding: 0.5rem;
    }
  }
}

// Unified Split Sizer Bar Architecture
.resizer {
  background-color: $border-color;
  z-index: 10;
  transition: background-color 0.1s;
  flex-shrink: 0;
  position: relative;

  &:hover { background-color: $primary-blue; }

  &.resizer-v {
    width: 1px;
    cursor: col-resize;
    &::after {
      content: '';
      position: absolute;
      top: 0;
      left: -5px;
      width: 11px;
      height: 100%;
    }
  }

  &.resizer-h {
    height: 1px;
    cursor: row-resize;
    display: flex;
    align-items: center;
    justify-content: flex-end;
    z-index: 15;
    &::after {
      content: '';
      position: absolute;
      left: 0;
      top: -5px;
      width: 100%;
      height: 11px;
    }
  }
}

// Right Block Viewport Area
.editor-console-container {
  display: flex;
  flex-direction: column;
  flex-grow: 1;
  overflow: hidden;
}

// Center Monaco Editor Frame
.editor-container {
  flex-grow: 1;
  flex-shrink: 1;
  overflow: hidden;
  position: relative;
  background-color: $editor-bg;

  .editor-placeholder {
    height: 100%;
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    color: $text-muted;
  }
}

// Lower Log Window Console Architecture Box
.console-container {
  background-color: color.adjust($editor-bg, $lightness: -2%);
  padding: 0.5rem;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  transition: height 0.2s cubic-bezier(0.16, 1, 0.3, 1);
  overflow: hidden;
  border-top: 1px solid $border-color;

  .console-header {
    font-size: 0.75rem;
    font-weight: 600;
    border-bottom: 1px solid $border-color;
    padding-bottom: 0.25rem;
    text-transform: uppercase;
    color: $text-muted;
    letter-spacing: 0.05em;

    .clear-hook {
      cursor: pointer;
      &:hover { color: #ffffff; }
    }
  }

  .console-output {
    font-size: 0.75rem;
    font-family: 'Fira Code', 'Consolas', monospace;
    overflow-y: auto;
    flex-grow: 1;
    margin-top: 0.25rem;
    color: #d1d5db;
  }
}

// Console Overlay Minimize Trigger Button Rules
.console-toggle-btn {
  position: relative;
  z-index: 20;
  background-color: $border-color;
  border: none;
  color: #ffffff;
  padding: 0.25rem;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;

  &:hover { background-color: $primary-blue; }
  .icon-arrow {
    width: 1rem;
    height: 1rem;
    transition: transform 0.2s;
    &.collapsed { transform: rotate(180deg); }
  }
}

// Floated Context Item Windows Popup Menu 
.context-menu {
  position: fixed;
  background-color: color.adjust($sidebar-bg, $lightness: 10%);
  color: #ffffff;
  font-size: 0.875rem;
  border-radius: 0.25rem;
  box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.5);
  padding: 0.25rem 0;
  z-index: 50;
  border: 1px solid $border-color;

  .context-item {
    padding: 0.5rem 1rem;
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 0.5rem;

    &:hover { background-color: $primary-blue; }
    &.danger:hover { background-color: #ef4444; }
    .icon-small { width: 1rem; height: 1rem; }
  }
}

// Modal Form Interceptor Elements rules
.modal-backdrop {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(4px);
  z-index: 50;
  display: flex;
  align-items: center;
  justify-content: center;

  .modal-card {
    background-color: color.adjust($sidebar-bg, $lightness: 5%);
    border: 1px solid $border-color;
    padding: 1.5rem;
    border-radius: 0.5rem;
    box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5);
    width: 100%;
    max-width: 24rem;

    .modal-title {
      font-size: 1.25rem;
      font-weight: 700;
      color: #ffffff;
      margin-bottom: 1rem;
    }
    .modal-message {
      color: #d1d5db;
      font-size: 0.875rem;
      margin-bottom: 1rem;
      line-height: 1.4;
    }
    
    .modal-input-wrapper {
      margin-bottom: 1rem;
      .modal-input {
        width: 100%;
        padding: 0.5rem;
        border-radius: 0.25rem;
        background-color: rgba(0, 0, 0, 0.2);
        color: #ffffff;
        border: 1px solid $border-color;
        font-size: 0.875rem;
        box-sizing: border-box;
        &:focus {
          outline: none;
          border-color: $primary-blue;
        }
      }
    }

    .modal-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
    }
  }
}

// Utility styling classes inside log lists
.p-1 { padding: 0.25rem; }
.border-b { border-bottom-width: 1px; }
.border-gray-800 { border-color: color.adjust($border-color, $lightness: -5%); }
.text-sm { font-size: 0.8rem; }
.text-red-400 { color: #f87171; }
.text-gray-300 { color: #d1d5db; }
.text-gray-500 { color: #6b7280; }
.mr-2 { margin-right: 0.5rem; }

// Standardized Webkit Custom Scrollbars
::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}
::-webkit-scrollbar-track {
  background: transparent;
}
::-webkit-scrollbar-thumb {
  background: color.adjust($border-color, $lightness: 10%);
  border-radius: 4px;
  &:hover { background: color.adjust($border-color, $lightness: 20%); }
}
</style>