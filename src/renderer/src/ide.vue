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
        <button class="ui-btn standard-action" @click="handleRecompileAll">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M17.65 6.35A7.958 7.958 0 0012 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"/></svg>
          Recompile
        </button>
        <button class="ui-btn standard-action" @click="handleImportImage">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 5H5l3.5-4.5z"/></svg>
          Import Img
        </button>
        <button class="ui-btn primary-action" @click="saveActiveFile" :disabled="!activeTabPath">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M17 3H5C3.89 3 3 3.89 3 5v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>
          Save
        </button>
        <button class="ui-btn standard-action" @click="handleExitWorkspace">Exit Workspace</button>
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
        @folder-toggled="handleFolderToggleState"
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

          <div id="editor-container" class="monaco-mount-target"></div>
          
          <div v-if="openTabs.length === 0" class="placeholder-screen">
            <p>Select a structural project file asset from the tree browser configuration map to begin code composition.</p>
          </div>
        </div>
        
        <div class="pane-divider-h" @mousedown="startConsoleResize"></div>

        <div class="console-subview-dock" :style="{ height: isConsoleMinimized ? '35px' : consoleHeight + 'px' }">
          <ConsolePanel 
            class="console-panel-component"
            :is-minimized="isConsoleMinimized"
            show-errors-tab
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
      <div class="popover-row" @click="triggerCreateModal(contextMenu.targetNode?.isDir ? contextMenu.targetNode.path : contextMenu.targetNode?.path.substring(0, contextMenu.targetNode.path.lastIndexOf('/')))">New File</div>
      
      <div v-if="contextMenu.targetNode && !contextMenu.targetNode.isDir" class="popover-row" @click="triggerRenameModal(contextMenu.targetNode)">Rename</div>
      <div v-if="contextMenu.targetNode && !contextMenu.targetNode.isDir" class="popover-divider"></div>
      <div v-if="contextMenu.targetNode && !contextMenu.targetNode.isDir" class="popover-row alert-action" @click="triggerDeleteAction(contextMenu.targetNode)">Delete</div>
    </div>

    <div class="modal-system-dimmer" v-if="closeConfirmVisible" @keydown.esc="handleConfirmCloseCancel" tabindex="-1">
      <div class="modal-window-card">
        <h3 class="modal-heading">Unsaved Document Changes</h3>
        <p class="modal-body">
          This file contains unsaved changes. Would you like to synchronize changes to disk storage before closing it?
        </p>
        <div class="modal-actions-row" style="display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px;">
          <button class="ui-btn standard-action" @click="handleConfirmCloseCancel">Cancel</button>
          <button class="ui-btn destructive-action" @click="handleConfirmCloseDiscard">Discard Changes</button>
          <button class="ui-btn primary-action" @click="handleConfirmCloseSave">Save & Close</button>
        </div>
      </div>
    </div>

    <div class="modal-system-dimmer" v-if="modal.visible" ref="modalDimmerRef" @keydown.capture="handleModalKeyDown" tabindex="-1">
      <div class="modal-window-card">
        <h3 class="modal-heading">{{ modal.title }}</h3>
        <p class="modal-body">{{ modal.body }}</p>
        
        <div class="modal-input-container" style="display: flex; flex-direction: column; gap: 14px; margin-bottom: 20px;">
          <div v-if="modal.mode === 'create'" style="display: flex; flex-direction: column; gap: 4px; text-align: left;">
            <label style="font-size: 11px; color: #aaaaaa; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;">Target Folder Context</label>
            <select v-model="modal.targetNode" @change="handleFolderChange" class="modal-text-field dark-forced-input">
              <option disabled value="">-- Select Target Folder --</option>
              <option v-for="folder in modal.availableFolders" :key="folder" :value="folder">{{ folder }}</option>
            </select>
          </div>

          <div v-if="modal.mode !== 'confirm'" style="display: flex; flex-direction: column; gap: 4px; text-align: left;">
            <label style="font-size: 11px; color: #aaaaaa; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;">Name & Compiler Extension</label>
            <div style="display: flex; gap: 8px; width: 100%;">
              <input type="text" v-model="modal.inputValue" :placeholder="modal.placeholder" class="modal-text-field dark-forced-input" style="flex: 1;">
              
              <select v-model="modal.selectedExtension" class="modal-text-field dark-forced-input" style="width: 130px; font-family: monospace; font-weight: bold;">
                <option v-for="ext in [...new Set(Object.values(FILE_EXTENSION_MAP))]" :key="ext" :value="ext">{{ ext }}</option>
                <option value=".unknown">.unknown</option>
              </select>
            </div>
          </div>
      </div>
      
      <div class="modal-actions-row">
        <button class="ui-btn standard-action" @click="modal.visible = false">Cancel</button>
        <button class="ui-btn primary-action" @click="commitModalAction">Confirm</button>
      </div>
        
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, nextTick, onBeforeUnmount, markRaw, toRaw } from 'vue';
import ConsolePanel from '../ide/Console.vue';
import FileTreePanel from '../ide/FileTree.vue';
import { FILE_EXTENSION_MAP, getMonacoLanguage, defineDslLanguages } from '../ide/components/hoi4/config.js';

