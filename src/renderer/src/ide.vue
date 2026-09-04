<template>
  <div :class="['flex h-screen w-screen flex-col overflow-hidden bg-marshal-sidebar font-sans text-marshal-text', { 'cursor-col-resize select-none': isResizing }]">
    
    <div class="flex h-8 w-full shrink-0 items-center justify-center border-b border-marshal-border bg-marshal-editor text-marshal-muted [app-region:drag]">
      <span class="text-xs font-medium opacity-80">Marshal IDE - Workspace Template Layout</span>
    </div>

    <header class="flex h-[52px] shrink-0 items-center justify-between border-b border-marshal-border bg-black/20 px-4">
      <div class="flex items-center">
        <h1 class="m-0 text-sm font-bold text-white">Marshal IDE</h1>
        <div class="mx-4 h-4 w-px bg-marshal-border"></div>
        <span class="text-xs text-marshal-muted">Project: <span class="font-semibold text-marshal-text">{{ activeProjectName }}</span></span>
      </div>

      <div class="flex gap-2">
        <button class="inline-flex items-center justify-center gap-1.5 rounded border border-marshal-border bg-white/5 px-3.5 py-1.5 text-xs font-semibold text-marshal-text transition hover:bg-white/10 hover:opacity-90" @click="handleRecompileAll">
          <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M17.65 6.35A7.958 7.958 0 0012 4c-4.42 0-7.99 3.58-7.99 8s3.57 7.99 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"/></svg>
          Recompile
        </button>
        <button class="inline-flex items-center justify-center gap-1.5 rounded border border-marshal-border bg-white/5 px-3.5 py-1.5 text-xs font-semibold text-marshal-text transition hover:bg-white/10 hover:opacity-90" @click="handleImportImage">
          <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 5H5l3.5-4.5z"/></svg>
          Import Img
        </button>
        <button class="inline-flex items-center justify-center gap-1.5 rounded bg-marshal-primary px-3.5 py-1.5 text-xs font-semibold text-white transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50" @click="saveActiveFile" :disabled="!activeTabPath">
          <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M17 3H5C3.89 3 3 3.89 3 5v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>
          Save
        </button>
        <button class="inline-flex items-center justify-center rounded border border-marshal-border bg-white/5 px-3.5 py-1.5 text-xs font-semibold text-marshal-text transition hover:bg-white/10 hover:opacity-90" @click="handleExitWorkspace">Exit Workspace</button>
      </div>
    </header>

    <main class="flex w-full flex-1 overflow-hidden" id="app-workspace-split-root">
      
      <FileTreePanel 
        ref="fileTreeRef"
        :sidebar-width="sidebarWidth"
        :active-path="activeTabPath"
        @file-selected="handleFileSelection"
        @node-contextmenu="openContextMenu"
        @trigger-create="triggerCreateModal"
        @request-delete="triggerDeleteAction"
      />
      
      <div class="pane-divider-v" @mousedown="startWorkspaceResize"></div>

      <section class="flex min-w-0 flex-1 flex-col overflow-hidden bg-marshal-editor">
        
        <div class="relative min-h-0 flex-1 overflow-hidden">
          <div v-if="openTabs.length > 0" class="flex h-[35px] shrink-0 overflow-x-auto overflow-y-hidden border-b border-marshal-border bg-black/15">
            <div 
              v-for="tab in openTabs" 
              :key="tab.path" 
              :class="['inline-flex h-full shrink-0 cursor-pointer select-none items-center border-r border-marshal-border bg-black/10 px-3.5 text-xs text-marshal-muted transition hover:bg-white/[0.02] hover:text-marshal-text', { 'border-t-2 border-t-marshal-primary bg-marshal-editor font-medium text-white': activeTabPath === tab.path }]"
              @click="setActiveTab(tab.path)"
            >
              <span class="max-w-[120px] truncate">{{ tab.name }}</span>
              <span v-if="tab.isDirty" class="ml-1.5 text-[10px] text-rose-600">●</span>
              <span class="ml-2 flex h-3.5 w-3.5 items-center justify-center rounded text-sm hover:bg-white/10 hover:text-white" @click.stop="closeTab(tab.path)">×</span>
            </div>
          </div>

          <div id="editor-container" class="monaco-mount-target" :class="{ 'pointer-events-none': isResizing }"></div>
          
          <div v-if="openTabs.length === 0" class="absolute inset-0 flex items-center justify-center p-6 text-center text-[13px] text-marshal-muted">
            <p>Select a structural project file asset from the tree browser configuration map to begin code composition.</p>
          </div>
        </div>
        
        <div class="pane-divider-h" @mousedown="startConsoleResize"></div>

        <div class="flex w-full shrink-0 flex-col overflow-hidden border-t border-marshal-border bg-marshal-editor" :style="{ height: isConsoleMinimized ? '35px' : consoleHeight + 'px' }">
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
      class="fixed z-[100] min-w-44 rounded-md border border-gray-700 bg-marshal-editor py-1 shadow-2xl" 
      v-if="contextMenu.visible" 
      :style="{ top: contextMenu.y + 'px', left: contextMenu.x + 'px', display: 'block' }"
      v-click-outside="closeContextMenu"
    >
      <div class="cursor-pointer px-3.5 py-2 text-xs text-gray-300 transition hover:bg-white/10 hover:text-white" @click="triggerCreateModal(contextMenu.targetNode?.isDir ? contextMenu.targetNode.path : contextMenu.targetNode?.path.substring(0, contextMenu.targetNode.path.lastIndexOf('/')))">New File</div>
      
      <div v-if="contextMenu.targetNode && !contextMenu.targetNode.isDir" class="cursor-pointer px-3.5 py-2 text-xs text-gray-300 transition hover:bg-white/10 hover:text-white" @click="triggerRenameModal(contextMenu.targetNode)">Rename</div>
      <div v-if="contextMenu.targetNode && !contextMenu.targetNode.isDir" class="my-1 h-px bg-gray-700"></div>
      
      <div 
        v-if="contextMenu.selectedPaths.length > 0 || (contextMenu.targetNode && !contextMenu.targetNode.isDir)" 
        class="cursor-pointer px-3.5 py-2 text-xs text-rose-400 transition hover:bg-rose-600 hover:text-white" 
        @click="triggerDeleteAction()"
      >
        Delete {{ contextMenu.selectedPaths.length > 1 ? `(${contextMenu.selectedPaths.length} items)` : '' }}
      </div>
    </div>

    <div class="fixed inset-0 z-[200] flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm" v-if="closeConfirmVisible" @keydown.esc="handleConfirmCloseCancel" tabindex="-1">
      <div class="w-full max-w-sm rounded-md border border-marshal-border bg-marshal-card p-6 shadow-2xl">
        <h3 class="mb-2 text-[15px] font-semibold text-white">Unsaved Document Changes</h3>
        <p class="mb-4 text-[13px] leading-relaxed text-marshal-muted">
          This file contains unsaved changes. Would you like to synchronize changes to disk storage before closing it?
        </p>
        <div class="mt-4 flex justify-end gap-2">
          <button class="rounded border border-marshal-border bg-white/5 px-3.5 py-1.5 text-xs font-semibold text-marshal-text hover:bg-white/10" @click="handleConfirmCloseCancel">Cancel</button>
          <button class="rounded border border-red-500 bg-red-500/15 px-3.5 py-1.5 text-xs font-semibold text-red-400 hover:bg-red-500 hover:text-white" @click="handleConfirmCloseDiscard">Discard Changes</button>
          <button class="rounded bg-marshal-primary px-3.5 py-1.5 text-xs font-semibold text-white hover:bg-blue-600" @click="handleConfirmCloseSave">Save & Close</button>
        </div>
      </div>
    </div>

    <div class="fixed inset-0 z-[200] flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm" v-if="modal.visible" ref="modalDimmerRef" @keydown.capture="handleModalKeyDown" tabindex="-1">
      <div class="w-full max-w-sm rounded-md border border-marshal-border bg-marshal-card p-6 shadow-2xl">
        <h3 class="mb-2 text-[15px] font-semibold text-white">{{ modal.title }}</h3>
        <p class="mb-4 text-[13px] leading-relaxed text-marshal-muted">{{ modal.body }}</p>
        
        <div v-if="modal.mode !== 'delete'" class="mb-5 flex flex-col gap-3.5">
          <div v-if="modal.mode === 'create'" class="flex flex-col gap-1 text-left">
            <label class="text-[11px] font-semibold uppercase tracking-wider text-gray-400">Target Folder Context</label>
            <select v-model="modal.targetNode" @change="handleFolderChange" class="w-full rounded border border-gray-700 bg-gray-900 px-3 py-2 text-sm text-white outline-none focus:border-marshal-primary">
              <option disabled value="">-- Select Target Folder --</option>
              <option v-for="folder in modal.availableFolders" :key="folder" :value="folder">{{ folder }}</option>
            </select>
          </div>

          <div class="flex flex-col gap-1 text-left">
            <label class="text-[11px] font-semibold uppercase tracking-wider text-gray-400">Name & Compiler Extension</label>
            <div class="flex w-full gap-2">
              <input type="text" v-model="modal.inputValue" :placeholder="modal.placeholder" class="min-w-0 flex-1 rounded border border-gray-700 bg-gray-900 px-3 py-2 text-sm text-white outline-none focus:border-marshal-primary">
              
              <select v-model="modal.selectedExtension" class="w-[130px] rounded border border-gray-700 bg-gray-900 px-3 py-2 font-mono text-sm font-bold text-white outline-none focus:border-marshal-primary">
                <option v-for="ext in [...new Set(Object.values(FILE_EXTENSION_MAP))]" :key="ext" :value="ext">{{ ext }}</option>
                <option value=".unknown">.unknown</option>
              </select>
            </div>
          </div>
        </div>
      
        <div class="flex justify-end gap-2">
          <button class="rounded border border-marshal-border bg-white/5 px-3.5 py-1.5 text-xs font-semibold text-marshal-text hover:bg-white/10" @click="modal.visible = false">Cancel</button>
          <button :class="['rounded px-3.5 py-1.5 text-xs font-semibold text-white', modal.mode === 'delete' ? 'border border-red-500 bg-red-500/15 text-red-400 hover:bg-red-500 hover:text-white' : 'bg-marshal-primary hover:bg-blue-600']" @click="commitModalAction">
            {{ modal.mode === 'delete' ? 'Delete' : 'Confirm' }}
          </button>
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
let editorInstance = null;
let editorStateDisposables = [];

