<template>
  <div :class="['ide-frame', { 'layout-resizing': isResizing }]">
    
    <div class="window-titlebar">
      <span class="title-string">Marshal IDE - Workspace Template Layout</span>
    </div>

    <header class="app-header">
      <div class="meta-section">
        <h1 class="brand-title">Marshal IDE</h1>
        <div class="vertical-divider"></div>
        <span class="active-project-tag">Project Node: <span class="highlight">Active_Mod</span></span>
      </div>
      
      <div class="controls-section">
        <button class="ui-btn primary-action">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M17 3H5C3.89 3 3 3.89 3 5v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>
          Save
        </button>
        <button class="ui-btn standard-action">Exit Workspace</button>
      </div>
    </header>

    <main class="workspace-viewport" id="app-workspace-split-root">
      
      <aside class="sidebar-column" :style="{ width: sidebarWidth + 'px' }">
        <div class="column-header">
          <span class="header-label">Project Files</span>
          <button class="header-action-btn" title="Import Asset Bundle">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
          </button>
        </div>
        
        <div id="file-tree-container" class="tree-inner-scroller">
        </div>
      </aside>
      
      <div class="pane-divider-v" @mousedown="startWorkspaceResize"></div>

      <section class="main-editor-console-hub">
        
        <div class="editor-subview-frame">
          <div id="editor-container" class="monaco-mount-target">
            <div class="placeholder-screen">
              <p>Select a file within the file browser to start.</p>
            </div>
          </div>
        </div>
        
        <div class="pane-divider-h" @mousedown="startConsoleResize"></div>

        <div class="console-subview-dock" :style="{ height: isConsoleMinimized ? '35px' : consoleHeight + 'px' }">
          <ConsolePanel 
            class="console-panel-component"
            :is-minimized="isConsoleMinimized"
            @toggle-minimize="handleToggleMinimize" 
          />
        </div>

      </section>
    </main>

    <div class="floating-popover-menu" :style="{ top: '120px', left: '280px', display: 'none' }">
      <div class="popover-row">New Script Element File...</div>
      <div class="popover-row">Force Reload Tree Directory</div>
      <div class="popover-divider"></div>
      <div class="popover-row">Rename Structural Path...</div>
      <div class="popover-row alert-action">Delete Permanently</div>
    </div>

    <div class="modal-system-dimmer" v-if="false">
      <div class="modal-window-card">
        <h3 class="modal-heading">Modal Dialog Title</h3>
        <p class="modal-body">Are you sure you want to run this structural system action?</p>
        
        <div class="modal-input-container">
          <input type="text" class="modal-text-field" placeholder="Target designation parameter..." />
        </div>
        
        <div class="modal-actions-row">
          <button class="ui-btn standard-action">Abort</button>
          <button class="ui-btn primary-action">Commit Action</button>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { ref } from 'vue';
import ConsolePanel from './Console.vue'; // <-- EXPLICIT SYSTEM REGISTRATION

// Isolated layout states
const sidebarWidth = ref(260);
const consoleHeight = ref(240);
const isConsoleMinimized = ref(false);
const isResizing = ref(false);

const startWorkspaceResize = (e) => {
  isResizing.value = true;
  const processMouseMove = (moveEvent) => {
    sidebarWidth.value = Math.min(Math.max(moveEvent.clientX, 180), window.innerWidth * 0.6);
  };
  const processMouseUp = () => {
    isResizing.value = false;
    window.removeEventListener('mousemove', processMouseMove);
    window.removeEventListener('mouseup', processMouseUp);
  };
  window.addEventListener('mousemove', processMouseMove);
  window.addEventListener('mouseup', processMouseUp);
};

const startConsoleResize = (e) => {
  isResizing.value = true;
  const startHeight = consoleHeight.value;
  const startY = e.clientY;

  const processMouseMove = (moveEvent) => {
    const deltaY = moveEvent.clientY - startY;
    const targetHeight = startHeight - deltaY;
    
    // Auto-minimize if dragged lower than 45px threshold height
    if (targetHeight < 45) {
      isConsoleMinimized.value = true;
    } else {
      if (isConsoleMinimized.value) isConsoleMinimized.value = false;
      consoleHeight.value = Math.min(Math.max(targetHeight, 45), window.innerHeight * 0.7);
    }
  };

  const processMouseUp = () => {
    isResizing.value = false;
    window.removeEventListener('mousemove', processMouseMove);
    window.removeEventListener('mouseup', processMouseUp);
  };

  window.addEventListener('mousemove', processMouseMove);
  window.addEventListener('mouseup', processMouseUp);
};

const handleToggleMinimize = () => {
  isConsoleMinimized.value = !isConsoleMinimized.value;
};
</script>

<style lang="scss" scoped>
// Safely inherit alias mappings from main.scss
$sidebar-bg: var(--sidebar-bg);
$editor-bg: var(--editor-bg);
$card-bg: var(--card-bg);
$text-color: var(--text-color);
$primary-blue: var(--primary-blue);
$border-color: var(--border-color);
$text-muted: var(--text-muted);

.ide-frame {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
  overflow: hidden;
  background-color: $sidebar-bg;
  color: $text-color;
  font-family: 'Inter', sans-serif;

  &.layout-resizing {
    cursor: col-resize !important;
    user-select: none !important;
  }
}

.window-titlebar {
  height: 32px;
  background-color: $editor-bg; 
  color: $text-muted;
  display: flex;
  justify-content: center; 
  align-items: center;
  -webkit-app-region: drag;
  width: 100%;           
  flex-shrink: 0;        
  border-bottom: 1px solid $border-color;
  margin-bottom: 0;

  .title-string {
    font-size: 0.75rem; 
    font-weight: 500;   
    opacity: 0.8;
  }
}

