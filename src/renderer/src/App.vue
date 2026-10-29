<template>
  <div id="app-wrapper" class="h-screen w-screen font-sans text-marshal-text">
    <div v-if="currentScreen === 'SPLASH'" class="flex h-screen flex-col items-center justify-center [app-region:drag]">
      <div class="flex flex-col items-center gap-[50px]">
        <div class="h-60 w-60 animate-heartbeat-star bg-contain bg-center bg-no-repeat" :style="{ backgroundImage: `url(${logoUrl})` }"></div>
        <div class="flex gap-[15px] [app-region:no-drag]">
          <div class="h-3.5 w-3.5 animate-dot-fade rounded-full bg-gray-700"></div>
          <div class="h-3.5 w-3.5 animate-dot-fade rounded-full bg-gray-700 [animation-delay:200ms]"></div>
          <div class="h-3.5 w-3.5 animate-dot-fade rounded-full bg-gray-700 [animation-delay:400ms]"></div>
        </div>
        <div class="select-none text-lg font-semibold uppercase tracking-[3px] text-marshal-muted opacity-80">{{ statusText }}</div>
      </div>
    </div>

    <Eula v-else-if="currentScreen === 'EULA'" />
    <WorkspaceSelection v-else-if="currentScreen === 'WORKSPACE'" />
    <Wiki v-else-if="currentScreen === 'WIKI'" />
    <Settings v-else-if="currentScreen === 'SETTINGS'" />
    <Ide v-else-if="currentScreen === 'IDE'" />
    <Importing v-else-if="currentScreen === 'IMPORTING'" />

    <!-- What's New Modal -->
    <div v-if="showWhatsNewModal" class="fixed inset-0 z-[9999] flex items-center justify-center bg-black/75 p-4 backdrop-blur [app-region:no-drag]">
      <div class="flex max-h-[85vh] w-full max-w-2xl flex-col rounded-lg border border-gray-700 bg-marshal-editor p-6 text-gray-200 shadow-2xl animate-modal-slide-up">
        <div class="flex items-center justify-between gap-4">
          <h2 class="text-lg font-semibold text-white">What's New in v{{ appVersion }}</h2>
          <span class="rounded-full bg-marshal-primary/20 px-2.5 py-1 text-xs font-semibold text-marshal-primary">Latest Update</span>
        </div>

        <div class="mt-4 flex min-h-0 flex-1 flex-col">
          <!-- Exact Wiki-styled content container and parser -->
          <main class="main-content min-h-0 flex-1 overflow-y-auto rounded border border-gray-700 bg-marshal-editor p-4">
            <article class="text-marshal-text">
              <div v-if="isLoadingChangelog" class="animate-pulse text-marshal-primary">Reading changelog...</div>
              <div v-else-if="changelogError" class="text-red-400">
                <h1>Error Loading Changelog</h1>
                <p>{{ changelogError }}</p>
              </div>
              <div v-else class="prose-content" v-html="parsedMarkdown"></div>
            </article>
          </main>

          <!-- Update Prompt -->
          <div v-if="updateIsReadyToInstall" class="mt-4 rounded-md border border-marshal-primary bg-sky-950/50 p-4 text-center">
            <p class="mb-3 text-sm"><strong>An update is ready!</strong> Would you like to install and restart now?</p>
            <div class="flex justify-center gap-3">
              <button @click="triggerAppUpdate" class="rounded bg-emerald-400 px-4 py-1.5 text-xs font-semibold text-gray-900 transition hover:bg-emerald-300">Update Now</button>
              <button @click="updateIsReadyToInstall = false" class="rounded border border-gray-700 px-4 py-1.5 text-xs text-gray-400 transition hover:border-gray-500 hover:text-white">Later</button>
            </div>
          </div>
        </div>

        <div class="mt-6 flex justify-center">
          <button @click="closeWhatsNew" class="rounded bg-marshal-primary px-7 py-2.5 text-sm font-semibold text-white transition hover:bg-blue-700">Got it, let's explore!</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue';
import logoIcon from '../../../build/icon.png';
import Eula from './Eula.vue';
import WorkspaceSelection from './WorkspaceSelection.vue';
import Wiki from './wiki.vue';
import Settings from './settings.vue';
import Ide from './ide.vue';
import Importing from './Importing.vue';

// --- Reactive State ---
const currentScreen = ref('SPLASH');
const statusText = ref('Starting Marshal IDE');
const logoUrl = logoIcon;
const showWhatsNewModal = ref(false);
const pendingWhatsNew = ref(false);
const appVersion = ref('');
const updateIsReadyToInstall = ref(false);

const isLoadingChangelog = ref(false);
const changelogError = ref('');
const parsedMarkdown = ref('');