const activeProjectName = ref('Loading...');

const getWorkspaceTabsStorageKey = () => {
  const workspaceName = localStorage.getItem('marshal_project_to_load') || activeProjectName.value;
  return `workspace_tabs_${workspaceName}`;
};

onMounted(() => {
  activeProjectName.value = localStorage.getItem('marshal_project_to_load') || 'No Active Project';
});

const handleExitWorkspace = async () => {
  try {
    await window.api.invoke('unload-project');
  } catch (err) {
    console.error('Failed to execute unmount sequence through main process routing handler:', err);
  }
};

const sidebarWidth = ref(260);
const consoleHeight = ref(240);
const isConsoleMinimized = ref(false);
const isResizing = ref(false);

const fileTreeRef = ref(null);
const openTabs = ref([]);
const activeTabPath = ref(null);
let isSwitchingTab = false;
const expandedFoldersRegistry = ref(new Set());

const closeConfirmVisible = ref(false);
const pendingClosePath = ref(null);

const contextMenu = ref({ visible: false, x: 0, y: 0, targetNode: null, selectedPaths: [] });
const modal = ref({ 
  visible: false, 
  title: '', 
  body: '', 
  placeholder: '', 
  inputValue: '', 
  mode: '', 
  targets: [], 
  targetNode: '', 
  selectedExtension: '.unknown', 
  availableFolders: [] 
});

