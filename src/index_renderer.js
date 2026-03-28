// index_renderer.js

// --- Global State ---
let modToDeleteName = null;
let modToEditName = null; // Store name of mod being edited 

const MODAL_ELEMENTS = {
    create: document.getElementById('create-mod-modal'),
    delete: document.getElementById('delete-mod-modal'),
    outputDirInput: document.getElementById('output-dir-input'),
    confirmCreateBtn: document.getElementById('confirm-create-btn'),
    // Settings Modal Elements
    settings: document.getElementById('settings-mod-modal'),
    settingsOutputDirInput: document.getElementById('settings-output-dir-input'),
    settingsModName: document.getElementById('settings-mod-name'),
    // Wiki Modal Elements
    wiki: document.getElementById('wiki-choice-modal'),
    browserWiki: document.getElementById('open-wiki-browser-btn'),
    importChoice: document.getElementById('import-choice-modal'),
    inAppWiki: document.getElementById('open-wiki-inapp-btn')
};

// --- Import Logic ---

function showImportModal() {
    MODAL_ELEMENTS.importChoice.classList.remove('hidden');
}

function hideImportModal() {
    MODAL_ELEMENTS.importChoice.classList.add('hidden');
}

async function handleMarshalImport() {
    hideImportModal();
    const result = await window.api.invoke('import-project');
    
    if (result.success) {
        alert(`Successfully imported: ${result.projectName}`);
        // Refresh the mod list to show the new project
        const projects = await window.api.invoke('list-projects');
        renderModList(projects.projects);
    } else if (result.message) {
        alert(`Import failed: ${result.message}`);
    }
}
// --- Mod List Management ---

function renderModList(projects) {
    const listContainer = document.getElementById('mod-list');
    listContainer.innerHTML = ''; 

    const projectNames = Object.keys(projects);

    if (projectNames.length === 0) {
        listContainer.innerHTML = '<p class="text-gray-400 col-span-full">No mods found. Click "New Mod" to get started.</p>';
        return;
    }

    projectNames.forEach(projectName => {
        const projectConfig = projects[projectName];
        const currentOutputDir = projectConfig.output_dir || '';

        const card = document.createElement('div');
        card.className = 'card p-4 rounded-lg flex flex-col justify-between';
        
        card.innerHTML = `
            <div>
                <h3 class="text-xl font-bold text-white mb-2">${projectName}</h3>
                <p class="text-sm text-gray-400 mb-4">Click to open in the IDE.</p>
            </div>
            <div class="flex justify-between items-center pt-2 border-t border-gray-600">
                <button class="text-sm text-primary-blue hover:underline open-mod-btn" data-name="${projectName}">
                    Open IDE
                </button>
                <button class="text-gray-400 hover:text-white settings-mod-btn" 
                        data-name="${projectName}" 
                        data-output-path="${currentOutputDir}">
                    <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-settings"><path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"/><circle cx="12" cy="12" r="3"/></svg>
                </button>
            </div>
        `;

        card.querySelector('.open-mod-btn').addEventListener('click', (e) => openMod(e.target.dataset.name));
        card.querySelector('.settings-mod-btn').addEventListener('click', (e) => {
            const button = e.currentTarget;
            showSettingsModal(button.dataset.name, button.dataset.outputPath);
        });

        listContainer.appendChild(card);
    });
}

/**
 * Calls the main process to get the list of available projects.
 */
async function startModListListener() {
    window.api.log.info('Attempting to load available projects.', 'Renderer-Startup');
    try {
        const result = await window.api.invoke('list-projects', {});
        if (result.success) {
            renderModList(result.projects);
            window.api.log.info(`Loaded ${Object.keys(result.projects).length} projects successfully.`, 'Renderer-Startup');
        } else {
            // Display error gracefully in the list area
            document.getElementById('mod-list').innerHTML = `<p class="text-red-400 col-span-full">Error loading projects: ${result.message}</p>`;
            window.api.log.error(`Error loading projects: ${result.message}`, 'Renderer-Startup');
        }
    } catch (error) {
        // Display fatal error if IPC fails completely
        document.getElementById('mod-list').innerHTML = `<p class="text-red-400 col-span-full">FATAL: IPC call 'list-projects' failed: ${error.message}</p>`;
        window.api.log.error(`FATAL: IPC call 'list-projects' failed: ${error.message}`, 'Renderer-Startup-IPC');
    }
}


// --- Project Creation (New Mod) ---

/**
 * Opens the IDE page for the selected mod.
 */
function openMod(projectName) {
    // Log the user action
    window.api.log.info(`User opened project: ${projectName}`, 'Renderer-Navigation');
    // Save project name to localStorage for the IDE page to load it
    localStorage.setItem('marshal_project_to_load', projectName);
    // Use IPC to switch the main window's page
    window.api.send('switch-page', 'ide');
}

