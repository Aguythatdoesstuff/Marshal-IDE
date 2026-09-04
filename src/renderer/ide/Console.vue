<template>
  <div class="relative flex h-full w-full flex-1 flex-col overflow-hidden bg-marshal-editor font-mono text-marshal-text" @click="closeContextMenu">
    <div class="flex h-[35px] shrink-0 select-none items-center justify-between border-b border-marshal-border bg-marshal-sidebar px-3.5">
      <div class="flex h-full items-center gap-3">
        <div 
          :class="['flex h-full cursor-pointer items-center border-b-2 border-transparent px-1 text-[11px] font-semibold uppercase tracking-wider text-marshal-muted transition hover:text-white', { 'border-marshal-primary text-marshal-text': activeTab === 'console' }]" 
          @click="activeTab = 'console'"
        >
          Console
        </div>
        <div 
          v-if="showErrorsTab" 
          :class="['flex h-full cursor-pointer items-center border-b-2 border-transparent px-1 text-[11px] font-semibold uppercase tracking-wider text-marshal-muted transition hover:text-white', { 'border-marshal-primary text-marshal-text': activeTab === 'errors' }]" 
          @click="activeTab = 'errors'"
        >
          Errors 
          <span v-if="errorCount > 0" class="ml-1.5 rounded-full bg-red-500 px-1.5 py-0.5 text-[10px] font-bold text-white">{{ errorCount }}</span>
        </div>
        <div 
          v-if="showErrorsTab" 
          :class="['flex h-full cursor-pointer items-center border-b-2 border-transparent px-1 text-[11px] font-semibold uppercase tracking-wider text-marshal-muted transition hover:text-white', { 'border-marshal-primary text-marshal-text': activeTab === 'warnings' }]" 
          @click="activeTab = 'warnings'"
        >
          Warnings 
          <span v-if="warningCount > 0" class="ml-1.5 rounded-full bg-yellow-600 px-1.5 py-0.5 text-[10px] font-bold text-white">{{ warningCount }}</span>
        </div>
      </div>

      <div class="flex gap-1.5">
        <button class="flex cursor-pointer items-center rounded p-1 text-marshal-muted transition hover:bg-white/5 hover:text-white" title="Clear Console Output" @click="clearLogs">
          <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg>
        </button>
        <button class="flex cursor-pointer items-center rounded p-1 text-marshal-muted transition hover:bg-white/5 hover:text-white" :title="isMinimized ? 'Expand Console' : 'Minimize Console'" @click="$emit('toggle-minimize')">
          <svg v-if="isMinimized" class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M12 8l-6 6 1.41 1.41L12 10.83l4.59 4.58L18 14z"/></svg>
          <svg v-else class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z"/></svg>
        </button>
      </div>
    </div>

    <div v-show="!isMinimized" class="flex-1 overflow-y-auto bg-black/10 px-4 py-3" ref="consoleOutputElement">
      <div class="flex flex-col gap-1.5 text-xs leading-relaxed">
        <div 
          v-for="(log, idx) in displayLogs" 
          :key="idx" 
          :class="[
            'whitespace-pre-wrap break-words',
            {
              'cursor-pointer hover:bg-white/5': log.fileRef !== undefined,
              'text-sky-400': log.type === 'system-msg',
              'text-marshal-muted': log.type === 'info-msg' || log.type === 'info',
              'font-medium text-yellow-500': log.type === 'warning-msg' || log.type === 'compiler-warning',
              'font-medium text-red-500': log.type === 'error-msg' || log.type === 'error' || log.type === 'compiler-error'
            }
          ]"
          @click.stop="handleLogClick($event, log)"
        >
          <span v-if="log.timestamp" class="mr-1 text-marshal-muted opacity-50">[{{ log.timestamp }}] </span>{{ log.text }}
        </div>
        <div v-if="displayLogs.length === 0" class="whitespace-pre-wrap break-words italic text-marshal-muted opacity-70">
          &gt; {{ activeTab === 'errors' ? 'No syntax or compilation errors detected.' : activeTab === 'warnings' ? 'No syntax or compilation warnings detected.' : 'Console execution buffer cleared.' }}
        </div>
      </div>
    </div>

    <div 
      v-if="contextMenu.visible" 
      class="fixed z-[1000] rounded border border-marshal-border bg-marshal-sidebar py-1 shadow-lg" 
      :style="{ top: `${contextMenu.y}px`, left: `${contextMenu.x}px` }"
      @click.stop
    >
      <div class="cursor-pointer px-3.5 py-1.5 text-[11px] text-marshal-text transition hover:bg-white/10" @click="goToError">
        Go to {{ contextMenu.targetLog?.type === 'compiler-error' ? 'Error' : 'Warning' }}
      </div>
      <div class="cursor-pointer px-3.5 py-1.5 text-[11px] text-marshal-text transition hover:bg-white/10" @click="copyError">
        Copy {{ contextMenu.targetLog?.type === 'compiler-error' ? 'Error' : 'Warning' }}
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, nextTick, onMounted, onUnmounted, watch } from 'vue';

