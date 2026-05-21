import { FILE_EXTENSION_MAP } from './components/hoi4/hoi4Config.js';
import { loadDirectoryTree, refreshFolder } from './fileTree.js';

export const STATE = {
    PROJECT_NAME: null,
    INPUT_DIR: null,
    OUTPUT_DIR: null,
    CURRENT_FILE_PATH: null,
    CURRENT_PROJECT_CONFIG: null,
    GLOBAL_COMPILERS: null,
    MONACO_EDITOR: null,
    IS_DIRTY: false,
    ACTIVE_RESIZER: null,
    CONTEXT_PATH: null,
    pendingAction: null, // For modal operations
};

// Keep the object structure identical so your external scripts don't break
export const ELEMENTS = {
    fileTreeContainer: null,
    editorConsoleContainer: null,
    editorContainer: null,
    sidebarResizer: null,
    importImageBtn: null,
    sidebar: null,
    saveFileBtn: null,
    backToModsBtn: null,
    contextMenu: null,
    actionModal: null,
    actionTitle: null,
    actionMessage: null,
    actionInput: null,
    actionInputContainer: null,
    confirmActionBtn: null,
    cancelActionBtn: null,
};

/**
 * Initializes DOM elements hooks safely after the Vue UI mounts.
 * This prevents null runtime exceptions during IPC load-project events.
 */
export function initializeElements() {
    ELEMENTS.fileTreeContainer = document.getElementById('file-tree-container');
    ELEMENTS.editorContainer = document.getElementById('editor-container');
    
    ELEMENTS.sidebarResizer = document.querySelector('.resizer-v');
    ELEMENTS.sidebar = document.querySelector('.sidebar');
    ELEMENTS.saveFileBtn = document.querySelector('.action-group .btn-primary'); // Targets your Vue save button
}


/**
 * Normalizes paths by replacing backslashes with forward slashes
 * and removing trailing slashes to prevent logic errors.
 */
export function normalizePath(path) {
    if (typeof path !== 'string') return '';
    return path.replace(/\\/g, '/').replace(/\/+$/, '');
}

/**
 * Updates the UI and internal state when the editor content changes.
 */
export function setDirtyState(isDirty) {
    STATE.IS_DIRTY = isDirty;
    if (ELEMENTS.saveFileBtn) ELEMENTS.saveFileBtn.disabled = !isDirty;

    let title = 'Marshal IDE - Mod';
    if (STATE.PROJECT_NAME) {
         title = `Marshal IDE - ${STATE.PROJECT_NAME}`;
    }
    
    if (STATE.CURRENT_FILE_PATH) {
        title = `${title} - ${STATE.CURRENT_FILE_PATH.split('/').pop()}`;
    }
    
    if (isDirty) {
        title = `* ${title}`;
    }
    
    // Safely look up the element before setting textContent
    const windowTitleEl = document.getElementById('window-title');
    if (windowTitleEl) {
        windowTitleEl.textContent = title;
    }
}


/**
 * Loads the project configuration after selection.
 */
export async function loadProject(projectName) {
    window.api.log.info(`[System] Attempting to load project: ${projectName}...`, 'ide-Renderer');
    
    try {
        const result = await window.api.invoke('load-project', projectName);
        
        if (result.success) {
            STATE.PROJECT_NAME = projectName;
            STATE.CURRENT_PROJECT_CONFIG = result.config;
            STATE.GLOBAL_COMPILERS = result.globalCompilers;
            STATE.INPUT_DIR = result.config.input_dir;
            
            // document.getElementById('current-project-display').textContent = `Project: ${projectName}`;
            
            window.api.log.info(`[SUCCESS] Project configuration loaded.`, 'ide-Renderer');
            // Update title
            setDirtyState(false);
            loadDirectoryTree(projectName); 
            
        } else {
            window.api.log.error(`[ERROR] Failed to load project config: ${result.message}`, 'ide-Renderer');
            setTimeout(() => window.api.send('switch-page', 'index'), 2000);
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC Error on load-project: ${error.message}`, 'ide-Renderer');
        setTimeout(() => window.api.send('switch-page', 'index'), 2000);
    }
}

/**
 * Handles saving the currently active file.
 */
export async function saveActiveFile() {
    if (!STATE.CURRENT_FILE_PATH || !STATE.MONACO_EDITOR || !STATE.IS_DIRTY) {
        window.api.log.warn("[WARNING] No active, dirty file selected or editor not ready.", 'ide-Renderer');
        return;
    }
    
    const content = STATE.MONACO_EDITOR.getValue();
    
    window.api.log.info(`[System] Saving file: ${STATE.CURRENT_FILE_PATH.split('/').pop()}...`, 'ide-Renderer');
    ELEMENTS.saveFileBtn.disabled = true;

    try {
        const result = await window.api.invoke('save-file', {
            filePath: STATE.CURRENT_FILE_PATH,
            content: content
        });
        
        if (result.success) {
            window.api.log.info(`[SUCCESS] ${result.message}`, 'ide-Renderer');
            STATE.MONACO_EDITOR.getModel()._initialContent = content;
            setDirtyState(false);
        } else {
            window.api.log.error(`[ERROR] Save failed: ${result.message}`, 'ide-Renderer');
            ELEMENTS.saveFileBtn.disabled = false;
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC Error on save-file: ${error.message}`, 'ide-Renderer');
        ELEMENTS.saveFileBtn.disabled = false;
    }
}