const handleFolderChange = () => {
  if (!modal.value.targetNode) return;
  const normalized = modal.value.targetNode.replace(/\\/g, '/');
  const pathSegments = normalized.split('/');
  const parentFolder = pathSegments[pathSegments.length - 1].toLowerCase().trim();
  modal.value.selectedExtension = FILE_EXTENSION_MAP[parentFolder] || '.unknown';
  expandedFoldersRegistry.value.add(modal.value.targetNode);
};

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

watch(() => modal.value.visible, async (isVisible) => {
  if (isVisible) {
    await nextTick();
    modalDimmerRef.value?.focus();
  }
});

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
  if (isSwitchingTab) return;

  saveTabState();
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
        
        if (!modelInstance) {
          modelInstance = monacoGlobal.editor.createModel(targetTab.content, languageId, modelUri);
          modelInstance.onDidChangeContent(() => {
            const currentVal = modelInstance.getValue();
            if (targetTab.content !== currentVal) {
              targetTab.isDirty = true;
            }
          });
        }
        
        targetTab.monacoModel = modelInstance ? markRaw(modelInstance) : null;
      }

      const cleanNativeModel = toRaw(targetTab.monacoModel);
      activeEditor.setModel(cleanNativeModel);
      
      const currentLanguageId = getMonacoLanguage(path);
      
      setTimeout(() => {
        try {
          if (cleanNativeModel && typeof cleanNativeModel.isDisposed === 'function' && !cleanNativeModel.isDisposed()) {
            monacoGlobal.editor.setModelLanguage(cleanNativeModel, currentLanguageId);
          }
        } catch (langErr) {}
      }, 0);

      if (targetTab.lineNumber) {
        activeEditor.setPosition({ lineNumber: targetTab.lineNumber, column: 1 });
      }
      if (typeof targetTab.scrollTop === 'number') {
        activeEditor.setScrollTop(targetTab.scrollTop);
      }

    } catch (runtimeErr) {
    } finally {
      isSwitchingTab = false;
      saveTabState();
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
      
      if (res && (res.success !== false)) {
        const rawContent = typeof res === 'string' ? res : (res.content || '');
        const monacoGlobal = window.monaco;
        let tabModel = null;
        
        if (monacoGlobal) {
          const languageId = getMonacoLanguage(node.path);
          const modelUri = monacoGlobal.Uri.file(node.path);
          
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
          lineNumber: 1,
          scrollTop: 0,
          monacoModel: tabModel ? markRaw(tabModel) : null 
        };

        openTabs.value.push(existingTab);
      } else {
        console.error('Backend failed to read file context:', res?.message || res);
        return;
      }
    }

    await setActiveTab(node.path);
    saveTabState();

  } catch (err) {
    console.error('Failed during file selection loading sequence:', err);
  }
};