const modalDimmerRef = ref(null);

// Active Monaco reference variable accessible anywhere in this script block
let editorInstance = null;



const activeProjectName = ref('Loading...');

onMounted(() => {
  // Sync the UI header text to match the project loaded via localStorage
  activeProjectName.value = localStorage.getItem('marshal_project_to_load') || 'No Active Project';
});

const handleExitWorkspace = async () => {
  try {
    // Notify the main process to shut down background tasks and safely transition the view
    await window.api.invoke('unload-project');
  } catch (err) {
    console.error('Failed to execute unmount sequence through main process routing handler:', err);
  }
};

// Isolated layout states
const sidebarWidth = ref(260);
const consoleHeight = ref(240);
const isConsoleMinimized = ref(false);
const isResizing = ref(false);

// Active Tab and Editor Instance Registries
const fileTreeRef = ref(null);
const openTabs = ref([]);
const activeTabPath = ref(null);
let isUpdatingModelFromState = false;
let isSwitchingTab = false; // Anti-reentrancy lock to prevent freeze loops
const expandedFoldersRegistry = ref(new Set());

// Confirmation interceptor state for tabs with unsaved dirty code modifications
const closeConfirmVisible = ref(false);
const pendingClosePath = ref(null);

// Overlays Context System State Management
const contextMenu = ref({ visible: false, x: 0, y: 0, targetNode: null });
const modal = ref({ 
  visible: false, 
  title: '', 
  body: '', 
  placeholder: '', 
  inputValue: '', 
  mode: '', 
  targetNode: '', // Holds string paths for creation targets
  selectedExtension: '.unknown', 
  availableFolders: [] 
});

// Auto-switches the extension dropdown when the folder choice shifts
const handleFolderChange = () => {
  if (!modal.value.targetNode) return;
  const normalized = modal.value.targetNode.replace(/\\/g, '/');
  const pathSegments = normalized.split('/');
  const parentFolder = pathSegments[pathSegments.length - 1].toLowerCase().trim();
  modal.value.selectedExtension = FILE_EXTENSION_MAP[parentFolder] || '.unknown';
  
  expandedFoldersRegistry.value.add(modal.value.targetNode);
};


// Trap standard Enter/Escape keyboard layout shortcuts inside operational modals
const handleModalKeyDown = (event) => {
  if (event.key === 'Escape') {
    event.preventDefault();
    event.stopPropagation();
    modal.value.visible = false;
  } 
  else if (event.key === 'Enter') {
    if (event.target.tagName === 'TEXTAREA') return;
    
    event.preventDefault();
    event.stopPropagation();
    commitModalAction();
  }
};

