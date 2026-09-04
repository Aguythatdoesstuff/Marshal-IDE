<template>
  <div class="console-panel" @click="closeContextMenu">
    <div class="console-header">
      <div class="tab-group">
        <div 
          :class="['tab-label', { active: activeTab === 'console' }]" 
          @click="activeTab = 'console'"
        >
          Console
        </div>
        <div 
          v-if="showErrorsTab" 
          :class="['tab-label', { active: activeTab === 'errors' }]" 
          @click="activeTab = 'errors'"
        >
          Errors 
          <span v-if="errorCount > 0" class="error-badge">{{ errorCount }}</span>
        </div>
        <div 
          v-if="showErrorsTab" 
          :class="['tab-label', { active: activeTab === 'warnings' }]" 
          @click="activeTab = 'warnings'"
        >
          Warnings 
          <span v-if="warningCount > 0" class="warning-badge">{{ warningCount }}</span>
        </div>
      </div>

      <div class="control-group">
        <button class="icon-action-btn" title="Clear Console Output" @click="clearLogs">
          <svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg>
        </button>
        <button class="icon-action-btn" :title="isMinimized ? 'Expand Console' : 'Minimize Console'" @click="$emit('toggle-minimize')">
          <svg v-if="isMinimized" viewBox="0 0 24 24" fill="currentColor"><path d="M12 8l-6 6 1.41 1.41L12 10.83l4.59 4.58L18 14z"/></svg>
          <svg v-else viewBox="0 0 24 24" fill="currentColor"><path d="M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z"/></svg>
        </button>
      </div>
    </div>

    <div v-show="!isMinimized" class="console-body" ref="consoleOutputElement">
      <div class="log-stream-container">
        <div 
          v-for="(log, idx) in displayLogs" 
          :key="idx" 
          :class="['log-line', log.type, { clickable: log.fileRef !== undefined }]"
          @click.stop="handleLogClick($event, log)"
        >
          <span v-if="log.timestamp" class="log-time">[{{ log.timestamp }}] </span>{{ log.text }}
        </div>
        <div v-if="displayLogs.length === 0" class="log-line empty-msg">
          &gt; {{ activeTab === 'errors' ? 'No syntax or compilation errors detected.' : activeTab === 'warnings' ? 'No syntax or compilation warnings detected.' : 'Console execution buffer cleared.' }}
        </div>
      </div>
    </div>

    <div 
      v-if="contextMenu.visible" 
      class="context-menu" 
      :style="{ top: `${contextMenu.y}px`, left: `${contextMenu.x}px` }"
      @click.stop
    >
      <div class="context-menu-item" @click="goToError">
        Go to {{ contextMenu.targetLog?.type === 'compiler-error' ? 'Error' : 'Warning' }}
      </div>
      <div class="context-menu-item" @click="copyError">
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

<style scoped>

.console-panel {
  display: flex;
  flex-direction: column;
  flex: 1;                
  height: 100%;
  width: 100%;
  background-color: var(--editor-bg);
  color: var(--text-color);
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  overflow: hidden;
  position: relative;
}

.console-header {
  height: 35px;
  background-color: var(--sidebar-bg);
  border-bottom: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 14px;
  user-select: none;
  flex-shrink: 0;

  .tab-group {
    display: flex;
    align-items: center;
    height: 100%;
    gap: 12px;

    .tab-label {
      font-size: 11px;
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      border-bottom: 2px solid transparent;
      height: 100%;
      display: flex;
      align-items: center;
      padding: 0 4px;
      cursor: pointer;
      transition: color 0.15s, border-color 0.15s;

      &:hover { color: #ffffff; }
      &.active {
        color: var(--text-color);
        border-bottom-color: var(--primary-blue);
      }

      .error-badge {
        margin-left: 6px;
        background-color: #f44747;
        color: white;
        padding: 2px 6px;
        border-radius: 8px;
        font-size: 10px;
        font-weight: 700;
      }

      .warning-badge {
        margin-left: 6px;
        background-color: #cca700;
        color: white;
        padding: 2px 6px;
        border-radius: 8px;
        font-size: 10px;
        font-weight: 700;
      }
    }
  }

  .control-group {
    display: flex;
    gap: 6px;

    .icon-action-btn {
      background: transparent;
      border: none;
      color: var(--text-muted);
      padding: 4px;
      cursor: pointer;
      border-radius: 4px;
      display: flex;
      align-items: center;
      transition: color 0.1s, background-color 0.1s;

      svg { width: 14px; height: 14px; }
      &:hover { color: #ffffff; background-color: rgba(255, 255, 255, 0.05); }
    }
  }
}

.console-body {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  background-color: rgba(0, 0, 0, 0.12);

  .log-stream-container {
    display: flex;
    flex-direction: column;
    gap: 5px;
    font-size: 12px;
    line-height: 1.5;
  }

  .log-line {
    white-space: pre-wrap;
    word-break: break-all;

    &.clickable {
      cursor: pointer;

      &:hover {
        background-color: rgba(255, 255, 255, 0.05);
      }
    }

    .log-time {
      color: var(--text-muted);
      opacity: 0.5;
      margin-right: 4px;
    }

    &.system-msg { color: #4fc1ff; }
    &.info-msg, &.info { color: var(--text-muted); }
    &.warning-msg, &.compiler-warning { color: #cca700; font-weight: 500; }
    &.error-msg, &.error, &.compiler-error { color: #f44747; font-weight: 500; }
    &.empty-msg { color: var(--text-muted); font-style: italic; opacity: 0.7; }
  }
}

.context-menu {
  position: fixed;
  background-color: var(--sidebar-bg);
  border: 1px solid var(--border-color);
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.3);
  border-radius: 4px;
  padding: 4px 0;
  z-index: 1000;

  .context-menu-item {
    padding: 6px 14px;
    font-size: 11px;
    cursor: pointer;
    color: var(--text-color);
    transition: background-color 0.1s;

    &:hover {
      background-color: rgba(255, 255, 255, 0.1);
    }
  }
}
</style>