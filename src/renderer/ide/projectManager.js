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