/**
 * Shared logic for the Ctrl+S shortcut to ensure consistency
 * across both the global window listener and the Monaco command.
 */
export function triggerSaveShortcut(e) {
    if (e && typeof e.preventDefault === 'export function') e.preventDefault();

    if (STATE.IS_DIRTY && !ELEMENTS.saveFileBtn.disabled) {
        saveActiveFile();
    } else if (STATE.CURRENT_FILE_PATH && !STATE.IS_DIRTY) {
        window.api.log.info("[System] File is already saved.", 'ide-Renderer');
    } else if (!STATE.CURRENT_FILE_PATH) {
        window.api.log.warn("[WARNING] Cannot save: No file is open.", 'ide-Renderer');
    }
}

// --- Window Dynamic Sizing / Resizer Event Logic handlers ---
export const startSidebarResize = (e) => {
  isResizing.value = true;
  currentResizeType = 'v';
  document.addEventListener('mousemove', handleResizeMove);
  document.addEventListener('mouseup', stopResize);
};

export const startConsoleResize = (e) => {
  if (consoleMinimized.value) {
    restoreConsole();
  }
  isResizing.value = true;
  currentResizeType = 'h';
  document.addEventListener('mousemove', handleResizeMove);
  document.addEventListener('mouseup', stopResize);
};

export const handleResizeMove = (e) => {
  if (!isResizing.value) return;
  if (currentResizeType === 'v') {
    sidebarWidth.value = Math.min(Math.max(e.clientX, 150), window.innerWidth * 0.5);
  } else if (currentResizeType === 'h') {
    const container = editorConsoleContainer.value;
    if (!container) return;
    
    const mainContentRect = container.getBoundingClientRect();
    const mouseYRelativeToMain = e.clientY - mainContentRect.top;
    const totalHeight = mainContentRect.height;
    const calculatedHeight = totalHeight - mouseYRelativeToMain;
    
    const max = totalHeight * 0.8;
    const min = MINIMIZED_HEIGHT;
    const finalHeight = Math.min(Math.max(calculatedHeight, min), max);

    consoleHeight.value = finalHeight;

    if (finalHeight > MINIMIZED_HEIGHT + 5) {
      lastConsoleHeight = finalHeight;
      consoleMinimized.value = false;
    } else {
      consoleMinimized.value = true;
    }
  }
  if (STATE.MONACO_EDITOR) requestAnimationFrame(() => STATE.MONACO_EDITOR.layout());
};

export const stopResize = () => {
  isResizing.value = false;
  currentResizeType = null;
  document.removeEventListener('mousemove', handleResizeMove);
  document.removeEventListener('mouseup', stopResize);
  if (STATE.MONACO_EDITOR) STATE.MONACO_EDITOR.layout();
};

export const restoreConsole = () => {
  if (!consoleMinimized.value) return;
  consoleHeight.value = lastConsoleHeight;
  consoleMinimized.value = false;
  if (STATE.MONACO_EDITOR) STATE.MONACO_EDITOR.layout();
};

export const toggleConsole = () => {
  if (consoleMinimized.value) {
    restoreConsole();
  } else {
    const currentHeight = consoleHeight.value;
    if (currentHeight > MINIMIZED_HEIGHT + 5) { 
      lastConsoleHeight = currentHeight;
    }
    consoleMinimized.value = true;
  }
  if (STATE.MONACO_EDITOR) setTimeout(() => STATE.MONACO_EDITOR.layout(), 50);
};

