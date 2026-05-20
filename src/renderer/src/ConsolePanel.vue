<template>
  <div 
    ref="consoleContainer"
    class="flex flex-col bg-[#1e1e1e] border-t border-gray-800 transition-all duration-150"
    :class="{ 'resizing': isResizing }"
    :style="{ height: `${consoleHeight}px` }"
  >
    <div 
      ref="consoleResizer"
      class="h-1 w-full cursor-row-resize bg-transparent hover:bg-primary-blue/50 transition-colors"
      @mousedown="startResizing"
      @touchstart.prevent="startResizing"
    ></div>

    <div class="flex items-center justify-between px-4 py-1 bg-[#1a1a1a] border-b border-gray-800 select-none text-xs text-gray-400">
      <div class="flex items-center space-x-2">
        <span>Console Panel</span>
      </div>
      
      <div class="flex items-center space-x-3">
        <button 
          @click.stop="clearConsole" 
          class="hover:text-white transition-colors"
          title="Clear Console"
        >
          Clear
        </button>
        
        <button 
          @click.stop="toggleMinimize" 
          class="hover:text-white transition-colors flex items-center justify-center"
          title="Toggle Console"
        >
          <span v-html="isMinimized ? icons.EXPAND : icons.COLLAPSE"></span>
        </button>
      </div>
    </div>

    <div 
      ref="consoleOutput"
      class="flex-1 overflow-y-auto p-2 font-mono text-xs space-y-1 select-text bg-[#151515]"
    >
      <div 
        v-for="(log, idx) in logs" 
        :key="idx" 
        class="p-1 border-b border-gray-900/50 leading-relaxed"
        :class="log.isError ? 'text-red-400 bg-red-950/10' : 'text-gray-300'"
      >
        <span class="text-gray-500 mr-2">[{{ log.timestamp }}]</span>
        <span>{{ log.message }}</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, onUnmounted, nextTick, watch } from 'vue';

const props = defineProps({
  // Accept the monaco editor instance from parent view to handle layout updates
  editorInstance: {
    type: Object,
    default: null
  },
  // Element selector or ref of parent bounding container used for sizing calculation
  parentContainerSelector: {
    type: String,
    default: '#editor-console-container'
  }
});

// SVG Icons
const icons = {
  EXPAND: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14l5-5 5 5z"/></svg>', 
  COLLAPSE: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor"><path d="M7 10l5 5 5-5z"/></svg>',
};

// UI DOM Element references
const consoleContainer = ref(null);
const consoleOutput = ref(null);

// Reactive State variables
const logs = ref([{ timestamp: new Date().toLocaleTimeString(), message: '[System] Console Initialized.', isError: false }]);
const isMinimized = ref(true);
const isResizing = ref(false);

const MINIMIZED_HEIGHT = 30;
const lastConsoleHeight = ref(150);
const consoleHeight = ref(30);

// Watchers to recalculate Monaco's visual dimensions smoothly
watch(consoleHeight, () => {
  triggerEditorLayout();
});

function triggerEditorLayout() {
  if (props.editorInstance) {
    requestAnimationFrame(() => props.editorInstance.layout());
  }
}

// Console utilities
function clearConsole() {
  logs.value = [{ timestamp: new Date().toLocaleTimeString(), message: '[System] Console Cleared.', isError: false }];
}

function showConsoleMessage(message, isError = false) {
  logs.value.push({
    timestamp: new Date().toLocaleTimeString(),
    message,
    isError
  });
  
  // Auto-scroll logic inside Vue nextTick lifecycle
  nextTick(() => {
    if (consoleOutput.value) {
      consoleOutput.value.scrollTop = consoleOutput.value.scrollHeight;
    }
  });
}

function restoreConsole() {
  if (!isMinimized.value) return;
  consoleHeight.value = lastConsoleHeight.value;
  isMinimized.value = false;
}

function toggleMinimize() {
  if (isMinimized.value) {
    restoreConsole();
  } else {
    if (consoleHeight.value > MINIMIZED_HEIGHT + 5) { 
      lastConsoleHeight.value = consoleHeight.value;
    }
    consoleHeight.value = MINIMIZED_HEIGHT;
    isMinimized.value = true;
  }
}

// Log listener handles
const handleIncomingLog = (logData) => {
  const isError = ['error', 'warn'].includes(logData.level.toLowerCase());
  const formattedMessage = `[${logData.source}][${logData.level.toUpperCase()}]: ${logData.message}`;
  showConsoleMessage(formattedMessage, isError);
};

// Resizing Event Pipeline
const startResizing = () => {
  if (isMinimized.value) {
    restoreConsole();
  }
  isResizing.value = true;
  document.body.style.cursor = 'row-resize';
  document.body.style.userSelect = 'none';
  
  window.addEventListener('mousemove', resizeHandler);
  window.addEventListener('mouseup', stopResizing);
  window.addEventListener('touchmove', mobileResizeHandler, { passive: false });
  window.addEventListener('touchend', stopResizing);
};

const stopResizing = () => {
  if (!isResizing.value) return;
  isResizing.value = false;
  document.body.style.cursor = '';
  document.body.style.userSelect = '';
  
  window.removeEventListener('mousemove', resizeHandler);
  window.removeEventListener('mouseup', stopResizing);
  window.removeEventListener('touchmove', mobileResizeHandler);
  window.removeEventListener('touchend', stopResizing);
  
  triggerEditorLayout();
};

const resizeHandler = (e) => {
  if (!isResizing.value) return;

  const parentEl = document.querySelector(props.parentContainerSelector);
  if (!parentEl) return;

  const mainContentRect = parentEl.getBoundingClientRect();
  const mouseYRelativeToMain = e.clientY - mainContentRect.top;
  const totalHeight = mainContentRect.height;
  const newConsoleHeight = totalHeight - mouseYRelativeToMain;

  const max = totalHeight * 0.8;
  const min = MINIMIZED_HEIGHT;
  const finalHeight = Math.min(Math.max(newConsoleHeight, min), max);

  consoleHeight.value = finalHeight;

  // Reactively track minimize bounds toggling flags
  if (finalHeight > MINIMIZED_HEIGHT + 5) {
    lastConsoleHeight.value = finalHeight;
    isMinimized.value = false;
  } else {
    isMinimized.value = true;
  }
};

const mobileResizeHandler = (e) => {
  if (!isResizing.value || !e.touches[0]) return;
  resizeHandler({ clientY: e.touches[0].clientY });
};

// Lifecycles hooks managing Electron IPC listeners cleanly
onMounted(() => {
  // Read setup configuration properties directly from CSS if available
  const rootStyle = getComputedStyle(document.documentElement);
  const cssHeightFull = parseInt(rootStyle.getPropertyValue('--console-height-full')) || 150;
  const cssHeightMin = parseInt(rootStyle.getPropertyValue('--console-height')) || 30;
  
  lastConsoleHeight.value = cssHeightFull;
  consoleHeight.value = cssHeightMin;
  isMinimized.value = true;

  if (window.api?.logBroadcaster) {
    console.log('Log Display Vue listener attached to IPC broadcast channel...');
    window.api.logBroadcaster.addListener('log', handleIncomingLog);
  }
});

onUnmounted(() => {
  if (window.api?.logBroadcaster) {
    console.log('Dismounting active Vue log broadcast listener...');
    window.api.logBroadcaster.removeListener('log', handleIncomingLog);
  }
  // Fallback cleanly to avoid leaking event listeners on window
  stopResizing();
});

// Expose internal functions if elements outside this Vue context want to push messages manually
defineExpose({
  showConsoleMessage,
  toggleMinimize,
  clearConsole
});
</script>