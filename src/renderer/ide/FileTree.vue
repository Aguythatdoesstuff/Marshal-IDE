<template>
  <aside 
    class="sidebar-column" 
    :style="{ width: sidebarWidth + 'px' }"
    @dragover.prevent="handleDragOver"
    @dragenter.prevent="handleDragEnter"
    @dragleave="handleDragLeave"
    @drop.prevent="handleDrop"
    :class="{ 'is-dragging-over': isDraggingOver }"
    tabindex="0"
    @keydown="handleKeyDown"
  >
    <div class="column-header">
      <span class="header-label">Project Files</span>
      <button class="header-action-btn" title="Create New Element" @click="$emit('trigger-create', '')">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"></line>
          <line x1="5" y1="12" x2="19" y2="12"></line>
        </svg>
      </button>
    </div>
    
    <div id="file-tree-container" class="tree-inner-scroller">
      <div v-if="treeData.length === 0" class="tree-empty-notice">No active project tree mapped.</div>
      <div v-else class="tree-root-nodes">
        <div v-for="node in treeData" :key="node.path" class="tree-node-wrapper">
          <div 
            :class="[
              'tree-item-row', 
              { 
                'is-active': activePath === node.path,
                'is-selected': isNodeSelected(node)
              }
            ]" 
            :style="{ paddingLeft: (node.depth * 12 + 8) + 'px' }"
            @click="handleNodeClick($event, node)"
            @contextmenu.prevent="handleContextMenu($event, node)"
          >
            <span class="node-icon">
              <template v-if="node.isDir">
                <span v-if="node.isExpanded">📂</span>
                <span v-else>📁</span>
              </template>
              <template v-else>
                <svg viewBox="0 0 24 24" fill="currentColor" :style="{ color: getFileColor(node.name) }" width="14" height="14" style="vertical-align: middle; margin-bottom: 2px;">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6zm-1 7V3.5L18.5 9H13z"/>
                </svg>
              </template>
            </span>
            <span class="node-text-label">{{ node.name }}</span>
          </div>
        </div>
      </div>
    </div>

    <div v-if="isDraggingOver" class="drop-overlay">
      <span>Drop .dds image(s) to import</span>
    </div>
  </aside>
</template>

<script setup>
import { ref, onMounted } from 'vue';

// Centralized color mapping for file extensions.
const FILE_COLOR_MAP = {
  '.decision': '#eab308',   // Yellow
  '.event': '#ef4444',      // Red
  '.scriptedgui': '#a855f7',// Purple
  '.script': '#3b82f6',     // Blue
  '.idea': '#f97316',       // Orange
  '.focus': '#22c55e',      // Green
  '.unknown': '#8a8a93'     // Muted Gray
};

const getFileColor = (fileName) => {
  const lastDotIndex = fileName.lastIndexOf('.');
  if (lastDotIndex === -1) return FILE_COLOR_MAP['.unknown'];
  
  const ext = fileName.substring(lastDotIndex).toLowerCase();
  return FILE_COLOR_MAP[ext] || FILE_COLOR_MAP['.unknown'];
};

const props = defineProps({
  sidebarWidth: { type: Number, default: 260 },
  activePath: { type: String, default: null }
});

const emit = defineEmits(['file-selected', 'node-contextmenu', 'trigger-create', 'files-deleted']);

const treeData = ref([]);

// Track paths of folders that are manually opened by the user
const openFoldersSet = ref(new Set());

// Track multi-selected file paths (Files ONLY)
const selectedFilePaths = ref(new Set());
const lastSelectedNode = ref(null);

// Drag & drop state
const isDraggingOver = ref(false);

const loadDirectory = async (dirPath = '') => {
  if (!window.api || !window.api.invoke) return [];
  const cleanPath = typeof dirPath === 'string' ? dirPath : '';
  const result = await window.api.invoke('list-directory-contents', { dirPath: cleanPath });
  if (result && result.success) {
    return result.contents.map(item => ({
      name: item.name,
      path: item.path,
      isDir: item.isDir,
      isExpanded: false,
      depth: dirPath === '' ? 0 : dirPath.split(/[/\\]/).length
    }));
  }
  return [];
};

