<template>
  <div class="console-panel">
    <div class="console-header">
      <div class="tab-group">
        <div class="tab-label">Console</div>
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
        <div v-for="(log, idx) in logs" :key="idx" :class="['log-line', log.type]">
          <span v-if="log.timestamp" class="log-time">[{{ log.timestamp }}] </span>{{ log.text }}
        </div>
        <div v-if="logs.length === 0" class="log-line empty-msg">
          &gt; Console execution buffer cleared.
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, nextTick, onMounted, onUnmounted } from 'vue';

// Define layout properties passed down from master view frame container
defineProps({
  isMinimized: {
    type: Boolean,
    default: false
  }
});

defineEmits(['toggle-minimize']);

const consoleOutputElement = ref(null);
const logs = ref([]);

const clearLogs = () => {
  logs.value = [];
};

// Captures custom incoming execution logs and triggers autoscroll track
const showConsoleMessage = (message, type = 'info-msg') => {
  logs.value.push({
    timestamp: new Date().toLocaleTimeString(),
    text: message,
    type: type
  });
  
  nextTick(() => {
    if (consoleOutputElement.value) {
      consoleOutputElement.value.scrollTop = consoleOutputElement.value.scrollHeight;
    }
  });
};

// Maps raw stream structures into formatted UI components
const handleIncomingLog = (logData) => {
  const level = logData.level ? logData.level.toLowerCase() : 'info';
  let determinedType = 'info-msg';
  
  if (level === 'error' || level === 'fatal') determinedType = 'error-msg';
  else if (level === 'warn' || level === 'warning') determinedType = 'warning-msg';
  else if (level === 'system') determinedType = 'system-msg';

  const formattedMessage = `[${logData.source || 'App'}][${level.toUpperCase()}]: ${logData.message}`;
  showConsoleMessage(formattedMessage, determinedType);
};

// Event wrapper function to handle the Vue main.js custom browser event
const handleGlobalStreamEvent = (event) => {
  if (event.detail) {
    handleIncomingLog(event.detail);
  }
};

onMounted(() => {
  console.log("[System] Workspace environment successfully loaded into memory layout grid.");
  
  // 1. Hook directly into our clean centralized Vue stream
  window.addEventListener('marshal-runtime-log', handleGlobalStreamEvent);

  // 2. Keep legacy fallback endpoints intact for backward compatibility
  window.MarshalConsole = {
    log: showConsoleMessage,
    handleLog: handleIncomingLog,
    clear: clearLogs
  };
});

onUnmounted(() => {
  // Clean up listener paths to prevent DOM memory leaks
  window.removeEventListener('marshal-runtime-log', handleGlobalStreamEvent);
  if (window.MarshalConsole) delete window.MarshalConsole;
});
</script>

<style lang="scss" scoped>
$sidebar-bg: var(--sidebar-bg);
$editor-bg: var(--editor-bg);
$border-color: var(--border-color);
$text-color: var(--text-color);
$text-muted: var(--text-muted);
$primary-blue: var(--primary-blue);

.console-panel {
  display: flex;
  flex-direction: column;
  flex: 1;                // Explicitly consume all available room within the footer node
  height: 100%;
  width: 100%;
  background-color: $editor-bg;
  color: $text-color;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  overflow: hidden;       // Prevents structural double-scrollbar leakages
}

.console-header {
  height: 35px;
  background-color: $sidebar-bg;
  border-bottom: 1px solid $border-color;
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

    .tab-label {
      font-size: 11px;
      font-weight: 600;
      color: $text-color;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      border-bottom: 2px solid $primary-blue;
      height: 100%;
      display: flex;
      align-items: center;
      padding: 0 4px;
    }
  }

  .control-group {
    display: flex;
    gap: 6px;

    .icon-action-btn {
      background: transparent;
      border: none;
      color: $text-muted;
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

    .log-time {
      color: $text-muted;
      opacity: 0.5;
      margin-right: 4px;
    }

    &.system-msg { color: #4fc1ff; }
    &.info-msg { color: $text-muted; }
    &.warning-msg { color: #cca700; }
    &.error-msg { color: #f44747; font-weight: 500; }
    &.empty-msg { color: $text-muted; font-style: italic; opacity: 0.7; }
  }
}
</style>