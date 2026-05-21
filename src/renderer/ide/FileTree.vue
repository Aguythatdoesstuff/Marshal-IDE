<template>
  <aside class="sidebar-column" :style="{ width: sidebarWidth + 'px' }">
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
            :class="['tree-item-row', { 'is-active': activePath === node.path }]" 
            :style="{ paddingLeft: (node.depth * 12 + 8) + 'px' }"
            @click="handleNodeClick(node)"
            @contextmenu.prevent="$emit('node-contextmenu', $event, node)"
          >
            <span class="node-icon">
              <template v-if="node.isDir">
                <span v-if="node.isExpanded">📂</span>
                <span v-else>📁</span>
              </template>
              <template v-else>📄</template>
            </span>
            <span class="node-text-label">{{ node.name }}</span>
          </div>
        </div>
      </div>
    </div>
  </aside>
</template>

<script setup>
import { ref, onMounted } from 'vue';

const props = defineProps({
  sidebarWidth: { type: Number, default: 260 },
  activePath: { type: String, default: null }
});

const emit = defineEmits(['file-selected', 'node-contextmenu', 'trigger-create']);

const treeData = ref([]);

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
  const rootNodes = await loadDirectory('');
  treeData.value = rootNodes.sort((a, b) => b.isDir - a.isDir || a.name.localeCompare(b.name));
};

const handleNodeClick = async (node) => {
  if (node.isDir) {
    node.isExpanded = !node.isExpanded;
    if (node.isExpanded) {
      const childNodes = await loadDirectory(node.path);
      childNodes.sort((a, b) => b.isDir - a.isDir || a.name.localeCompare(b.name));
      
      const targetIndex = treeData.value.findIndex(n => n.path === node.path);
      if (targetIndex !== -1) {
        treeData.value.splice(targetIndex + 1, 0, ...childNodes);
      }
    } else {
      treeData.value = treeData.value.filter(n => !n.path.startsWith(node.path + '/') && !n.path.startsWith(node.path + '\\'));
    }
  } else {
    emit('file-selected', node);
  }
};

defineExpose({ refreshTree });

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
      &.is-active { background-color: rgba(0, 122, 204, 0.2); color: #ffffff; border-left: 2px solid $primary-blue; }
      .node-icon { margin-right: 6px; font-size: 12px; display: inline-flex; align-items: center; }
      .node-text-label { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    }
  }
}
</style>