const props = defineProps({
  isMinimized: {
    type: Boolean,
    default: false
  },
  showErrorsTab: {
    type: Boolean,
    default: false
  },
  customLogs: {
    type: Array,
    default: () => null // Added safely to catch Importing.vue state
  }
});

const emit = defineEmits(['toggle-minimize', 'clear-logs']);

const consoleOutputElement = ref(null);
const localLogs = ref([]);
const activeTab = ref('console');

const contextMenu = ref({
  visible: false,
  x: 0,
  y: 0,
  targetLog: null
});

// Determines if we are using the external custom prop (Importing) or local state (IDE)
const actualLogs = computed(() => props.customLogs || localLogs.value);

// Filters the viewport rendering based on the active tab
const displayLogs = computed(() => {
  if (activeTab.value === 'errors') {
    return actualLogs.value.filter(log => log.type === 'compiler-error');
  }
  if (activeTab.value === 'warnings') {
    return actualLogs.value.filter(log => log.type === 'compiler-warning');
  }
  // Standard console tab displays absolutely everything EXCEPT the pretty compiler error/warning rows
  return actualLogs.value.filter(log => log.type !== 'compiler-error' && log.type !== 'compiler-warning');
});

// Provides dynamic count for the badge based strictly on active compiler errors
const errorCount = computed(() => {
  return actualLogs.value.filter(log => log.type === 'compiler-error').length;
});

// Provides dynamic count for the badge based strictly on active compiler warnings
const warningCount = computed(() => {
  return actualLogs.value.filter(log => log.type === 'compiler-warning').length;
});

const clearLogs = () => {
  if (props.customLogs) {
    emit('clear-logs');
  } else {
    localLogs.value = [];
  }
};

const showConsoleMessage = (message, type = 'info-msg') => {
  // If Importing is routing external logs, don't double inject here
  if (props.customLogs) return;

  localLogs.value.push({
    timestamp: new Date().toLocaleTimeString(),
    text: message,
    type: type
  });
};

const handleLogClick = (event, log) => {
  if (log.type === 'compiler-error' || log.type === 'compiler-warning') {
    contextMenu.value = {
      visible: true,
      x: event.clientX,
      y: event.clientY,
      targetLog: log
    };
  } else {
    closeContextMenu();
  }
};

const closeContextMenu = () => {
  contextMenu.value.visible = false;
  contextMenu.value.targetLog = null;
};

const goToError = () => {
  if (contextMenu.value.targetLog) {
    const log = contextMenu.value.targetLog;
    
    // Check if the global navigation function exists and fire it
    if (window.navigateToFileAndLine) {
      window.navigateToFileAndLine(log.fileRef, log.lineRef);
    }
    else
    {
      ipc.warn("Navigation function not available. Cannot go to file and line.");
    }
  }
  closeContextMenu();
};

const copyError = () => {
  if (contextMenu.value.targetLog) {
    const logText = contextMenu.value.targetLog.text;
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(logText).catch(err => {
        ipc.warn("Failed to copy to clipboard:", err);
      });
    }
  }
  closeContextMenu();
};

// Replaces the localized nextTick push to securely watch the reactive model
watch(() => displayLogs.value.length, () => {
  nextTick(() => {
    if (consoleOutputElement.value) {
      consoleOutputElement.value.scrollTop = consoleOutputElement.value.scrollHeight;
    }
  });
});

const decodeHumanReadable = (str) => {
  if (!str) return '';
  try {
    return str.replace(/\\u([\dA-Fa-f]{4})/g, (m, g) => String.fromCharCode(parseInt(g, 16)));
  } catch { return str; }
};

// Track the last file that the sync engine updated to catch empty report targets
const lastActiveFile = ref('');

