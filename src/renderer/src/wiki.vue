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

      <main class="flex-1 overflow-y-auto bg-marshal-editor">
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

<style>
.wiki-wrapper {
  height: 100vh;
  overflow: hidden;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  background-color: var(--editor-bg);
  
  .titlebar { 
    margin-bottom: 0 !important; 
    height: 32px;
    background-color: color-mix(in srgb, var(--sidebar-bg), black 2%);
    border-bottom: 1px solid var(--border-color);
    display: flex;
    align-items: center;
    padding: 0 1rem;
    
    .titlebar-text {
      font-size: 0.75rem;
      color: var(--text-muted);
      font-weight: 500;
      letter-spacing: 0.05em;
    }
  } 

  .app-container {
    display: flex;
    overflow: hidden;
    height: calc(100vh - 32px); 
  }

  /* --- Sidebar Explorer Styling --- */
  .sidebar {
    width: 18rem; 
    background-color: var(--sidebar-bg);
    border-right: 1px solid var(--border-color);
    display: flex;
    flex-direction: column;
    flex-shrink: 0;
    user-select: none;

    .sidebar-header {
      padding: 1rem 0.75rem;
      display: flex;
      justify-content: space-between;
      align-items: center;

      .title {
        font-size: 0.85rem;
        font-weight: 600;
        color: #f1f5f9;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        margin: 0;
      }

      .back-btn {
        background: none;
        border: none;
        color: var(--text-muted);
        cursor: pointer;
        padding: 4px;
        border-radius: 4px;
        display: flex;
        align-items: center;
        transition: all 0.2s ease;
        
        &:hover { 
          color: #ffffff; 
          background-color: rgba(255, 255, 255, 0.05);
        }
      }
    }

    .search-box {
      padding: 0 0.75rem 0.75rem 0.75rem;
      
      .search-input {
        margin-bottom: 0 !important; 
        background-color: color-mix(in srgb, var(--sidebar-bg), white 3%);
        border: 1px solid var(--border-color); 
        border-radius: 6px;        
        padding: 0.5rem 0.75rem;   
        color: #ffffff;
        font-size: 0.85rem;
        outline: none;
        box-sizing: border-box;
        width: 100%;
        transition: all 0.2s ease;
        
        &::placeholder {
          color: #475569;          
        }
        
        &:focus {
          border-color: var(--primary-blue);
          background-color: color-mix(in srgb, var(--sidebar-bg), white 5%);
          box-shadow: 0 0 0 2px rgba(var(--primary-blue), 0.2);
        }
      }
    }

    .sidebar-nav {
      flex: 1;
      overflow-y: auto;
      padding: 0.25rem 0.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.125rem;

      /* Minimal Scrollbars */
      &::-webkit-scrollbar { width: 6px; }
      &::-webkit-scrollbar-track { background: transparent; }
      &::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.1); border-radius: 3px; }
      &::-webkit-scrollbar-thumb:hover { background: rgba(255, 255, 255, 0.2); }

      .status-text {
        padding: 1rem 0.5rem;
        color: var(--text-muted);
        font-size: 0.8rem;
        text-align: center;

        &.error { color: #f87171; font-style: italic; }
      }

      .tree-node { margin-bottom: 0.125rem; }

      .folder-btn {
        width: 100%;
        display: flex;
        align-items: center;
        padding: 0.35rem 0.5rem;
        font-size: 0.85rem; 
        font-weight: 500;
        color: #cbd5e1;
        background: none;
        border: none;
        border-radius: 4px;
        text-align: left;
        cursor: pointer;
        transition: all 0.15s ease;

        .icon {
          margin-right: 0.5rem;
          font-size: 0.9rem;
          display: inline-block;
          transition: transform 0.15s ease;
          &.open { transform: rotate(0deg); }
        }

        &:hover { 
          background-color: rgba(255, 255, 255, 0.04);
          color: #ffffff; 
        }
      }

      .folder-content {
        margin-left: 0.65rem;
        border-left: 1px solid rgba(255, 255, 255, 0.06);
        padding-left: 0.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.125rem;
        margin-top: 0.125rem;
      }

      .file-btn {
        width: 100%;
        text-align: left;
        padding: 0.35rem 0.75rem;
        border-radius: 4px;
        font-size: 0.85rem;
        color: var(--text-muted);
        background: transparent;
        border: none;
        cursor: pointer;
        display: flex;
        align-items: center;
        gap: 0.35rem;
        transition: all 0.15s ease;

        &.root-file { margin-bottom: 0.125rem; }

        &:hover {
          background-color: rgba(255, 255, 255, 0.04);
          color: #f1f5f9;
        }

        &.active-file {
          background-color: rgba(var(--primary-blue), 0.15);
          color: color-mix(in srgb, var(--primary-blue), white 15%);
          font-weight: 500;
        }
      }
    }

    .mode-tag {
      padding: 0.5rem;
      font-size: 0.65rem; 
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      text-align: center;
      border-top: 1px solid var(--border-color);
      color: color-mix(in srgb, var(--text-muted), black 10%);
      background-color: color-mix(in srgb, var(--sidebar-bg), black 1%);
    }
  }

  /* --- Main Markdown Reader Canvas --- */
  .main-content {
    flex: 1;
    overflow-y: auto;
    background-color: var(--editor-bg);
    
    &::-webkit-scrollbar { width: 10px; }
    &::-webkit-scrollbar-track { background: transparent; }
    &::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.08); border-radius: 5px; }
    &::-webkit-scrollbar-thumb:hover { background: rgba(255, 255, 255, 0.15); }

    .document-area {
      max-width: 52rem; 
      margin: 0 auto;
      padding: 4rem 3rem;
      color: var(--text-color);
    }

    .welcome-screen {
      text-align: left;
      
      h1 { 
        color: #ffffff; 
        font-size: 2.25rem;
        font-weight: 700;
        margin-bottom: 0.75rem;
      }
      
      p {
        color: var(--text-muted);
        font-size: 1.05rem;
        line-height: 1.5;
      }

      .tip-box {
        margin-top: 2.5rem;
        padding: 1rem 1.25rem;
        background-color: rgba(var(--primary-blue), 0.06);
        border: 1px solid rgba(var(--primary-blue), 0.2);
        border-radius: 6px;
        font-size: 0.9rem;
        line-height: 1.5;
        color: color-mix(in srgb, var(--primary-blue), white 25%);
        
        strong {
          color: color-mix(in srgb, var(--primary-blue), white 35%);
        }
      }
    }

    .blue-glow { color: color-mix(in srgb, var(--primary-blue), white 10%); }
  }

  /* --- Advanced Typographical Framework --- */
  .prose-content {
    line-height: 1.7;
    font-size: 1rem;
    color: #cbd5e1;

    h1, h2, h3, h4 { 
      color: #ffffff; 
      font-weight: 600; 
      line-height: 1.3;
      margin-top: 2.5rem; 
      margin-bottom: 1rem; 
    }
    
    h1 { font-size: 2.25rem; margin-top: 0; padding-bottom: 0.5rem; border-bottom: 1px solid var(--border-color);}
    h2 { font-size: 1.6rem; border-bottom: 1px solid rgba(255, 255, 255, 0.08); padding-bottom: 0.3rem; }
    h3 { font-size: 1.25rem; }
    
    p, ul, ol { margin-bottom: 1.5rem; }
    
    ul { 
      list-style-type: cubic-bezier(0,0,0,1); 
      padding-left: 1.5rem; 
      li { margin-bottom: 0.5rem; }
    }
    
    a { 
      color: color-mix(in srgb, var(--primary-blue), white 15%); 
      text-decoration: none; 
      border-bottom: 1px dashed transparent;
      transition: all 0.2s ease;
      
      &:hover { 
        border-bottom-color: color-mix(in srgb, var(--primary-blue), white 15%);
      } 
    }
    
    pre {
      background-color: color-mix(in srgb, var(--sidebar-bg), black 2%);
      border: 1px solid var(--border-color);
      border-radius: 8px;
      padding: 1.25rem;
      overflow-x: auto;
      margin: 1.75rem 0;

      code {
        background: transparent;
        color: #e2e8f0;
        padding: 0;
        border-radius: 0;
        font-size: 0.875rem;
        font-family: 'Fira Code', 'Consolas', 'Courier New', monospace;
      }
    }

    code {
      color: #f43f5e;
      background-color: rgba(244, 63, 94, 0.1);
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
      font-size: 0.9rem;
      font-family: 'Fira Code', 'Consolas', monospace;
    }
    
    blockquote {
      border-left: 4px solid var(--primary-blue);
      background-color: rgba(var(--primary-blue), 0.04);
      padding: 0.75rem 1.25rem;
      margin: 1.75rem 0;
      border-radius: 0 6px 6px 0;
      color: #94a3b8;
      
      p { margin-bottom: 0; }
    }
  }

  /* System Performance Animations */
  .animate-pulse {
    animation: corePulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
  }

  @keyframes corePulse {
    0%, 100% { opacity: 1; }
    50% { opacity: .4; }
  }
}
</style>