// Instantly force focus onto the modal overlay container the microsecond it appears
watch(() => modal.value.visible, async (isVisible) => {
  if (isVisible) {
    await nextTick();
    // Yank focus away from the main window view directly to the modal wrapper
    modalDimmerRef.value?.focus();
  }
});

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
const setActiveTab = async (path) => {
  if (isSwitchingTab) {
    return;
  }

  isSwitchingTab = true;
  activeTabPath.value = path;
  
  const targetTab = openTabs.value.find(t => t.path === path);
  if (!targetTab) {
    isSwitchingTab = false;
    return;
  }

  await nextTick();

  const activeEditor = editorInstance || window.editorInstance;
  const monacoGlobal = window.monaco;


  if (activeEditor && monacoGlobal) {
    try {
      if (!targetTab.monacoModel) {
        const languageId = getMonacoLanguage(path);
        const modelUri = monacoGlobal.Uri.file(path);
        
        let modelInstance = monacoGlobal.editor.getModel(modelUri);
        
        if (modelInstance) {
        } else {
          modelInstance = monacoGlobal.editor.createModel(targetTab.content, languageId, modelUri);
          
          modelInstance.onDidChangeContent(() => {
            const currentVal = modelInstance.getValue();
            if (targetTab.content !== currentVal) {
              targetTab.isDirty = true;
            }
          });
        }
        
        targetTab.monacoModel = modelInstance ? markRaw(modelInstance) : null;
      } else {
      }

      // Strip any reactive proxies clean before sending the object into the native framework
      const cleanNativeModel = toRaw(targetTab.monacoModel);

      activeEditor.setModel(cleanNativeModel);
      
      const currentLanguageId = getMonacoLanguage(path);
      
      // Wrap in a safe microtask deferred timeout to let the editor frame paint the text first
      setTimeout(() => {
        try {
          monacoGlobal.editor.setModelLanguage(targetTab.monacoModel, currentLanguageId);
        } catch (langErr) {
        }
      }, 0);

    } catch (runtimeErr) {
    } finally {
      isSwitchingTab = false;
    }
  } else {
    isSwitchingTab = false;
  }
};
const handleFileSelection = async (node) => {
  if (!node || node.isDir) return;

  try {
    let existingTab = openTabs.value.find(t => t.path === node.path);
    
    if (!existingTab) {
      const res = await window.api.invoke('get-file-content', { filePath: node.path });
      
      // Handle both boolean success and raw object returns depending on your exact wrapper
      if (res && (res.success !== false)) {
        const rawContent = typeof res === 'string' ? res : (res.content || '');
        
        const monacoGlobal = window.monaco;
        let tabModel = null;
        
        if (monacoGlobal) {
          const languageId = getMonacoLanguage(node.path);
          const modelUri = monacoGlobal.Uri.file(node.path);
          
          // Look up existing global model instances first
          tabModel = monacoGlobal.editor.getModel(modelUri);
          
          if (!tabModel) {
            tabModel = monacoGlobal.editor.createModel(rawContent, languageId, modelUri);
            
            tabModel.onDidChangeContent(() => {
              const currentVal = tabModel.getValue();
              const tabItem = openTabs.value.find(t => t.path === node.path);
              if (tabItem && tabItem.content !== currentVal) {
                tabItem.isDirty = true;
              }
            });
          }
        }

        existingTab = {
          name: node.name,
          path: node.path,
          content: rawContent,
          isDirty: false,
          monacoModel: tabModel ? markRaw(tabModel) : null 
        };

        openTabs.value.push(existingTab);
      } else {
        console.error('Backend failed to read file context:', res?.message || res);
        return;
      }
    }

    // Set active pointer and update Monaco display
    await setActiveTab(node.path);

  } catch (err) {
    console.error('Failed during file selection loading sequence:', err);
  }
};


