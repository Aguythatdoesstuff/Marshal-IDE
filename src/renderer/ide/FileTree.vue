<template>
  <aside 
    class="relative flex h-full shrink-0 flex-col overflow-hidden bg-marshal-sidebar outline-none"
    :style="{ width: sidebarWidth + 'px' }"
    @dragover.prevent="handleDragOver"
    @dragenter.prevent="handleDragEnter"
    @dragleave="handleDragLeave"
    @drop.prevent="handleDrop"
    :class="{ 'border-2 border-dashed border-marshal-primary': isDraggingOver }"
    tabindex="0"
    @keydown="handleKeyDown"
  >
    <div class="flex h-[38px] shrink-0 items-center justify-between border-b border-marshal-border bg-black/10 px-3.5">
      <span class="text-[11px] font-semibold uppercase tracking-wider text-marshal-muted">Project Files</span>
      <button class="flex cursor-pointer items-center rounded border border-white/10 bg-white/5 p-1 text-marshal-text transition hover:border-marshal-primary hover:bg-marshal-primary/25 hover:text-white" title="Create New Element" @click="$emit('trigger-create', '')">
        <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"></line>
          <line x1="5" y1="12" x2="19" y2="12"></line>
        </svg>
      </button>
    </div>
    
    <div id="file-tree-container" class="min-h-0 flex-1 overflow-y-auto py-1.5">
      <div v-if="treeData.length === 0" class="p-4 text-center text-xs text-marshal-muted">No active project tree mapped.</div>
      <div v-else class="tree-root-nodes">
        <div v-for="node in treeData" :key="node.path" class="tree-node-wrapper">
          <div 
            :class="[
              'flex h-[26px] cursor-pointer select-none items-center overflow-hidden pr-3 text-[13px] text-gray-300 transition-colors hover:bg-white/5 hover:text-white',
              { 
                'border-l-2 border-marshal-primary bg-marshal-primary/20 text-white': activePath === node.path,
                'bg-marshal-primary/30 text-white': isNodeSelected(node)
              }
            ]" 
            :style="{ paddingLeft: (node.depth * 12 + 8) + 'px' }"
            @click="handleNodeClick($event, node)"
            @contextmenu.prevent="handleContextMenu($event, node)"
          >
            <span class="mr-1.5 inline-flex shrink-0 items-center text-xs">
              <template v-if="node.isDir">
                <span v-if="node.isExpanded">📂</span>
                <span v-else>📁</span>
              </template>
              <template v-else>
                <svg class="h-3.5 w-3.5 align-middle" viewBox="0 0 24 24" fill="currentColor" :style="{ color: getFileColor(node.name) }">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6zm-1 7V3.5L18.5 9H13z"/>
                </svg>
              </template>
            </span>
            <span class="truncate whitespace-nowrap">{{ node.name }}</span>
          </div>
        </div>
      </div>
    </div>

    <div v-if="isDraggingOver" class="pointer-events-none absolute inset-0 flex items-center justify-center bg-marshal-primary/20 text-[13px] font-semibold text-white backdrop-blur-sm">
      <span>Drop .dds image(s) to import</span>
    </div>
  </aside>
</template>

<script setup>
import { ref, onMounted } from 'vue';

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

const emit = defineEmits(['file-selected', 'node-contextmenu', 'trigger-create', 'request-delete']);

const treeData = ref([]);
const openFoldersSet = ref(new Set());
const selectedFilePaths = ref(new Set());
const lastSelectedNode = ref(null);
const isDraggingOver = ref(false);

const getOpenFoldersStorageKey = () => {
  const workspaceName = localStorage.getItem('marshal_project_to_load') || 'No Active Project';
  return `workspace_open_folders_${workspaceName}`;
};

const saveOpenFolders = () => {
  localStorage.setItem(getOpenFoldersStorageKey(), JSON.stringify(Array.from(openFoldersSet.value)));
};

