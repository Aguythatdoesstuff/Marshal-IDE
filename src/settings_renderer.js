// settings_renderer.js

// --- Global State & Elements ---

const TAB_IDS = ['workspace', 'logger'];
let hasUnsavedChanges = false;
let initialSettingsData = {
    logger: {}
}; // Stores loaded settings for comparison

const DEFAULT_SETTINGS = {
    logger: {
        max_archived_mb: 20, 
        MIN_LOG_SIZE: 10,    // Application-defined minimum
    }
};

const ELEMENTS = {
    // Tab Navigation
    settingsContent: document.getElementById('settings-content'),
    workspaceModList: document.getElementById('workspace-mod-list'),
    
    // Buttons
    closeBtn: document.getElementById('close-settings-btn'),
    saveBtn: document.getElementById('save-settings-btn'),
    resetAllBtn: document.getElementById('reset-all-settings-btn'), 
    
    // Logger Settings
    maxLogSizeInput: document.getElementById('max-log-size-input'),

    // Modals
    unsavedModal: document.getElementById('unsaved-changes-modal'),
    saveAndCloseBtn: document.getElementById('save-and-close-btn'),
    discardChangesBtn: document.getElementById('discard-changes-btn'),
    cancelCloseBtn: document.getElementById('cancel-close-btn'),
};


// --- Tab Switching Logic ---

/**
 * Switches the active tab in the settings view.
 * @param {string} tabName - The ID suffix of the tab to activate (e.g., 'workspace').
 */
function switchTab(tabName) {
    window.api.log.info(`Switching to settings tab: ${tabName}`, 'Renderer-Settings-UI');
    
    TAB_IDS.forEach(id => {
        const tabElement = document.getElementById(`tab-${id}`);
        const contentElement = document.getElementById(`content-${id}`);

        if (id === tabName) {
            tabElement.classList.add('active');
            contentElement.classList.remove('hidden');
        } else {
            tabElement.classList.remove('active');
            contentElement.classList.add('hidden');
        }
    });

    // Special action: if switching to workspace, load the mod list
    if (tabName === 'workspace') {
        loadWorkspaceModList();
    }
}



/**
 * Loads Logger settings from the main process on page load.
 */