const navigateToFileAndLine = async (filePath, lineNumber, scrollTop) => {
  try {
    let actualPath = filePath;

    if (!actualPath.includes('/') && !actualPath.includes('\\')) {
      const ext = '.' + actualPath.split('.').pop().toLowerCase();
      const matchedFolder = Object.keys(FILE_EXTENSION_MAP).find(
        folder => FILE_EXTENSION_MAP[folder] === ext
      );
      if (matchedFolder) {
        actualPath = `${matchedFolder}/${actualPath}`;
      }
    }

    let targetTab = openTabs.value.find(t => t.path === actualPath);
    
    if (!targetTab) {
      const fileName = actualPath.split(/[\\/]/).pop();
      await handleFileSelection({ path: actualPath, name: fileName, isDir: false });
    } else {
      await setActiveTab(actualPath);
    }

    await nextTick();
    
    setTimeout(() => {
      const activeEditor = editorInstance || window.editorInstance;
      if (activeEditor && lineNumber !== undefined && lineNumber !== 'Unknown') {
        const parsedLine = parseInt(lineNumber, 10);
        if (!isNaN(parsedLine)) {
          activeEditor.revealLineInCenter(parsedLine);
          activeEditor.setPosition({ lineNumber: parsedLine, column: 1 });
          const parsedScrollTop = parseInt(scrollTop, 10);
          if (!isNaN(parsedScrollTop)) activeEditor.setScrollTop(parsedScrollTop);
          const restoredTab = openTabs.value.find(t => t.path === actualPath);
          if (restoredTab) {
            restoredTab.lineNumber = parsedLine;
            restoredTab.scrollTop = !isNaN(parsedScrollTop) ? parsedScrollTop : restoredTab.scrollTop;
          }
          activeEditor.focus();
        }
      }
      saveTabState();
    }, 50);

  } catch (err) {
    console.error("Failed to navigate to file/line:", err);
  }
};

