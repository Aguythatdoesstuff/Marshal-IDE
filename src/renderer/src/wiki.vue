<template>
  <div class="workspace-wrapper wiki-wrapper">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - Wiki</span>
    </div>

    <div class="app-container">
      <aside class="sidebar">
        <div class="sidebar-header">
          <h1 class="title">Wiki Explorer</h1>
          <button v-if="isAppMode" @click="goBack" class="back-btn" title="Go Back">
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
          </button>
        </div>

        <div class="search-box">
          <input 
            v-model="searchQuery" 
            type="text" 
            placeholder="Search titles or content..." 
            class="modal-input search-input" 
          />
        </div>

        <nav class="sidebar-nav">
          <div v-if="isLoadingTree" class="status-text animate-pulse">Initializing documentation...</div>
          <div v-else-if="initError" class="status-text error">{{ initError }}</div>
          <div v-else-if="filteredTree.length === 0" class="status-text">No documents found.</div>

          <div v-for="item in filteredTree" :key="item.path || item.name" class="tree-node">
            
            <template v-if="item.type === 'folder'">
              <button @click="toggleFolder(item.name)" class="folder-btn">
                <span class="icon" :class="{ open: !collapsedFolders[item.name] }">📂</span> 
                {{ item.name }}
              </button>
              
              <div v-show="!collapsedFolders[item.name]" class="folder-content">
                <button 
                  v-for="child in item.children" 
                  :key="child.path"
                  class="file-btn" 
                  :class="{ 'active-file': activeItem?.path === child.path }"
                  @click="loadDoc(child)"
                >
                  📄 {{ child.name }}
                </button>
              </div>
            </template>

            <template v-else>
              <button 
                class="file-btn root-file" 
                :class="{ 'active-file': activeItem?.path === item.path }" 
                @click="loadDoc(item)"
              >
                📄 {{ item.name }}
              </button>
            </template>

          </div>
        </nav>

        <div class="mode-tag">{{ modeText }}</div>
      </aside>

      <main class="main-content">
        <article class="document-area">
          <div v-if="isLoadingDoc" class="status-text animate-pulse blue-glow">Reading file...</div>
          <div v-else-if="docError" class="status-text error">
            <h1>Error Loading Document</h1>
            <p>{{ docError }}</p>
          </div>
          
          <div v-else-if="parsedMarkdown" class="prose-content" v-html="parsedMarkdown"></div>
          
          <div v-else class="welcome-screen">
            <h1>Welcome to Marshal Wiki</h1>
            <p>Select a document from the explorer to begin viewing your project documentation.</p>
            <div class="tip-box">
              <strong>Tip:</strong> You can search for keywords across all your documents using the search bar on the left.
            </div>
          </div>
        </article>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';

// Environment Detection
const isAppMode = typeof window.api !== 'undefined';
const browserData = window.BROWSER_WIKI_DATA || null;

// Reactive State
const treeData = ref([]);
const searchQuery = ref('');
const activeItem = ref(null);
const parsedMarkdown = ref('');
const collapsedFolders = ref({});

// UI Flags
const isLoadingTree = ref(true);
const isLoadingDoc = ref(false);
const initError = ref('');
const docError = ref('');
const modeText = ref('Checking Environment...');

// Native Markdown-to-HTML Compiler (Zero Dependencies)
const parseMarkdownNative = (md) => {
  let html = md;
  // Blockquotes
  html = html.replace(/^\>\s+(.*)$/gim, '<blockquote>$1</blockquote>');
  // Code Blocks
  html = html.replace(/```([\s\S]*?)```/gm, '<pre><code>$1</code></pre>');
  // Inline Code
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
  // Headers (h1 - h3)
  html = html.replace(/^# (.*)$/gim, '<h1>$1</h1>');
  html = html.replace(/^## (.*)$/gim, '<h2>$1</h2>');
  html = html.replace(/^### (.*)$/gim, '<h3>$1</h3>');
  // Bold / Italics
  html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  // Links
  html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
  // Bullet points
  html = html.replace(/^\s*-\s+(.*)$/gim, '<ul><li>$1</li></ul>');
  // Fix double list stacking wrappers
  html = html.replace(/<\/ul>\s*<ul>/g, '');
  
  return html;
};

// Initialization
const init = async () => {
  if (isAppMode) {
    modeText.value = "● LIVE MODE (APP)";
    try {
      treeData.value = await window.api.invoke('get-docs-structure');
    } catch (e) {
      initError.value = `IPC Error: ${e.message}`;
    }
  } else if (browserData) {
    modeText.value = "○ SNAPSHOT MODE (BROWSER)";
    treeData.value = browserData;
  } else {
    initError.value = "Error: No documentation data found.";
  }
  isLoadingTree.value = false;
};

const toggleFolder = (folderName) => {
  collapsedFolders.value[folderName] = !collapsedFolders.value[folderName];
};

const filterTree = (items, term) => {
  return items.map(item => {
    if (item.type === 'folder') {
      const children = filterTree(item.children, term);
      return children.length > 0 ? { ...item, children } : null;
    }
    const matchName = item.name.toLowerCase().includes(term);
    const matchContent = item.content && item.content.toLowerCase().includes(term);
    return (matchName || matchContent) ? item : null;
  }).filter(Boolean);
};

const filteredTree = computed(() => {
  const term = searchQuery.value.toLowerCase().trim();
  if (!term) return treeData.value;
  return filterTree(treeData.value, term);
});

const loadDoc = async (item) => {
  activeItem.value = item;
  isLoadingDoc.value = true;
  docError.value = '';
  parsedMarkdown.value = '';

  try {
    let markdown = "";
    if (item.content) {
      markdown = item.content;
    } else if (isAppMode) {
      markdown = await window.api.invoke('read-doc-file', item.path);
    } else {
      markdown = "# Error\nNo content available for this file.";
    }
    
    // Using the native parsing function here
    parsedMarkdown.value = parseMarkdownNative(markdown);
    
    document.querySelector('.main-content').scrollTop = 0;
  } catch (e) {
    docError.value = e.message;
  } finally {
    isLoadingDoc.value = false;
  }
};

const goBack = () => {
  if (isAppMode) window.api.send('switch-page', 'index');
};

onMounted(() => {
  init();
});
</script>