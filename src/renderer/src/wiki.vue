<template>
  <div class="flex h-screen flex-col overflow-hidden bg-marshal-editor font-sans text-marshal-text">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - Wiki</span>
    </div>

    <div class="flex h-[calc(100vh-32px)] overflow-hidden">
      <aside class="flex w-72 shrink-0 select-none flex-col border-r border-marshal-border bg-marshal-sidebar">
        <div class="flex items-center justify-between p-3">
          <h1 class="text-sm font-semibold uppercase tracking-wider text-slate-100">Wiki Explorer</h1>
          <button v-if="isAppMode" @click="goBack" class="flex cursor-pointer items-center rounded p-1 text-marshal-muted transition hover:bg-white/5 hover:text-white" title="Go Back">
            <svg class="h-[18px] w-[18px]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
          </button>
        </div>

        <div class="px-3 pb-3">
          <input 
            v-model="searchQuery" 
            type="text" 
            placeholder="Search titles or content..." 
            class="w-full rounded-md border border-marshal-border bg-white/5 px-3 py-2 text-sm text-white outline-none placeholder:text-slate-600 focus:border-marshal-primary" 
          />
        </div>

        <nav class="flex flex-1 flex-col gap-0.5 overflow-y-auto px-2 py-1">
          <div v-if="isLoadingTree" class="p-4 text-center text-xs text-marshal-muted animate-pulse">Initializing documentation...</div>
          <div v-else-if="initError" class="p-4 text-center text-xs italic text-red-400">{{ initError }}</div>
          <div v-else-if="filteredTree.length === 0" class="p-4 text-center text-xs text-marshal-muted">No documents found.</div>

          <div v-for="item in filteredTree" :key="item.path || item.name" class="mb-0.5">
            
            <template v-if="item.type === 'folder'">
              <button @click="toggleFolder(item.name)" class="flex w-full items-center rounded px-2 py-1.5 text-left text-sm font-medium text-slate-300 transition hover:bg-white/5 hover:text-white">
                <span class="mr-2 text-sm">📂</span> 
                {{ item.name }}
              </button>
              
              <div v-show="!collapsedFolders[item.name]" class="ml-2 flex flex-col gap-0.5 border-l border-white/10 pl-1">
                <button 
                  v-for="child in item.children" 
                  :key="child.path"
                  class="flex w-full items-center gap-1.5 rounded px-3 py-1.5 text-left text-sm text-marshal-muted transition hover:bg-white/5 hover:text-slate-100"
                  :class="{ 'bg-marshal-primary/15 font-medium text-blue-300': activeItem?.path === child.path }"
                  @click="loadDoc(child)"
                >
                  📄 {{ child.name }}
                </button>
              </div>
            </template>

            <template v-else>
              <button 
                class="flex w-full items-center gap-1.5 rounded px-3 py-1.5 text-left text-sm text-marshal-muted transition hover:bg-white/5 hover:text-slate-100"
                :class="{ 'bg-marshal-primary/15 font-medium text-blue-300': activeItem?.path === item.path }" 
                @click="loadDoc(item)"
              >
                📄 {{ item.name }}
              </button>
            </template>

          </div>
        </nav>

        <div class="border-t border-marshal-border bg-black/5 p-2 text-center text-[10px] font-semibold uppercase tracking-wider text-slate-500">{{ modeText }}</div>
      </aside>

      <main class="main-content flex-1 overflow-y-auto bg-marshal-editor">
        <article class="mx-auto max-w-4xl px-12 py-16 text-marshal-text">
          <div v-if="isLoadingDoc" class="animate-pulse text-marshal-primary">Reading file...</div>
          <div v-else-if="docError" class="text-red-400">
            <h1>Error Loading Document</h1>
            <p>{{ docError }}</p>
          </div>
          
          <div v-else-if="parsedMarkdown" class="prose-content" v-html="parsedMarkdown"></div>
          
          <div v-else class="text-left">
            <h1 class="mb-3 text-4xl font-bold text-white">Welcome to Marshal Wiki</h1>
            <p class="text-lg leading-relaxed text-marshal-muted">Select a document from the explorer to begin viewing your project documentation.</p>
            <div class="mt-10 rounded-md border border-marshal-primary/20 bg-marshal-primary/5 px-5 py-4 text-sm leading-relaxed text-blue-300">
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