const closeTab = (path) => {
  const tabIndex = openTabs.value.findIndex(t => t.path === path);
  if (tabIndex === -1) return;

  const targetTab = openTabs.value[tabIndex];

  if (targetTab.isDirty) {
    pendingClosePath.value = path;
    closeConfirmVisible.value = true;
    return;
  }

  executeForceCloseTab(path);
};

const executeForceCloseTab = (path) => {
  const tabIndex = openTabs.value.findIndex(t => t.path === path);
  if (tabIndex === -1) return;

  const targetTab = openTabs.value[tabIndex];
  const activeEditor = editorInstance || window.editorInstance;

  if (activeTabPath.value === path && activeEditor && window.monaco) {
    if (openTabs.value.length > 1) {
      const nextIndex = tabIndex === openTabs.value.length - 1 ? tabIndex - 1 : tabIndex + 1;
      const nextTab = openTabs.value[nextIndex];
      if (nextTab && nextTab.monacoModel) {
        activeEditor.setModel(toRaw(nextTab.monacoModel));
      } else {
        const emptyModel = window.monaco.editor.createModel('', 'plaintext');
        activeEditor.setModel(emptyModel);
      }
      activeTabPath.value = openTabs.value[nextIndex].path;
    } else {
      activeTabPath.value = null;
      const emptyModel = window.monaco.editor.createModel('', 'plaintext');
      activeEditor.setModel(emptyModel);
    }
  }

  if (targetTab?.monacoModel?.dispose) {
    targetTab.monacoModel.dispose();
  } else {
    const fallbackModel = window.monaco?.editor?.getModel(window.monaco.Uri.file(path));
    if (fallbackModel) fallbackModel.dispose();
  }

  openTabs.value.splice(tabIndex, 1);
  saveTabState();
};

const handleConfirmCloseSave = async () => {
  if (!pendingClosePath.value) return;
  await setActiveTab(pendingClosePath.value);
  await saveActiveFile();
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
  
  const targetTab = openTabs.value.find(t => t.path === path);
  if (targetTab) targetTab.isDirty = false;
  
  executeForceCloseTab(path);
};

const handleConfirmCloseCancel = () => {
  pendingClosePath.value = null;
  closeConfirmVisible.value = false;
};

const saveTabState = () => {
  const activeEditor = editorInstance || window.editorInstance;
  const activeTab = openTabs.value.find(t => t.path === activeTabPath.value);

  if (activeEditor && activeTab) {
    const position = activeEditor.getPosition();
    activeTab.lineNumber = position?.lineNumber || activeTab.lineNumber || 1;
    activeTab.scrollTop = activeEditor.getScrollTop();
  }

  const payload = {
    activeTabPath: activeTabPath.value || '',
    tabs: openTabs.value.map(tab => ({
      path: tab.path,
      lineNumber: tab.lineNumber || 1,
      scrollTop: typeof tab.scrollTop === 'number' ? tab.scrollTop : 0
    }))
  };

  localStorage.setItem(getWorkspaceTabsStorageKey(), JSON.stringify(payload));
};