.app-header {
  height: 52px;
  background-color: rgba(0, 0, 0, 0.2); 
  border-bottom: 1px solid $border-color;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  flex-shrink: 0;

  .meta-section {
    display: flex;
    align-items: center;
    
    .brand-title {
      font-size: 14px;
      font-weight: 700;
      letter-spacing: -0.01em;
      margin: 0;
      color: #ffffff;
    }

    .vertical-divider {
      width: 1px;
      height: 16px;
      background-color: $border-color;
      margin: 0 16px;
    }

    .active-project-tag {
      font-size: 12px;
      color: $text-muted;
      
      .highlight { 
        color: $text-color; 
        font-weight: 600; 
      }
    }
  }

  .controls-section {
    display: flex;
    gap: 8px;
  }
}

.workspace-viewport {
  display: flex;
  flex: 1;
  overflow: hidden;
  width: 100%;
}

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

    .header-label {
      font-size: 11px;
      font-weight: 600;
      color: $text-muted;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .header-action-btn {
      background: rgba(255, 255, 255, 0.06); 
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: $text-color;
      cursor: pointer;
      padding: 3px 4px; 
      border-radius: 4px;
      display: flex;
      align-items: center;
      transition: background-color 0.15s, color 0.15s, border-color 0.15s;

      svg { 
        width: 18px; 
        height: 18px; 
      }
      &:hover { 
        color: #ffffff; 
        background-color: rgba(0, 122, 204, 0.25); 
        border-color: $primary-blue;
      }
    }
  }

  .tree-inner-scroller {
    flex: 1;
    overflow-y: auto;
  }
}

.pane-divider-v {
  width: 4px;
  background-color: $border-color;
  cursor: col-resize;
  z-index: 20;
  transition: background-color 0.15s;
  flex-shrink: 0;
  
  &:hover {
    background-color: $primary-blue;
  }
}

.main-editor-console-hub {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
  background-color: $editor-bg;
}

.editor-subview-frame {
  flex: 1;
  position: relative;
  overflow: hidden;
}

.monaco-mount-target {
  width: 100%;
  height: 100%;
  position: relative;

  .placeholder-screen {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: $text-muted;
    font-size: 13px;
    padding: 24px;
    text-align: center;
  }
}

.pane-divider-h {
  height: 4px;
  background-color: $border-color;
  cursor: row-resize;
  z-index: 20;
  transition: background-color 0.15s;
  flex-shrink: 0;
  
  &:hover {
    background-color: $primary-blue;
  }
}

.console-subview-dock {
  border-top: 1px solid $border-color;
  flex-shrink: 0;
  overflow: hidden;
  display: flex !important;          
  flex-direction: column !important; 
  width: 100%;
  background-color: $editor-bg;

  /* Force the locally imported component layout to follow structural dimensions */
  .console-panel-component {
    display: flex !important;
    flex-direction: column !important;
    flex: 1 !important;
    height: 100% !important;
    width: 100% !important;
  }
}

.floating-popover-menu {
  position: fixed;
  background-color: $card-bg;
  border: 1px solid $border-color;
  border-radius: 4px;
  padding: 4px 0;
  min-width: 180px;
  box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5);
  z-index: 100;

  .popover-row {
    padding: 8px 14px;
    font-size: 12px;
    cursor: pointer;
    color: $text-color;
    transition: background-color 0.1s, color 0.1s;

    &:hover { 
      background-color: $primary-blue; 
      color: #ffffff; 
    }
    &.alert-action { 
      color: #f43f5e; 
      &:hover { 
        background-color: #e11d48; 
        color: #ffffff; 
      } 
    }
  }

  .popover-divider {
    height: 1px;
    background-color: $border-color;
    margin: 4px 0;
  }
}

.modal-system-dimmer {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(2px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 200;

  .modal-window-card {
    background-color: $card-bg;
    border: 1px solid $border-color;
    border-radius: 6px;
    padding: 24px;
    width: 100%;
    max-width: 380px;
    box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.7);

    .modal-heading { 
      font-size: 15px; 
      font-weight: 600; 
      margin: 0 0 10px 0; 
      color: #ffffff;
    }
    .modal-body { 
      font-size: 13px; 
      color: $text-muted; 
      line-height: 1.5; 
      margin: 0 0 16px 0; 
    }
    
    .modal-input-container {
      margin-bottom: 20px;
      
      .modal-text-field {
        width: 100%; 
        background-color: rgba(0, 0, 0, 0.2); 
        border: 1px solid $border-color;
        color: #ffffff; 
        padding: 8px 12px; 
        font-size: 13px; 
        border-radius: 4px; 
        box-sizing: border-box;
        
        &:focus { 
          outline: none; 
          border-color: $primary-blue; 
        }
      }
    }

    .modal-actions-row {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
  }
}

.ui-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 6px 14px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  border: none;
  cursor: pointer;
  transition: opacity 0.1s, background-color 0.1s;

  &:hover { 
    opacity: 0.9; 
  }
  
  &.primary-action { 
    background-color: $primary-blue; 
    color: #ffffff; 
  }
  &.standard-action { 
    background-color: rgba(255, 255, 255, 0.05); 
    color: $text-color; 
    border: 1px solid $border-color;
    
    &:hover {
      background-color: rgba(255, 255, 255, 0.1);
    }
  }

  .btn-icon { 
    width: 14px; 
    height: 14px; 
    margin-right: 6px; 
  }
}
</style>