const refreshTree = async () => {
  // 1. Fetch the root directory items
  const rootNodes = await loadDirectory('');
  const newTreeData = rootNodes.sort((a, b) => b.isDir - a.isDir || a.name.localeCompare(b.name));

  // 2. Re-hydrate any folders that were previously open by splicing their contents back in
  for (let i = 0; i < newTreeData.length; i++) {
    const node = newTreeData[i];
    
    if (node.isDir && openFoldersSet.value.has(node.path)) {
      node.isExpanded = true;
      const childNodes = await loadDirectory(node.path);
      childNodes.sort((a, b) => b.isDir - a.isDir || a.name.localeCompare(b.name));
      
      newTreeData.splice(i + 1, 0, ...childNodes);
      i += childNodes.length;
    }
  }

  // 3. Commit state
  treeData.value = newTreeData;
};

const isNodeSelected = (node) => {
  if (node.isDir) return false;
  return selectedFilePaths.value.has(node.path);
};

const handleNodeClick = async (event, node) => {
  if (node.isDir) {
    // Folders cannot be selected or deleted; handle expansion/collapse only
    node.isExpanded = !node.isExpanded;
    if (node.isExpanded) {
      openFoldersSet.value.add(node.path);
      const childNodes = await loadDirectory(node.path);
      childNodes.sort((a, b) => b.isDir - a.isDir || a.name.localeCompare(b.name));
      
      const targetIndex = treeData.value.findIndex(n => n.path === node.path);
      if (targetIndex !== -1) {
        treeData.value.splice(targetIndex + 1, 0, ...childNodes);
      }
    } else {
      openFoldersSet.value.delete(node.path);
      for (const path of openFoldersSet.value) {
        if (path.startsWith(node.path + '/') || path.startsWith(node.path + '\\')) {
          openFoldersSet.value.delete(path);
        }
      }
      treeData.value = treeData.value.filter(n => !n.path.startsWith(node.path + '/') && !n.path.startsWith(node.path + '\\'));
    }
  } else {
    // Multi-selection logic for files ONLY
    if (event.shiftKey && lastSelectedNode.value && !lastSelectedNode.value.isDir) {
      const idxA = treeData.value.findIndex(n => n.path === lastSelectedNode.value.path);
      const idxB = treeData.value.findIndex(n => n.path === node.path);

      if (idxA !== -1 && idxB !== -1) {
        const start = Math.min(idxA, idxB);
        const end = Math.max(idxA, idxB);

        // If not holding Ctrl/Cmd, clear previous selection range
        if (!event.ctrlKey && !event.metaKey) {
          selectedFilePaths.value.clear();
        }

        for (let i = start; i <= end; i++) {
          const item = treeData.value[i];
          if (!item.isDir) {
            selectedFilePaths.value.add(item.path);
          }
        }
      }
    } else if (event.ctrlKey || event.metaKey) {
      if (selectedFilePaths.value.has(node.path)) {
        selectedFilePaths.value.delete(node.path);
      } else {
        selectedFilePaths.value.add(node.path);
      }
      lastSelectedNode.value = node;
    } else {
      selectedFilePaths.value.clear();
      selectedFilePaths.value.add(node.path);
      lastSelectedNode.value = node;
      emit('file-selected', node);
    }
  }
};

const handleContextMenu = (event, node) => {
  // If right-clicking a file that isn't selected, make it the single selection.
  // If right-clicking an already selected file in a multi-selection set, keep all selections intact.
  if (!node.isDir) {
    if (!selectedFilePaths.value.has(node.path)) {
      selectedFilePaths.value.clear();
      selectedFilePaths.value.add(node.path);
      lastSelectedNode.value = node;
    }
  }

  // Pass the full selected array along with the clicked node
  emit('node-contextmenu', event, node, Array.from(selectedFilePaths.value));
};

// Batch deletion of all selected files
const deleteSelectedFiles = async () => {
  if (selectedFilePaths.value.size === 0) return;

  const pathsToDelete = Array.from(selectedFilePaths.value);
  const confirmMsg = pathsToDelete.length === 1
    ? `Are you sure you want to delete this file?`
    : `Are you sure you want to delete these ${pathsToDelete.length} files?`;

  if (window.confirm(confirmMsg)) {
    for (const filePath of pathsToDelete) {
      if (window.api && window.api.invoke) {
        await window.api.invoke('delete-element', filePath);
      }
    }
    selectedFilePaths.value.clear();
    lastSelectedNode.value = null;
    await refreshTree();
    emit('files-deleted', pathsToDelete);
  }
};

const handleKeyDown = (event) => {
  if (event.key === 'Delete' || event.key === 'Backspace') {
    deleteSelectedFiles();
  }
};

// --- Drag and Drop Handling (.dds files) ---
const handleDragOver = (e) => {
  e.preventDefault();
  isDraggingOver.value = true;
};

const handleDragEnter = (e) => {
  e.preventDefault();
  isDraggingOver.value = true;
};