const restoreTabState = async () => {
  const storedState = localStorage.getItem(getWorkspaceTabsStorageKey());
  if (!storedState) return;

  try {
    const payload = JSON.parse(storedState);
    if (!Array.isArray(payload.tabs)) return;

    for (const tab of payload.tabs) {
      if (tab?.path) await navigateToFileAndLine(tab.path, tab.lineNumber, tab.scrollTop);
    }

    if (payload.activeTabPath && openTabs.value.some(tab => tab.path === payload.activeTabPath)) {
      await navigateToFileAndLine(
        payload.activeTabPath,
        openTabs.value.find(tab => tab.path === payload.activeTabPath)?.lineNumber,
        openTabs.value.find(tab => tab.path === payload.activeTabPath)?.scrollTop
      );
    }
  } catch (err) {
    console.warn('Failed to restore workspace tab state:', err);
  }
};

const saveActiveFile = async () => {
  if (!activeTabPath.value || !editorInstance) return;
  const currentTab = openTabs.value.find(t => t.path === activeTabPath.value);
  if (!currentTab) return;

  const modelToSave = currentTab.monacoModel || editorInstance.getModel();
  if (!modelToSave) return;

  const codePayload = modelToSave.getValue();
  const response = await window.api.invoke('save-file', { filePath: currentTab.path, content: codePayload });
  if (response && response.success) {
    currentTab.content = codePayload;
    currentTab.isDirty = false;
  }
};

const handleGlobalShortcuts = (e) => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
    e.preventDefault();
    saveActiveFile();
  }
};

// Context Overlay Hooks
const openContextMenu = (e, node, selectedPaths = []) => {
  const paths = selectedPaths.length > 0 ? selectedPaths : (node && !node.isDir ? [node.path] : []);
  contextMenu.value = { visible: true, x: e.clientX, y: e.clientY, targetNode: node, selectedPaths: paths };
};

const closeContextMenu = () => { 
  contextMenu.value.visible = false; 
};

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
    targets: [],
    targetNode: parentPath,
    selectedExtension: initialExt,
    availableFolders: Object.keys(FILE_EXTENSION_MAP)
  };
};

const triggerRenameModal = (node) => {
  closeContextMenu();
  if (node.isDir) return;

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
    targets: [node.path],
    targetNode: node,
    selectedExtension: originalExt,
    availableFolders: Object.keys(FILE_EXTENSION_MAP)
  };
};

// Unified Multi-File and Single-File Deletion Trigger
const triggerDeleteAction = (pathsOverride = null) => {
  closeContextMenu();

  let targetPaths = [];
  if (Array.isArray(pathsOverride) && pathsOverride.length > 0) {
    targetPaths = pathsOverride;
  } else if (contextMenu.value.selectedPaths.length > 0) {
    targetPaths = [...contextMenu.value.selectedPaths];
  } else if (contextMenu.value.targetNode && !contextMenu.value.targetNode.isDir) {
    targetPaths = [contextMenu.value.targetNode.path];
  }

  if (targetPaths.length === 0) return;

  const title = targetPaths.length > 1 ? 'Delete Multiple Files' : 'Delete File';
  const body = targetPaths.length > 1
    ? `Are you sure you want to permanently delete these ${targetPaths.length} selected files?`
    : `Are you sure you want to permanently delete "${targetPaths[0].split(/[/\\]/).pop()}"?`;

  modal.value = { 
    visible: true, 
    title, 
    body, 
    placeholder: '', 
    inputValue: '', 
    mode: 'delete', 
    targets: targetPaths,
    targetNode: contextMenu.value.targetNode,
    selectedExtension: '.unknown', 
    availableFolders: Object.keys(FILE_EXTENSION_MAP) 
  };
};

const handleRecompileAll = async () => {
  try {
    await window.api.invoke('recompile-all');
  } catch (err) {
    console.error('Failed to trigger full workspace recompilation:', err);
  }
};

const handleImportImage = async () => {
  try {
    const res = await window.api.invoke('import-image');
    if (res && res.success) {
      fileTreeRef.value?.refreshTree();
    } else if (res && res.message && !res.message.includes('cancelled')) {
      console.warn(`Import warning notice structural stack trace: ${res.message}`);
    }
  } catch (err) {
    console.error('Failed to trigger native main process asset importer:', err);
  }
};