const handleIncomingLog = (logData) => {
  const rawMessage = logData.message || '';
  
  // Intercept Sync-Engine context clues to know what file is actively building
  if (logData.source === 'Sync-Engine' || rawMessage.includes('[Sync-Engine]')) {
    const syncMatch = rawMessage.match(/added\/changed\s+(?:.*[\/\\])?([^\/\\]+)$/);
    if (syncMatch && syncMatch[1]) {
      lastActiveFile.value = syncMatch[1];
    }
  }

  const ipcMarker = '[[IPC]]:';
  const ipcIndex = rawMessage.indexOf(ipcMarker);

  if (ipcIndex !== -1) {
    try {
      const jsonStr = rawMessage.substring(ipcIndex + ipcMarker.length);
      const parsed = JSON.parse(jsonStr);

      if (parsed.type === 'ValidationReport') {
        const payload = parsed.payload;

        if (payload) {
          // If the compiler returns zero errors/warnings and an empty files array, wipe errors/warnings for the last active file context
          if (payload.TotalErrors === 0 && payload.TotalWarnings === 0 && (!payload.Files || payload.Files.length === 0) && lastActiveFile.value) {
            localLogs.value = localLogs.value.filter(log => !((log.type === 'compiler-error' || log.type === 'compiler-warning') && log.fileRef === lastActiveFile.value));
          } else if (payload.Files) {
            payload.Files.forEach(file => {
              let filePath = file.File || file.FilePath || 'Unknown File';
              // Normalize backslashes out of paths if needed to match clean targets
              if (filePath.includes('\\') || filePath.includes('/')) {
                filePath = filePath.split(/[\\/]/).pop();
              }

              // Clear out previous errors and warnings linked specifically to this individual file name
              localLogs.value = localLogs.value.filter(log => !((log.type === 'compiler-error' || log.type === 'compiler-warning') && log.fileRef === filePath));

              // Append new individual human-readable error rows into the array tracking state
              (file.Errors || file.errors || file.Diagnostics || []).forEach(err => {
                const errMsg = decodeHumanReadable(err.message || err.Message || err.Error || JSON.stringify(err));
                const lineNum = err.line !== undefined ? err.line : (err.Line !== undefined ? err.Line : 'Unknown');
                const lineInfo = lineNum !== 'Unknown' ? `Line ${lineNum}` : 'Unknown Line';

                localLogs.value.push({
                  timestamp: new Date().toLocaleTimeString(),
                  text: `[${filePath}] ${lineInfo}: ${errMsg}`,
                  type: 'compiler-error',
                  fileRef: filePath,
                  lineRef: lineNum
                });
              });

              // Append new individual human-readable warning rows into the array tracking state
              (file.Warnings || file.warnings || []).forEach(warn => {
                const warnMsg = decodeHumanReadable(warn.message || warn.Message || warn.Warning || JSON.stringify(warn));
                const lineNum = warn.line !== undefined ? warn.line : (warn.Line !== undefined ? warn.Line : 'Unknown');
                const lineInfo = lineNum !== 'Unknown' ? `Line ${lineNum}` : 'Unknown Line';

                localLogs.value.push({
                  timestamp: new Date().toLocaleTimeString(),
                  text: `[${filePath}] ${lineInfo}: ${warnMsg}`,
                  type: 'compiler-warning',
                  fileRef: filePath,
                  lineRef: lineNum
                });
              });
            });
          }
        }
      }
    } catch (e) {
      console.warn("[IDE] Failed to parse compiler IPC message:", e);
    }
  }

  // ABSOLUTELY UNTOUCHED RAW FALLTHROUGH: Prints the exact output log string directly into the Console layout array
  const level = logData.level ? logData.level.toLowerCase() : 'info';
  let determinedType = 'info-msg';
  
  if (level === 'error' || level === 'fatal') determinedType = 'error-msg';
  else if (level === 'warn' || level === 'warning') determinedType = 'warning-msg';
  else if (level === 'system') determinedType = 'system-msg';

  const formattedMessage = `[${logData.source || 'App'}][${level.toUpperCase()}]: ${rawMessage}`;
  showConsoleMessage(formattedMessage, determinedType);
};

const handleGlobalStreamEvent = (event) => {
  if (event.detail) {
    handleIncomingLog(event.detail);
  }
};

onMounted(() => {
  if (!props.customLogs) {
    console.log("[System] Workspace environment successfully loaded into memory layout grid.");
    window.addEventListener('marshal-runtime-log', handleGlobalStreamEvent);
    
    window.MarshalConsole = {
      log: showConsoleMessage,
      handleLog: handleIncomingLog,
      clear: clearLogs
    };
  }
});

onUnmounted(() => {
  window.removeEventListener('marshal-runtime-log', handleGlobalStreamEvent);
  if (window.MarshalConsole) delete window.MarshalConsole;
});
</script>