async function loadGlobalSettings() {
    window.api.log.info('Requesting global logger settings.', 'Renderer-Settings-IPC');
    try {
        const result = await window.api.invoke('load-all-global-settings');
        
        if (result.success) {
            window.api.log.info('Successfully loaded global settings.', 'Renderer-Settings-IPC');
            
            // Store the initial state for change detection
            initialSettingsData = result.settings;
            
            // Populate Logger Settings
            ELEMENTS.maxLogSizeInput.value = initialSettingsData.logger.max_archived_mb;

            setUnsavedChanges(false); // Set initial state to "saved"
        } else {
            window.api.log.error(`[ERROR] Failed to load global settings: ${result.message}`, 'Renderer-Settings-IPC');
            alert(`Error: Could not load settings. ${result.message}`);
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC call 'load-all-global-settings' failed: ${error.message}`, 'Renderer-Settings-IPC');
        alert(`Critical Error: Failed to contact main process. ${error.message}`);
    }
}


// --- State Management ---

/**
 * Sets the unsaved changes flag and updates the Save button visual.
 * @param {boolean} state 
 */
function setUnsavedChanges(state) {
    hasUnsavedChanges = state;
    if (state) {
        ELEMENTS.saveBtn.classList.add('unsaved-indicator');
        ELEMENTS.saveBtn.textContent = 'Save Changes *';
        window.api.log.warning('[UNSAVED] Settings state changed to UNSAVED.', 'Renderer-Settings-State');
    } else {
        ELEMENTS.saveBtn.classList.remove('unsaved-indicator');
        ELEMENTS.saveBtn.textContent = 'Save Global Settings';
        window.api.log.info('Settings state changed to SAVED.', 'Renderer-Settings-State');
    }
}

/**
 * Attaches event listeners to all relevant settings inputs.
 */
function initializeInputListeners() {

    // Logger listener
    ELEMENTS.maxLogSizeInput.addEventListener('input', () => setUnsavedChanges(true));

    // Attach listeners to all individual Reset buttons
    document.querySelectorAll('.vscode-button-reset').forEach(button => {
        button.addEventListener('click', handleResetToDefault);
    });
}


// --- Reset Functionality ---

/**
 * Handles clicks on individual Reset to Default buttons.
 * @param {Event} e 
 */
function handleResetToDefault(e) {
    const settingId = e.currentTarget.dataset.settingId;
    window.api.log.warning(`[RESET] Attempting to reset setting: ${settingId} to default.`, 'Renderer-Settings-Reset');

    // 💡 MODIFIED: Using standardized keys from DEFAULT_SETTINGS
    switch (settingId) {
        case 'logger-max-size':
            ELEMENTS.maxLogSizeInput.value = DEFAULT_SETTINGS.logger.max_archived_mb;
            window.api.log.info('Logger max size reset to default.', 'Renderer-Settings-Reset');
            break;
        default:
            window.api.log.error(`[ERROR] Unknown setting ID for reset: ${settingId}`, 'Renderer-Settings-Reset');
            return;
    }
    
    setUnsavedChanges(true);
}

/**
 * Resets all settings to their default values.
 */
function handleResetAllToDefault() {
    // NOTE: Using window.alert() for simplicity.
    const confirmed = confirm('Are you sure you want to reset ALL global settings to default? This cannot be undone!');
    
    if (!confirmed) {
        window.api.log.info('User cancelled "Reset All" action.', 'Renderer-Settings-Reset');
        return;
    }

    ELEMENTS.maxLogSizeInput.value = DEFAULT_SETTINGS.logger.max_archived_mb;

    window.api.log.warning('[RESET] All global settings reset to default.', 'Renderer-Settings-Reset');
    setUnsavedChanges(true);
}


// --- Tab-Specific Logic: Workspace Settings ---

/**
 * Renders the list of mods and their output directories for batch editing.
 */
async function loadWorkspaceModList() {
    ELEMENTS.workspaceModList.innerHTML = '<div class="text-gray-400 p-2">Loading workspace data...</div>';
    window.api.log.info('Requesting workspace project list.', 'Renderer-Settings-IPC');

    try {
        const result = await window.api.invoke('list-projects', {});
        
        if (result.success && Object.keys(result.projects).length > 0) {
            ELEMENTS.workspaceModList.innerHTML = '';
            
            Object.keys(result.projects).forEach(projectName => {
                const projectConfig = result.projects[projectName];
                const currentOutputDir = projectConfig.output_dir || ''; // Use empty string if not set

                const row = document.createElement('div');
                row.className = 'grid grid-cols-4 gap-4 items-center bg-gray-700 hover:bg-gray-600/50 rounded p-2';
                row.innerHTML = `
                    <span class="truncate font-medium text-white col-span-1">${projectName}</span>
                    <input type="text" value="${currentOutputDir}" id="output-dir-${projectName}" data-mod-name="${projectName}" 
                           class="col-span-2 p-1 rounded bg-gray-600 text-white border border-gray-500 focus:outline-none focus:border-primary-blue text-sm mod-output-path" readonly>
                    <button id="edit-dir-${projectName}" data-mod-name="${projectName}" 
                            class="bg-gray-600 hover:bg-gray-500 text-white font-semibold py-1 px-3 rounded text-sm justify-self-end workspace-edit-btn">
                        Edit
                    </button>
                `;
                ELEMENTS.workspaceModList.appendChild(row);
            });
            window.api.log.info(`${Object.keys(result.projects).length} projects rendered in workspace settings tab.`, 'Renderer-Settings-UI');
            
            document.querySelectorAll('.workspace-edit-btn').forEach(button => {
                button.addEventListener('click', handleWorkspaceEdit);
            });

            // NOTE: Listen for changes on path inputs (e.g., manual edit, though not recommended)
            // This is a failsafe. The primary trigger is handleWorkspaceEdit.
            document.querySelectorAll('.mod-output-path').forEach(input => {
                input.addEventListener('input', () => setUnsavedChanges(true));
            });

        } else {
            ELEMENTS.workspaceModList.innerHTML = '<div class="text-gray-400 p-2">No mods found. Create one on the main screen to configure its output directory.</div>';
            window.api.log.warning('[WARNING] No projects found to display in workspace settings.', 'Renderer-Settings-UI');
        }
    } catch (error) {
        ELEMENTS.workspaceModList.innerHTML = `<div class="text-red-400 p-2">Error loading mod list: ${error.message}</div>`;
        window.api.log.error(`[ERROR] Failed to load workspace list for settings: ${error.message}`, 'Renderer-Settings-IPC');
    }
}

/**
 * Handles the directory selection dialog for a specific mod's output path.
 * @param {Event} e - The click event from the edit button.
 */
async function handleWorkspaceEdit(e) {
    const projectName = e.currentTarget.dataset.modName;
    const inputElement = document.getElementById(`output-dir-${projectName}`);
    
    // Temporarily indicate that the field is waiting for path
    inputElement.classList.add('border-warning-red');
    
    window.api.log.info(`Opening directory dialog for mod: ${projectName}`, 'Renderer-Settings-UI');
    
    try {
        // Invoke the IPC call to open the directory selection dialog
        const result = await window.api.invoke('open-directory-dialog', { defaultPath: inputElement.value });
        
        // Revert temporary visual state
        inputElement.classList.remove('border-warning-red');

        if (result.success && result.path && result.path !== inputElement.value) {
            // Update the input field with the new path
            inputElement.value = result.path;
            
            // Set unsaved changes flag since the path was updated by the user
            setUnsavedChanges(true); 
            window.api.log.info(`New output path selected for ${projectName}: ${result.path}`, 'Renderer-Settings-UI');
        } else if (!result.path) {
            // If result.path is falsey, the user cancelled
             window.api.log.warning("[WARNING] Directory dialog cancelled or path selection failed.", 'Renderer-Settings-UI');
        } else {
            // Path selected was the same as before
            window.api.log.info(`Path for ${projectName} remains unchanged.`, 'Renderer-Settings-UI');
        }
    } catch (error) {
        inputElement.classList.remove('border-warning-red');
        window.api.log.error(`[ERROR] IPC call 'open-directory-dialog' failed: ${error.message}`, 'Renderer-Settings-IPC');
    }
}


// --- Global Button Handlers ---

/**
 * Collects all settings data and calls the Main Process to save it.
 * This includes all modified workspace output paths.
 * @param {boolean} shouldClose - If true, closes the page after successful save.
 */
async function saveGlobalSettings(shouldClose = false) {
    window.api.log.info('Attempting to save global settings.', 'Renderer-Settings-Save');
    
    ELEMENTS.saveBtn.disabled = true;

    // 1. Collect Logger Settings & Validate (MINIMUM LOG SIZE CHECK)
    const maxLogSize = parseInt(ELEMENTS.maxLogSizeInput.value);
    const MIN_LOG_SIZE = DEFAULT_SETTINGS.logger.MIN_LOG_SIZE; 
    
    if (isNaN(maxLogSize) || maxLogSize < MIN_LOG_SIZE) {
        alert(`Max Archived Log Size must be a number greater than or equal to ${MIN_LOG_SIZE} MB.`);
        window.api.log.error(`[ERROR] Save failed: Max Log Size ${maxLogSize} is below minimum of ${MIN_LOG_SIZE} MB.`, 'Renderer-Settings-Validation');
        ELEMENTS.saveBtn.disabled = false;
        ELEMENTS.maxLogSizeInput.focus();
        return;
    }

    // Collect Workspace Changes (All current output paths)
    const workspaceUpdates = {};
    document.querySelectorAll('.mod-output-path').forEach(input => {
        const projectName = input.dataset.modName;
        // The value property holds the path selected by handleWorkspaceEdit
        workspaceUpdates[projectName] = input.value; 
    });

    const settingsData = {
        logger: {
            max_archived_mb: maxLogSize,
        },
        // This data structure is handled by the 'save-global-settings' IPC handler
        workspaceUpdates: workspaceUpdates 
    };
    
    window.api.log.info("Settings payload ready:", JSON.stringify(settingsData), 'Renderer-Settings-Payload');
    
    // --- Perform Save Operation ---
    let saveResult = { success: false, message: 'IPC call failed or unknown error.' };
    try {
        // Send the settings data, including workspace path updates, to the main process for persistence
        saveResult = await window.api.invoke('save-global-settings', settingsData);
        window.api.log.info(`IPC Response from 'save-global-settings': ${JSON.stringify(saveResult)}`, 'Renderer-Settings-IPC');
    } catch (error) {
        window.api.log.error(`[ERROR] IPC invocation of 'save-global-settings' failed: ${error.message}`, 'Renderer-Settings-IPC');
        saveResult.message = error.message;
    }

    if (saveResult.success) {
        initialSettingsData.logger.max_archived_mb = maxLogSize;
        // Note: Workspace paths are re-fetched on tab switch, so no need to update them here.
        
        setUnsavedChanges(false);
        alert('Settings Saved Successfully!');
        window.api.log.info('Global settings and workspace paths saved successfully.', 'Renderer-Settings-Save');
        
        if (shouldClose) {
            closeSettingsPage();
        }
    } else {
        alert(`Failed to save settings: ${saveResult.message || 'Check logs for details.'}`);
        window.api.log.error(`[ERROR] Global settings failed to save: ${saveResult.message}`, 'Renderer-Settings-Save');
    }

    ELEMENTS.saveBtn.disabled = false;
}

/**
 * Hides the unsaved changes modal and closes the page.
 */
function closeSettingsPage() {
    ELEMENTS.unsavedModal.classList.add('hidden');
    window.api.log.info('User closed global settings page.', 'Renderer-Navigation');
    window.api.send('switch-page', 'index'); 
}

/**
 * Handles the initial click on the Close button.
 * Prompts the user to save if there are unsaved changes.
 */
function handleCloseClick() {
    if (hasUnsavedChanges) {
        window.api.log.warning('[UNSAVED] Prompting user for unsaved changes before close.', 'Renderer-Navigation');
        ELEMENTS.unsavedModal.classList.remove('hidden');
    } else {
        closeSettingsPage();
    }
}


// --- Initialization ---

window.addEventListener('load', () => {
    
    initializeInputListeners(); 

    // --- Event Listeners for Tab Navigation ---
    document.querySelectorAll('.settings-tab').forEach(tab => {
        tab.addEventListener('click', (e) => {
            switchTab(e.target.dataset.tab);
        });
    });

    // --- Event Listeners for Buttons ---
    ELEMENTS.closeBtn.addEventListener('click', handleCloseClick);
    ELEMENTS.saveBtn.addEventListener('click', () => saveGlobalSettings(false));
    ELEMENTS.resetAllBtn.addEventListener('click', handleResetAllToDefault); 

    // --- Modal Listeners ---
    ELEMENTS.saveAndCloseBtn.addEventListener('click', () => saveGlobalSettings(true));
    ELEMENTS.discardChangesBtn.addEventListener('click', closeSettingsPage); 
    ELEMENTS.cancelCloseBtn.addEventListener('click', () => {
        ELEMENTS.unsavedModal.classList.add('hidden'); 
        window.api.log.info('User canceled close action, staying on settings page.', 'Renderer-Navigation');
    });
    
    loadGlobalSettings();
    
    switchTab('workspace');
});