const commitModalAction = async () => {
  const { mode, targetNode, inputValue, selectedExtension, targets } = modal.value;

  if (mode === 'create') {
    let input = inputValue.trim();
    if (!input) return;
    const lastDotIndex = input.lastIndexOf('.');
    if (lastDotIndex !== -1) input = input.substring(0, lastDotIndex);

    const cleanFileName = `${input}${selectedExtension}`;

    if (!targetNode) {
      alert('Please select a target folder first.');
      return;
    }
    const finalPath = `${targetNode}/${cleanFileName}`;
    const res = await window.api.invoke('create-file', { filePath: finalPath });
    if (res && res.success) {
      fileTreeRef.value?.refreshTree([...expandedFoldersRegistry.value]);
    }
  } 
  else if (mode === 'rename' && targetNode) {
    let input = inputValue.trim();
    if (!input) return;
    const lastDotIndex = input.lastIndexOf('.');
    if (lastDotIndex !== -1) input = input.substring(0, lastDotIndex);

    const cleanFileName = `${input}${selectedExtension}`;
    
    // Normalize path separators to accurately slice parent path across all OS formats
    const normalizedPath = targetNode.path.replace(/\\/g, '/');
    const lastSlash = normalizedPath.lastIndexOf('/');
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
  else if (mode === 'delete' && targets && targets.length > 0) {
    for (const filePath of targets) {
      const res = await window.api.invoke('delete-file-or-dir', { path: filePath });
      if (res && res.success) {
        executeForceCloseTab(filePath);
      }
    }
    fileTreeRef.value?.clearSelection();
    fileTreeRef.value?.refreshTree([...expandedFoldersRegistry.value]);
  }

  modal.value.visible = false;
};

const handleWindowResize = () => {
  const activeEd = editorInstance || window.editorInstance;
  activeEd?.layout();
};

onMounted(() => {
  window.addEventListener('keydown', handleGlobalShortcuts);
  window.addEventListener('resize', handleWindowResize);
  window.navigateToFileAndLine = navigateToFileAndLine;

  const initMonacoInstance = async () => {
    let monacoGlobal = window.monaco || (typeof monaco !== 'undefined' ? monaco : null);

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

      if (editorInstance || window.editorInstance) return;

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

        editorStateDisposables = [
          instance.onDidBlurEditorText(saveTabState),
          instance.onDidScrollChange((event) => {
            if (event.scrollTopChanged) saveTabState();
          })
        ];

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

        await restoreTabState();
      } catch (initErr) {
        console.error("[DIAGNOSTIC] Error during editor layout orchestration:", initErr);
      }
    }
  };

  initMonacoInstance();
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleGlobalShortcuts);
  window.removeEventListener('resize', handleWindowResize);
  
  if (window.navigateToFileAndLine) delete window.navigateToFileAndLine;

  if (editorInstance) {
    editorInstance.dispose();
    editorInstance = null;
  }
  editorStateDisposables.forEach(disposable => disposable.dispose());
  editorStateDisposables = [];
  if (window.editorInstance) {
    window.editorInstance = null;
  }
});

const startWorkspaceResize = (e) => {
  isResizing.value = true;
  const processMouseMove = (moveEvent) => {
    sidebarWidth.value = Math.min(Math.max(moveEvent.clientX, 180), window.innerWidth * 0.6);
    editorInstance?.layout();
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

<style scoped>

.pane-divider-v {
  width: 4px;
  background-color: var(--border-color);
  cursor: col-resize;
  z-index: 20;
  transition: background-color 0.15s;
  flex-shrink: 0;
  
  &:hover {
    background-color: var(--primary-blue);
  }
}

.monaco-mount-target {
  width: 100%;
  height: 100%;
  position: relative;
}

.pane-divider-h {
  height: 4px;
  background-color: var(--border-color);
  cursor: row-resize;
  z-index: 20;
  transition: background-color 0.15s;
  flex-shrink: 0;
  
  &:hover {
    background-color: var(--primary-blue);
  }
}

</style>