const handleDragLeave = (e) => {
  if (e.currentTarget.contains(e.relatedTarget)) return;
  isDraggingOver.value = false;
};

// --- FileTree.vue ---

// --- FileTree.vue ---

const handleDrop = async (e) => {
  console.log('[FileTree] Drop event triggered:', e);
  isDraggingOver.value = false;
  
  const rawFiles = e.dataTransfer?.files ? Array.from(e.dataTransfer.files) : [];
  console.log('[FileTree] Raw dropped files count:', rawFiles.length);

  // Extract valid file paths using webUtils (cross-platform Linux/Windows support)
  const ddsFilePaths = rawFiles
    .filter(f => f.name && f.name.toLowerCase().endsWith('.dds'))
    .map(f => {
      // 1. Try Electron webUtils helper (Electron 28+)
      if (window.api && typeof window.api.getPathForFile === 'function') {
        try {
          return window.api.getPathForFile(f);
        } catch (err) {
          console.warn('[FileTree] webUtils.getPathForFile failed for file:', f.name, err);
        }
      }
      // 2. Fallback to direct path property (Legacy Electron versions)
      return f.path || null;
    })
    .filter(filePath => Boolean(filePath)); // Remove null/undefined entries

  console.log('[FileTree] Resolved .dds OS file paths:', ddsFilePaths);

  if (ddsFilePaths.length === 0) {
    console.warn('[FileTree] No valid .dds file paths could be resolved from dropped items.');
    return;
  }

  if (window.api && window.api.invoke) {
    console.log('[FileTree] Invoking "import-image" IPC with:', { sourcePath: ddsFilePaths });
    try {
      const response = await window.api.invoke('import-image', { sourcePath: ddsFilePaths });
      console.log('[FileTree] IPC "import-image" returned response:', response);
    } catch (err) {
      console.error('[FileTree] IPC "import-image" call threw an error:', err);
    }
  } else {
    console.error('[FileTree] CRITICAL: window.api or window.api.invoke is not available on renderer context!');
  }

  await refreshTree();
};

defineExpose({ 
  refreshTree,
  deleteSelectedFiles,
  getSelectedFiles: () => Array.from(selectedFilePaths.value)
});

onMounted(() => {
  refreshTree();
});
</script>

<style lang="scss" scoped>
$sidebar-bg: var(--sidebar-bg, #1e1e24);
$border-color: var(--border-color, rgba(255, 255, 255, 0.08));
$text-muted: var(--text-muted, #8a8a93);
$text-color: var(--text-color, #e2e2e9);
$primary-blue: var(--primary-blue, #007acc);

.sidebar-column {
  background-color: $sidebar-bg;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  flex-shrink: 0;
  position: relative;
  outline: none;

  &.is-dragging-over {
    border: 2px dashed $primary-blue;
  }

  .column-header {
    height: 38px;
    border-bottom: 1px solid $border-color;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 14px;
    background-color: rgba(0, 0, 0, 0.1);
    .header-label { font-size: 11px; font-weight: 600; color: $text-muted; text-transform: uppercase; letter-spacing: 0.06em; }
    .header-action-btn {
      background: rgba(255, 255, 255, 0.06); border: 1px solid rgba(255, 255, 255, 0.1); color: $text-color; cursor: pointer; padding: 4px; border-radius: 4px; display: flex; align-items: center;
      svg { width: 14px; height: 14px; }
      &:hover { background-color: rgba(0, 122, 204, 0.25); border-color: $primary-blue; color: #ffffff; }
    }
  }

  .tree-inner-scroller {
    flex: 1; overflow-y: auto; padding: 6px 0;
    .tree-empty-notice { padding: 16px; text-align: center; color: $text-muted; font-size: 12px; }
    
    .tree-item-row {
      display: flex; align-items: center; height: 26px; padding-right: 12px; cursor: pointer; font-size: 13px; color: #cccccc; user-select: none;
      &:hover { background-color: rgba(255, 255, 255, 0.04); color: #ffffff; }
      &.is-selected { background-color: rgba(0, 122, 204, 0.3); color: #ffffff; }
      &.is-active { background-color: rgba(0, 122, 204, 0.2); color: #ffffff; border-left: 2px solid $primary-blue; }
      .node-icon { margin-right: 6px; font-size: 12px; display: inline-flex; align-items: center; }
      .node-text-label { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    }
  }

  .drop-overlay {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background: rgba(0, 122, 204, 0.2);
    backdrop-filter: blur(2px);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #ffffff;
    font-size: 13px;
    font-weight: 600;
    pointer-events: none;
  }
}
</style>