// --- Exact Native Markdown-to-HTML Compiler from Wiki.vue ---
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

// --- Load Changelog exactly like Wiki.vue loads docs ---
const loadChangelog = async () => {
  isLoadingChangelog.value = true;
  changelogError.value = '';
  parsedMarkdown.value = '';

  try {
    let markdown = "";
    if (window.api && window.api.invoke) {
      markdown = await window.api.invoke('read-doc-file', 'About Marshal IDE/Changelog.md');
    } else {
      markdown = "# Changelog\nEnvironment API not available.";
    }
    
    parsedMarkdown.value = parseMarkdownNative(markdown);
  } catch (e) {
    changelogError.value = e.message;
  } finally {
    isLoadingChangelog.value = false;
  }
};

watch(showWhatsNewModal, (newVal) => {
  if (newVal) {
    loadChangelog();
  }
});

// --- Lifecycle (matching original options API hooks) ---
onMounted(async () => {
  if (window.api && window.api.on) {
    window.api.on('update-ready-interactive', () => { 
      updateIsReadyToInstall.value = true; 
      showWhatsNewModal.value = true; 
    });
  }

  if (localStorage.getItem('FORCE_SCREEN') === 'WIKI') { 
    currentScreen.value = 'WIKI'; 
    localStorage.removeItem('FORCE_SCREEN'); 
    return; 
  }

  if (!window.api || !window.api.invoke) { 
    console.error('Electron IPC bridge is not available.'); 
    currentScreen.value = 'EULA'; 
    return; 
  }

  try {
    const bootState = await window.api.invoke('get-boot-state');
    statusText.value = 'Loading Environment...';
    appVersion.value = bootState.currentVersion || '1.0.0';
    
    if (bootState.enforceEula) { 
      currentScreen.value = 'EULA'; 
      pendingWhatsNew.value = bootState.showWhatsNew; 
    } else { 
      currentScreen.value = 'WORKSPACE'; 
      showWhatsNewModal.value = bootState.showWhatsNew; 
    }
  } catch (error) { 
    console.error('Failed to fetch boot state:', error); 
    currentScreen.value = 'EULA'; 
  }

  window.api.on('eula-accepted-success', () => { 
    currentScreen.value = 'WORKSPACE'; 
    if (pendingWhatsNew.value) { 
      showWhatsNewModal.value = true; 
      pendingWhatsNew.value = false; 
    } 
  });

  window.api.on('navigate-to', targetScreen => { 
    if (targetScreen) currentScreen.value = targetScreen.toUpperCase(); 
  });
});

const closeWhatsNew = async () => { 
  showWhatsNewModal.value = false; 
  if (window.api && window.api.invoke) { 
    try { 
      await window.api.invoke('dismiss-whats-new'); 
    } catch (error) { 
      console.error("Failed to dismiss What's New modal:", error); 
    } 
  } 
};

const triggerAppUpdate = async () => { 
  if (window.api && window.api.invoke) { 
    try { 
      await window.api.invoke('execute-immediate-install'); 
    } catch (error) { 
      console.error('Failed to invoke immediate installation routine:', error); 
    } 
  } 
};
</script>

<style scoped>
/* Matching Wiki.vue prose styles */
.prose-content :deep(h1) {
  font-size: 1.75rem;
  font-weight: 700;
  color: #ffffff;
  margin-top: 1rem;
  margin-bottom: 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  padding-bottom: 0.25rem;
}

.prose-content :deep(h2) {
  font-size: 1.35rem;
  font-weight: 600;
  color: #60a5fa;
  margin-top: 1.25rem;
  margin-bottom: 0.5rem;
}

.prose-content :deep(h3) {
  font-size: 1.1rem;
  font-weight: 600;
  color: #e2e8f0;
  margin-top: 1rem;
  margin-bottom: 0.35rem;
}

.prose-content :deep(ul) {
  list-style-type: disc;
  padding-left: 1.5rem;
  margin-bottom: 0.75rem;
}

.prose-content :deep(li) {
  margin-bottom: 0.35rem;
  color: #cbd5e1;
}

.prose-content :deep(p) {
  margin-bottom: 0.75rem;
  line-height: 1.6;
  color: #94a3b8;
}

.prose-content :deep(code) {
  background-color: rgba(255, 255, 255, 0.1);
  padding: 0.15rem 0.35rem;
  border-radius: 0.25rem;
  font-family: monospace;
  font-size: 0.85em;
  color: #f87171;
}

.prose-content :deep(pre) {
  background-color: rgba(0, 0, 0, 0.3);
  padding: 1rem;
  border-radius: 0.375rem;
  overflow-x: auto;
  margin-bottom: 0.75rem;
}

.prose-content :deep(a) {
  color: #38bdf8;
  text-decoration: underline;
}
</style>