/**
 * Uses the new IPC handler to get the absolute logs path and opens it via the main process.
 */
function openLogsDirectory() {
    window.api.log.info(`[Renderer-UI] - User requested to open logs directory.`, 'Renderer-UI');
    
    // 1. Get the absolute path from the main process
    window.api.getLogDirectory()
        .then(result => {
            if (result.success) {
                const absoluteLogPath = result.path;
                
                // 2. Pass the absolute path to the main process to open
                window.api.openPath(absoluteLogPath)
                    .then(openResult => {
                        if (openResult.success) {
                            window.api.log.info(`[SUCCESS] Logs directory opened at: ${absoluteLogPath}`, 'Renderer-UI');
                        } else {
                            // This will show the error message from the main process (e.g., path not found)
                            window.api.log.error(`[ERROR] Failed to open logs directory: ${openResult.message}`, 'Renderer-UI');
                        }
                    })
                    .catch(openErr => {
                        window.api.log.error(`[ERROR] IPC error calling openPath: ${openErr.message}`, 'Renderer-UI');
                    });
            } else {
                window.api.log.error(`[ERROR] Failed to get log directory path: ${result.message}`, 'Renderer-UI');
            }
        })
        .catch(err => {
             window.api.log.error(`[ERROR] IPC error calling getLogDirectory: ${err.message}`, 'Renderer-UI');
        });
}

// Add this utility function to expose it, or just call openLogsDirectory() directly 
// from your UI code (e.g., in attachEventListeners).

function attachLogsButtonListener() {
    // Assuming you have a button like this in your HTML: <button id="open-logs-btn">Open Logs</button>
    const openLogsBtn = document.getElementById('open-logs-btn');
    if (openLogsBtn) {
        openLogsBtn.addEventListener('click', openLogsDirectory);
    }
}

/**
 * Handles the native directory selection dialog.
 */
async function browseOutputDirectory() {
    try {
        const result = await window.api.invoke('open-directory-dialog', {});
        if (result.success) {
            MODAL_ELEMENTS.outputDirInput.value = result.path;
            validateCreateForm();
        } else {
            window.api.log.warn("Directory dialog cancelled or failed.", 'Renderer-UI');
        }
    } catch (error) {
        window.api.log.error(`FATAL: IPC call 'open-directory-dialog' failed: ${error.message}`, 'Renderer-UI-IPC');
    }
}

/**
 * Validates the New Mod form and enables/disables the button.
 */
function validateCreateForm() {
    const modName = document.getElementById('mod-name-input').value.trim();
    const outputDir = MODAL_ELEMENTS.outputDirInput.value.trim();

    if (modName && outputDir) {
        MODAL_ELEMENTS.confirmCreateBtn.disabled = false;
    } else {
        MODAL_ELEMENTS.confirmCreateBtn.disabled = true;
    }
}

/**
 * Calls the main process to create the new project.
 */
async function createNewMod() {
    const projectName = document.getElementById('mod-name-input').value.trim();
    const outputDir = MODAL_ELEMENTS.outputDirInput.value.trim();

    if (!projectName || !outputDir) return;

    MODAL_ELEMENTS.confirmCreateBtn.disabled = true;
    window.api.log.info(`Attempting to create new mod: ${projectName}`, 'Renderer-ModCreation');

    try {
        const result = await window.api.invoke('create-project', { 
            projectName, 
            outputDir 
        });

        if (result.success) {
            hideCreateModal();
            // Using the async function correctly here
            await startModListListener(); 
            window.api.log.info(`Mod created successfully: ${result.projectName}`, 'Renderer-ModCreation');
        } else {
            // Log the error and re-enable the button
            window.api.log.error(`Failed to create mod: ${result.message}`, 'Renderer-ModCreation');
            MODAL_ELEMENTS.confirmCreateBtn.disabled = false;
        }
    } catch (error) {
        window.api.log.error(`FATAL: IPC call 'create-project' failed: ${error.message}`, 'Renderer-ModCreation-IPC');
        MODAL_ELEMENTS.confirmCreateBtn.disabled = false;
    }
}


// --- Project Deletion ---

function showDeleteModal(projectName) {
    modToDeleteName = projectName;
    window.api.log.warn(`User initiated deletion for mod: ${projectName}`, 'Renderer-ModDeletion');
    document.getElementById('delete-mod-message').innerHTML = `Are you sure you want to **permanently delete** the mod: <strong class="text-yellow-400">${projectName}</strong>? This action **cannot be undone** and will remove all associated files from your disk.`;
    MODAL_ELEMENTS.delete.classList.remove('hidden');
}

