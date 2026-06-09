<template>
  <div class="console-panel">
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
        <div v-for="(log, idx) in displayLogs" :key="idx" :class="['log-line', log.type]">
          <span v-if="log.timestamp" class="log-time">[{{ log.timestamp }}] </span>{{ log.text }}
        </div>
        <div v-if="displayLogs.length === 0" class="log-line empty-msg">
          &gt; {{ activeTab === 'errors' ? 'No syntax or compilation errors detected.' : 'Console execution buffer cleared.' }}
        </div>
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

// Determines if we are using the external custom prop (Importing) or local state (IDE)
const actualLogs = computed(() => props.customLogs || localLogs.value);

// Filters the viewport rendering based on the active tab
const displayLogs = computed(() => {
  if (activeTab.value === 'errors') {
    return actualLogs.value.filter(log => log.type === 'error-msg' || log.type === 'error');
  }
  return actualLogs.value;
});

// Provides dynamic count for the badge
const errorCount = computed(() => {
  return actualLogs.value.filter(log => log.type === 'error-msg' || log.type === 'error').length;
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

// Replaces the localized nextTick push to securely watch the reactive model
watch(() => displayLogs.value.length, () => {
  nextTick(() => {
    if (consoleOutputElement.value) {
      consoleOutputElement.value.scrollTop = consoleOutputElement.value.scrollHeight;
    }
  });
});

const handleIncomingLog = (logData) => {
  const level = logData.level ? logData.level.toLowerCase() : 'info';
  let determinedType = 'info-msg';
  
  if (level === 'error' || level === 'fatal') determinedType = 'error-msg';
  else if (level === 'warn' || level === 'warning') determinedType = 'warning-msg';
  else if (level === 'system') determinedType = 'system-msg';

  const formattedMessage = `[${logData.source || 'App'}][${level.toUpperCase()}]: ${logData.message}`;
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
  flex: 1;                
  height: 100%;
  width: 100%;
  background-color: $editor-bg;
  color: $text-color;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  overflow: hidden;       
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
    gap: 12px;

    .tab-label {
      font-size: 11px;
      font-weight: 600;
      color: $text-muted;
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
        color: $text-color;
        border-bottom-color: $primary-blue;
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
    &.info-msg, &.info { color: $text-muted; }
    &.warning-msg { color: #cca700; }
    &.error-msg, &.error { color: #f44747; font-weight: 500; }
    &.empty-msg { color: $text-muted; font-style: italic; opacity: 0.7; }
  }
}
</style>