const closeTab = (path) => {
  const tabIndex = openTabs.value.findIndex(t => t.path === path);
  if (tabIndex === -1) return;

  const targetTab = openTabs.value[tabIndex];

  // Intercept close request if tab has pending, unsaved modifications
  if (targetTab.isDirty) {
    pendingClosePath.value = path;
    closeConfirmVisible.value = true;
    return; // Halt closing sequence until client confirms structural intent
  }

  // Fallthrough to standard clean closure routine if tab is not dirty
  executeForceCloseTab(path);
};

// Extracted baseline closure handler to run once confirmation checks pass
const executeForceCloseTab = (path) => {
  const tabIndex = openTabs.value.findIndex(t => t.path === path);
  if (tabIndex === -1) return;

  const targetTab = openTabs.value[tabIndex];
  if (targetTab?.monacoModel?.dispose) {
    targetTab.monacoModel.dispose();
  } else {
    const fallbackModel = window.monaco?.editor?.getModel(window.monaco.Uri.file(path));
    if (fallbackModel) fallbackModel.dispose();
  }

  openTabs.value.splice(tabIndex, 1);
  
  if (activeTabPath.value === path) {
    if (openTabs.value.length > 0) {
      setActiveTab(openTabs.value[Math.max(0, tabIndex - 1)].path);
    } else { 
      activeTabPath.value = null; 
      if (editorInstance && window.monaco) {
        const emptyModel = window.monaco.editor.createModel('', 'plaintext');
        editorInstance.setModel(emptyModel);
      }
    }
  }
};

const handleConfirmCloseSave = async () => {
  if (!pendingClosePath.value) return;
  
  // 1. Activate the tab to ensure saveActiveFile captures its current content
  await setActiveTab(pendingClosePath.value);
  await saveActiveFile();
  
  // 2. Clear state pointers and force closure
  const path = pendingClosePath.value;
  pendingClosePath.value = null;
  closeConfirmVisible.value = false;
  executeForceCloseTab(path);
};

const handleConfirmCloseDiscard = () => {
  if (!pendingClosePath.value) return;
  
  const path = pendingClosePath.value;
  pendingClosePath.value = null;
  closeConfirmVisible.value = false;
  
  // Reset dirty state to clear closure intercept gates
  const targetTab = openTabs.value.find(t => t.path === path);
  if (targetTab) targetTab.isDirty = false;
  
  executeForceCloseTab(path);
};

const handleConfirmCloseCancel = () => {
  pendingClosePath.value = null;
  closeConfirmVisible.value = false;
};

const saveActiveFile = async () => {
  if (!activeTabPath.value || !editorInstance) return;
  const currentTab = openTabs.value.find(t => t.path === activeTabPath.value);
  if (!currentTab) return;

  // Retrieve code from the tab's dedicated multi-model structure
  const modelToSave = currentTab.monacoModel || editorInstance.getModel();
  if (!modelToSave) return;

  const codePayload = modelToSave.getValue();
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
  let initialExt = '.unknown';
  if (parentPath) {
    const normalized = parentPath.replace(/\\/g, '/');
    const pathSegments = normalized.split('/');
    const parentFolder = pathSegments[pathSegments.length - 1].toLowerCase().trim();
    initialExt = FILE_EXTENSION_MAP[parentFolder] || '.unknown';
  }

  modal.value = { 
    visible: true, 
    title: 'New File', 
    body: 'Select a target folder and specify a name:', 
    placeholder: 'my_new_file', 
    inputValue: '', 
    mode: 'create', 
    targetNode: parentPath,
    selectedExtension: initialExt,
    availableFolders: Object.keys(FILE_EXTENSION_MAP)
  };
};

const triggerRenameModal = (node) => {
  closeContextMenu();
  if (node.isDir) return; // Block folders completely

  const originalExt = '.' + node.path.split('.').pop().toLowerCase();
  let baseName = node.name;
  const lastDot = baseName.lastIndexOf('.');
  if (lastDot !== -1) {
    baseName = baseName.substring(0, lastDot);
  }

  modal.value = { 
    visible: true, 
    title: 'Rename', 
    body: 'Change name (extension is preserved safely via dropdown):', 
    placeholder: baseName, 
    inputValue: baseName, 
    mode: 'rename', 
    targetNode: node,
    selectedExtension: originalExt,
    availableFolders: Object.keys(FILE_EXTENSION_MAP)
  };
};