const loadOpenFolders = () => {
  const storedFolders = localStorage.getItem(getOpenFoldersStorageKey());
  if (!storedFolders) return;

  try {
    const parsedFolders = JSON.parse(storedFolders);
    if (Array.isArray(parsedFolders)) openFoldersSet.value = new Set(parsedFolders);
  } catch (err) {
    console.warn('[FileTree] Failed to restore open folders:', err);
  }
};

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
  const buildVisibleTree = async (dirPath = '') => {
    const nodes = await loadDirectory(dirPath);
    nodes.sort((a, b) => b.isDir - a.isDir || a.name.localeCompare(b.name));

    const visibleNodes = [];
    for (const node of nodes) {
      visibleNodes.push(node);

      if (node.isDir && openFoldersSet.value.has(node.path)) {
        node.isExpanded = true;
        visibleNodes.push(...await buildVisibleTree(node.path));
      }
    }

    return visibleNodes;
  };

  treeData.value = await buildVisibleTree();
};

const isNodeSelected = (node) => {
  if (node.isDir) return false;
  return selectedFilePaths.value.has(node.path);
};

const clearSelection = () => {
  selectedFilePaths.value.clear();
  lastSelectedNode.value = null;
};

const handleNodeClick = async (event, node) => {
  if (node.isDir) {
    node.isExpanded = !node.isExpanded;
    if (node.isExpanded) {
      openFoldersSet.value.add(node.path);
      saveOpenFolders();
      await refreshTree();
    } else {
      openFoldersSet.value.delete(node.path);
      for (const path of openFoldersSet.value) {
        if (path.startsWith(node.path + '/') || path.startsWith(node.path + '\\')) {
          openFoldersSet.value.delete(path);
        }
      }
      saveOpenFolders();
      await refreshTree();
    }
  } else {
    // Multi-selection range (Shift Key)
    if (event.shiftKey && lastSelectedNode.value && !lastSelectedNode.value.isDir) {
      const idxA = treeData.value.findIndex(n => n.path === lastSelectedNode.value.path);
      const idxB = treeData.value.findIndex(n => n.path === node.path);

      if (idxA !== -1 && idxB !== -1) {
        const start = Math.min(idxA, idxB);
        const end = Math.max(idxA, idxB);

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
    } 
    // Toggle Selection (Ctrl / Cmd Key)
    else if (event.ctrlKey || event.metaKey) {
      if (selectedFilePaths.value.has(node.path)) {
        selectedFilePaths.value.delete(node.path);
      } else {
        selectedFilePaths.value.add(node.path);
      }
      lastSelectedNode.value = node;
    } 
    // Single File Selection
    else {
      selectedFilePaths.value.clear();
      selectedFilePaths.value.add(node.path);
      lastSelectedNode.value = node;
      emit('file-selected', node);
    }
  }
};

const handleContextMenu = (event, node) => {
  if (!node.isDir) {
    if (!selectedFilePaths.value.has(node.path)) {
      selectedFilePaths.value.clear();
      selectedFilePaths.value.add(node.path);
      lastSelectedNode.value = node;
    }
  } else {
    selectedFilePaths.value.clear();
  }

  emit('node-contextmenu', event, node, Array.from(selectedFilePaths.value));
};

const handleKeyDown = (event) => {
  if ((event.key === 'Delete' || event.key === 'Backspace') && selectedFilePaths.value.size > 0) {
    event.preventDefault();
    emit('request-delete', Array.from(selectedFilePaths.value));
  }
};

// Drag & Drop Import (.dds files)
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

const handleDrop = async (e) => {
  isDraggingOver.value = false;
  const rawFiles = e.dataTransfer?.files ? Array.from(e.dataTransfer.files) : [];

  const ddsFilePaths = rawFiles
    .filter(f => f.name && f.name.toLowerCase().endsWith('.dds'))
    .map(f => {
      if (window.api && typeof window.api.getPathForFile === 'function') {
        try {
          return window.api.getPathForFile(f);
        } catch (err) {
          console.warn('[FileTree] getPathForFile failed:', err);
        }
      }
      return f.path || null;
    })
    .filter(Boolean);

  if (ddsFilePaths.length === 0) return;

  if (window.api && window.api.invoke) {
    await window.api.invoke('import-image', { sourcePath: ddsFilePaths });
  }

  await refreshTree();
};

defineExpose({ 
  refreshTree,
  clearSelection,
  getSelectedFiles: () => Array.from(selectedFilePaths.value)
});

onMounted(() => {
  loadOpenFolders();
  refreshTree();
});
</script>