function hideDeleteModal() {
    modToDeleteName = null;
    MODAL_ELEMENTS.delete.classList.add('hidden');
}

/**
 * Deletes the globally selected mod.
 */
async function confirmDeleteMod() {
    if (!modToDeleteName) return;

    document.getElementById('confirm-delete-btn').disabled = true;
    window.api.log.warn(`Confirming permanent deletion of mod: ${modToDeleteName}`, 'Renderer-ModDeletion');
    
    try {
        const result = await window.api.invoke('delete-project', { 
            projectName: modToDeleteName 
        });

        if (result.success) {
            window.api.log.info(`Deletion successful: ${modToDeleteName}. Message: ${result.message}`, 'Renderer-ModDeletion');
            await startModListListener(); // Await the refresh
            hideDeleteModal();
        } else {
            // Keep the modal open and prepend the error message to the confirmation text
            document.getElementById('delete-mod-message').innerHTML = `<p class="text-red-500 mb-2">ERROR: ${result.message}</p> ${document.getElementById('delete-mod-message').innerHTML}`;
            window.api.log.error(`Failed to delete mod: ${result.message}`, 'Renderer-ModDeletion');
        }
    } catch (error) { 
        // A catastrophic failure should still be noted clearly in the modal
        document.getElementById('delete-mod-message').innerHTML = `<p class="text-red-500 mb-2">FATAL IPC ERROR: ${error.message}</p> ${document.getElementById('delete-mod-message').innerHTML}`;
        window.api.log.error(`FATAL: IPC call 'delete-project' failed: ${error.message}`, 'Renderer-ModDeletion-IPC');
    } finally { 
        document.getElementById('confirm-delete-btn').disabled = false;
    }
}

// --- Wiki Functions ---

function showWikiModal() {
    window.api.log.info('Wiki choice modal opened by user.', 'Renderer-UI');
    MODAL_ELEMENTS.wiki.classList.remove('hidden');
}

function hideWikiModal() {
    MODAL_ELEMENTS.wiki.classList.add('hidden');
}

/**
 * Opens the Wiki in the user's default external browser
 */
function openWikiInBrowser() {
    window.api.log.info('Opening Wiki in default browser.', 'Renderer-Wiki');
    window.api.invoke('open-wiki-external'); 
    hideWikiModal();
}

function openWikiInApp() {
    window.api.log.info('Opening Wiki in-app.', 'Renderer-Wiki');
    window.api.send('switch-page', 'wiki');
    hideWikiModal();
}

// --- Project Settings Functions ---

/**
 * Opens the settings modal and populates it with existing data.
 */
function showSettingsModal(projectName, currentOutputPath) {
    modToEditName = projectName;
    window.api.log.info(`User opening settings for mod: ${projectName}`, 'Renderer-ModSettings');
    
    MODAL_ELEMENTS.settingsModName.textContent = projectName;
    MODAL_ELEMENTS.settingsOutputDirInput.value = currentOutputPath || ''; // Handle undefined/null
    
    MODAL_ELEMENTS.settings.classList.remove('hidden');
}


function hideSettingsModal() {
    modToEditName = null;
    MODAL_ELEMENTS.settings.classList.add('hidden');
}

/**
 * Handles the native directory selection for the SETTINGS modal.
 */
async function browseSettingsOutputDirectory() {
    try {
        const result = await window.api.invoke('open-directory-dialog', {});
        if (result.success) {
            MODAL_ELEMENTS.settingsOutputDirInput.value = result.path;
        } else {
            window.api.log.warn("Directory dialog cancelled or failed.", 'Renderer-UI');
        }
    } catch (error) {
        window.api.log.error(`FATAL: IPC call 'open-directory-dialog' failed: ${error.message}`, 'Renderer-UI-IPC');
    }
}

/**
 * Calls the main process to save the new output directory.
 */