const triggerDeleteAction = (node) => {
  closeContextMenu();
  if (node.isDir) return; // Block folders completely
  modal.value = { visible: true, title: 'Delete', body: `Are you sure you want to permanently delete "${node.name}"?`, placeholder: '', inputValue: '', mode: 'confirm', targetNode: node };
};

const handleRecompileAll = async () => {
  try {
    // Triggers the main process IPC channel, which will pass the action 
    // down to the active watcher_workspace child process thread
    await window.api.invoke('recompile-all');
  } catch (err) {
    console.error('Failed to trigger full workspace recompilation:', err);
  }
};

const handleImportImage = async () => {
  try {
    const res = await window.api.invoke('import-image');
    if (res && res.success) {
      // Trigger our non-destructive flat tree refresher to instantly show the new .dds assets
      fileTreeRef.value?.refreshTree();
    } else if (res && res.message && !res.message.includes('cancelled')) {
      console.warn(`Import warning notice structural stack trace: ${res.message}`);
    }
  } catch (err) {
    console.error('Failed to trigger native main process asset importer:', err);
  }
};

const commitModalAction = async () => {
  const { mode, targetNode, inputValue, selectedExtension } = modal.value;
  let input = inputValue.trim();
  if (!input && mode !== 'confirm') return;

  // Strip any manually written dots/extensions from input
  const lastDotIndex = input.lastIndexOf('.');
  if (lastDotIndex !== -1) {
    input = input.substring(0, lastDotIndex);
  }

  const cleanFileName = `${input}${selectedExtension}`;

  if (mode === 'create') {
    if (!targetNode) {
      alert('Please select a target folder first.');
      return;
    }
    const finalPath = `${targetNode}/${cleanFileName}`;
    const res = await window.api.invoke('create-file', { filePath: finalPath });
    if (res && res.success) {
      // Refresh and feed the currently open folder paths straight back into the tree
      fileTreeRef.value?.refreshTree([...expandedFoldersRegistry.value]);
    }
  } 
  
  else if (mode === 'rename' && targetNode) {
    const lastSlash = targetNode.path.lastIndexOf('/');
    const parent = lastSlash !== -1 ? targetNode.path.substring(0, lastSlash + 1) : '';
    const newPath = `${parent}${cleanFileName}`;

    const res = await window.api.invoke('rename-file', { oldFilePath: targetNode.path, newFilePath: newPath });
    if (res && res.success) {
      const tab = openTabs.value.find(t => t.path === targetNode.path);
      if (tab) { 
        tab.name = cleanFileName; 
        tab.path = newPath; 
      }
      if (activeTabPath.value === targetNode.path) activeTabPath.value = newPath;
      fileTreeRef.value?.refreshTree([...expandedFoldersRegistry.value]);
    }
  } 
  
  else if (mode === 'confirm' && targetNode) {
    const res = await window.api.invoke('delete-file-or-dir', { path: targetNode.path });
    if (res && res.success) { 
      closeTab(targetNode.path); 
      fileTreeRef.value?.refreshTree([...expandedFoldersRegistry.value]); 
    }
  }

  modal.value.visible = false;
};