// --- Modal Configuration Management ---
export const openActionModal = (actionType, path, isDir) => {
  modal.type = actionType;
  modal.targetPath = path;
  modal.isDir = isDir;
  modal.inputValue = '';
  modal.requiresInput = false;
  modal.visible = true;

  switch (actionType) {
    case 'new-file': {
      const parentPath = isDir ? path : path.substring(0, path.lastIndexOf('/'));
      const folderName = parentPath.split('/').pop().toLowerCase();
      modal.forcedExtension = FILE_EXTENSION_MAP[folderName] || '.unknown';
      modal.targetPath = parentPath;

      modal.title = 'Create New File';
      modal.message = `Enter the new **file name**.<br/>The extension will automatically set to <code style="color:#007acc">${modal.forcedExtension}</code>.`;
      modal.placeholder = 'e.g., event_name';
      modal.requiresInput = true;
      break;
    }
    case 'rename': {
      const name = path.split(/[/\\]/).pop();
      const dotIdx = name.lastIndexOf('.');
      modal.originalExtension = dotIdx !== -1 ? name.substring(dotIdx) : '';
      modal.inputValue = dotIdx !== -1 ? name.substring(0, dotIdx) : name;

      modal.title = 'Rename File';
      modal.message = `Enter the new name for ${name} (extension is preserved automatically):`;
      modal.requiresInput = true;
      break;
    }
    case 'delete': {
      modal.title = 'Delete File';
      modal.message = `Are you sure you want to permanently delete: <strong>${path.split('/').pop()}</strong>? This action is permanent!`;
      break;
    }
  }

  if (modal.requiresInput) {
    nextTick(() => modalInput.value?.focus());
  }
};

export const closeModal = () => {
  modal.visible = false;
};

export const confirmModalAction = async () => {
  let result = { success: false, message: 'Action initialization exception.' };
  const cleanInput = modal.inputValue.trim();

  if (modal.requiresInput && !cleanInput) {
    alert("Input name field cannot remain blank.");
    return;
  }

  modal.visible = false;

  if (modal.type === 'new-file') {
    let name = cleanInput;
    if (!name.includes(modal.forcedExtension)) {
      if (name.includes('.')) name = name.substring(0, name.lastIndexOf('.'));
      name += modal.forcedExtension;
    }
    const filePath = `${modal.targetPath}/${name}`;
    try {
      result = await window.api.invoke('create-file', { filePath });
      if (result.success) {
        refreshFolder(modal.targetPath);
        loadFileContent(filePath);
      }
    } catch (err) { result.message = err.message; }
  }
  else if (modal.type === 'rename') {
    const oldPath = normalizePath(modal.targetPath);
    const finalName = cleanInput + modal.originalExtension;
    const pathParts = oldPath.split('/');
    pathParts.pop();
    const parentDir = pathParts.join('/');
    const newPath = normalizePath(`${parentDir}/${finalName}`);

    if (oldPath === newPath) return;

    try {
      result = await window.api.invoke('rename-file', { oldFilePath: oldPath, newFilePath: newPath });
      if (result.success) {
        if (normalizePath(currentFilePath.value) === oldPath) {
          STATE.CURRENT_FILE_PATH = newPath;
          setDirtyState(false);
        }
        refreshFolder(parentDir || projectName.value);
      }
    } catch (err) { result.message = err.message; }
  }
  else if (modal.type === 'delete') {
    const target = normalizePath(modal.targetPath);
    try {
      result = await window.api.invoke('delete-file-or-dir', { path: target });
      if (result.success) {
        if (normalizePath(currentFilePath.value) === target) {
          STATE.MONACO_EDITOR.setValue('File deleted. Select another file.');
          STATE.MONACO_EDITOR.updateOptions({ readOnly: true });
          STATE.CURRENT_FILE_PATH = null;
          setDirtyState(false);
        }
        const parts = target.split('/');
        parts.pop();
        refreshFolder(parts.join('/') || projectName.value);
      }
    } catch (err) { result.message = err.message; }
  }

  if (result.success) window.api.log.info(`[SUCCESS] ${result.message}`, 'ide-Renderer');
  else window.api.log.error(`[ERROR] Action failed: ${result.message}`, 'ide-Renderer');
};

// Synchronize Internal States with projectManager references 
export const syncStateWithCore = () => {
  projectName.value = STATE.PROJECT_NAME;
  currentFilePath.value = STATE.CURRENT_FILE_PATH;
  isDirty.value = STATE.IS_DIRTY;
  computeWindowTitle();
};

// Override original state management assignments with native computed bridges
export const injectCoreStateBridges = () => {
  Object.defineProperty(STATE, 'PROJECT_NAME', { get: () => projectName.value, set: (v) => { projectName.value = v; syncStateWithCore(); } });
  Object.defineProperty(STATE, 'CURRENT_FILE_PATH', { get: () => currentFilePath.value, set: (v) => { currentFilePath.value = v; syncStateWithCore(); } });
  Object.defineProperty(STATE, 'IS_DIRTY', { get: () => isDirty.value, set: (v) => { isDirty.value = v; syncStateWithCore(); } });
};