async function saveModSettings() {
    if (!modToEditName) return;

    const newOutputDir = MODAL_ELEMENTS.settingsOutputDirInput.value.trim();
    if (!newOutputDir) {
        alert("Output directory cannot be empty."); // Simple validation
        return;
    }

    const btn = document.getElementById('confirm-settings-btn');
    btn.disabled = true;
    window.api.log.info(`Saving new output dir for ${modToEditName}: ${newOutputDir}`, 'Renderer-ModSettings');

    const settingsUpdate = {
        workspaceUpdates: {
            [modToEditName]: newOutputDir
        }
    };

    try {
        const result = await window.api.invoke('save-global-settings', settingsUpdate);

        if (result.success) {
            window.api.log.info(`Settings saved for ${modToEditName}.`, 'Renderer-ModSettings');
            hideSettingsModal();
            await startModListListener(); 
        } else {
            window.api.log.error(`Failed to save settings: ${result.message}`, 'Renderer-ModSettings');
            alert(`Error saving settings: ${result.message}`); // Show error to user
        }

    } catch (error) {
        window.api.log.error(`FATAL: IPC call 'save-global-settings' failed: ${error.message}`, 'Renderer-ModSettings-IPC');
        alert(`FATAL ERROR: ${error.message}`);
    } finally {
        btn.disabled = false;
    }
}
async function exportWorkspace() {
    if (!modToEditName) return;
    window.api.log.info(`Exporting workspace: ${modToEditName}`, 'Renderer-UI');
    
    try {
        const result = await window.api.invoke('export-project', modToEditName);
        if (result.success) {
            window.api.log.info(`Export saved to: ${result.path}`, 'Renderer-UI');
        } else {
            window.api.log.error(`Export failed: ${result.message}`, 'Renderer-UI');
        }
    } catch (error) {
        window.api.log.error(`IPC Export Error: ${error.message}`, 'Renderer-UI');
    }
}

// --- Modal Visibility ---

function showCreateModal() {
    window.api.log.info('Create Mod modal opened by user.', 'Renderer-UI');
    // Reset form fields
    document.getElementById('mod-name-input').value = '';
    MODAL_ELEMENTS.outputDirInput.value = '';
    MODAL_ELEMENTS.confirmCreateBtn.disabled = true;
    MODAL_ELEMENTS.create.classList.remove('hidden');
}

function hideCreateModal() {
    MODAL_ELEMENTS.create.classList.add('hidden');
}


// --- Initialization ---

window.addEventListener('load', () => {
    window.api.log.info('index.html loaded.', 'Renderer-Startup');
    startModListListener();

    // Utility Buttons
    document.getElementById('open-logs-btn').addEventListener('click', openLogsDirectory);
    document.getElementById('open-settings-btn').addEventListener('click', () => window.api.send('switch-page', 'settings'));
    document.getElementById('open-wiki-btn').addEventListener('click', showWikiModal);
    document.getElementById('cancel-wiki-btn').addEventListener('click', hideWikiModal);
    MODAL_ELEMENTS.browserWiki.addEventListener('click', openWikiInBrowser);
    MODAL_ELEMENTS.inAppWiki.addEventListener('click', openWikiInApp);

    // Mod Creation
    document.getElementById('create-mod-btn').addEventListener('click', showCreateModal);
    document.getElementById('cancel-create-btn').addEventListener('click', hideCreateModal);
    document.getElementById('browse-output-dir-btn').addEventListener('click', browseOutputDirectory);
    document.getElementById('confirm-create-btn').addEventListener('click', createNewMod);
    document.getElementById('mod-name-input').addEventListener('input', validateCreateForm);

    // Mod Deletion (Final Confirmation)
    document.getElementById('cancel-delete-btn').addEventListener('click', hideDeleteModal);
    document.getElementById('confirm-delete-btn').addEventListener('click', confirmDeleteMod);
    
    // Settings Modal Buttons
    document.getElementById('cancel-settings-btn').addEventListener('click', hideSettingsModal);
    document.getElementById('confirm-settings-btn').addEventListener('click', saveModSettings);
    document.getElementById('browse-settings-output-dir-btn').addEventListener('click', browseSettingsOutputDirectory);
    
    // Actions inside Settings
    document.getElementById('export-workspace-btn').addEventListener('click', exportWorkspace);
    document.getElementById('settings-delete-mod-btn').addEventListener('click', () => {
        const name = modToEditName;
        hideSettingsModal();
        showDeleteModal(name);
    });
    // Opens the "How to import" choice modal
    document.getElementById('import-mod-btn').addEventListener('click', () => {
        document.getElementById('import-choice-modal').classList.remove('hidden');
    });

    // Closes the import choice modal
    document.getElementById('cancel-import-btn').addEventListener('click', () => {
        document.getElementById('import-choice-modal').classList.add('hidden');
    });

    // The actual "MarshalIDE" import logic
    document.getElementById('import-marshal-btn').addEventListener('click', async () => {
        document.getElementById('import-choice-modal').classList.add('hidden');
        
        // This triggers the main process to:
        // 1. Pick the .MarshalIDE zip
        // 2. Read metadata to get the project name
        // 3. Ask user for Output Directory
        // 4. Extract to workspaces folder
        const result = await window.api.invoke('import-project');
        
        if (result.success) {
            // Refresh the UI list so the new project shows up immediately
            const projects = await window.api.invoke('list-projects');
            renderModList(projects.projects);
            window.api.log.info(`Imported project: ${result.projectName}`, 'Renderer');
        } else if (result.message) {
            alert("Import failed: " + result.message);
        }
    });
});