onMounted(() => {
  window.addEventListener('keydown', handleGlobalShortcuts);
  window.addEventListener('resize', () => {
    const activeEd = editorInstance || window.editorInstance;
    activeEd?.layout();
  });

  console.log("[DIAGNOSTIC] onMounted fired. Checking initial loader environment...");
  console.log("[DIAGNOSTIC] Current window.monaco state:", !!window.monaco);
  console.log("[DIAGNOSTIC] Targets found: #editor-container =", !!document.getElementById('editor-container'));

  let passCount = 0;

  // Execution loop replaced with a clean, single-pass async initialization routine
  const initMonacoInstance = async () => {
    let monacoGlobal = window.monaco || (typeof monaco !== 'undefined' ? monaco : null);

    // Explicitly configure Monaco environment providers to treat worker compilation safely inline
    window.MonacoEnvironment = {
      getWorker: function (workerId, label) {
        return new Worker(new URL('monaco-editor/esm/vs/editor/editor.worker.js', import.meta.url), {
          type: 'module'
        });
      }
    };
    
    if (!monacoGlobal) {
      try {
        const importedModule = await import('monaco-editor');
        if (importedModule) {
          monacoGlobal = importedModule;
          window.monaco = monacoGlobal;
        }
      } catch (importErr) {
        console.error("[DIAGNOSTIC] Failed to import monaco-editor package:", importErr);
        return;
      }
    }

    if (monacoGlobal) {
      const container = document.getElementById('editor-container');
      if (!container) return;

      // Check if Monaco is already mounted to prevent double execution errors
      if (editorInstance || window.editorInstance) {
        console.log("[DIAGNOSTIC] Monaco is already mapped to the workspace layout container.");
        return;
      }

      try {
        container.innerHTML = ''; 
        defineDslLanguages(monacoGlobal);

        const instance = monacoGlobal.editor.create(container, {
          value: 'Select a project file asset from the tree browser to begin code composition.',
          language: 'plaintext', 
          theme: 'myDslTheme', 
          fixedOverflowWidgets: true,
          automaticLayout: true, 
          minimap: { enabled: true },
          fontSize: 13,
          fontFamily: "'Fira Code', 'Cascadia Code', monospace",
          readOnly: false, 
          links: false,
          overviewRerenderLanes: 0,
        });

        editorInstance = instance;
        window.editorInstance = instance;

        if (activeTabPath.value) {
          const bufferedTab = openTabs.value.find(t => t.path === activeTabPath.value);
          if (bufferedTab) {
            instance.setValue(bufferedTab.content);
            const languageId = getMonacoLanguage(activeTabPath.value);
            const model = instance.getModel();
            if (model && monacoGlobal.editor?.setModelLanguage) {
              monacoGlobal.editor.setModelLanguage(model, languageId);
            }
          }
        }
      } catch (initErr) {
        console.error("[DIAGNOSTIC] Error during editor layout orchestration:", initErr);
      }
    }
  };

  // Run the initialization single-pass directly
  initMonacoInstance();
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
  background-color: #1e1e1e !important;
  border: 1px solid #333333 !important;
  border-radius: 6px;
  padding: 4px 0 !important;
  min-width: 180px;
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.6);
  z-index: 100;

  .popover-row {
    padding: 8px 14px !important;
    margin: 0 !important;
    font-size: 12px;
    cursor: pointer;
    color: #cccccc !important;
    transition: background-color 0.1s, color 0.1s;

    &:hover { 
      background-color: #2a2a2a !important; 
      color: #ffffff !important; 
    }
    &.alert-action { 
      color: #f43f5e !important; 
      &:hover { 
        background-color: #e11d48 !important; 
        color: #ffffff !important; 
      } 
    }
  }

  .popover-divider {
    height: 1px;
    background-color: #333333 !important;
    margin: 4px 0 !important;
  }
}

// Global forced overrides to eliminate light grey text on light OS themes
.dark-forced-input {
  background-color: #1a1a1a !important;
  color: #ffffff !important;
  border: 1px solid #444444 !important;
  border-radius: 4px;
  padding: 8px 12px;
  
  option {
    background-color: #1a1a1a !important;
    color: #ffffff !important;
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
  
  &.destructive-action {
    background-color: rgba(231, 76, 60, 0.15);
    border: 1px solid #e74c3c;
    color: #e74c3c;
    
    &:hover { 
      background-color: #e74c3c; 
      color: #ffffff; 
    }
  }

  .btn-icon {
    width: 14px; 
    height: 14px; 
    margin-right: 6px; 
  }
}
</style>