<template>
  <div class="workspace-wrapper">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE</span>
    </div>

    <div class="main-container">
      <header class="header-section">
        <h1 class="title">Marshal IDE</h1>
        <p class="subtitle">Select a Mod or Create a New Project</p>
      </header>

      <main class="content-body">
        <div class="action-bar">
          <h2 class="section-title">Available Mods</h2>
          
          <div class="button-group">
            <button @click="openWikiInApp" class="btn btn-secondary">
              <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/>
                <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/>
              </svg>
              <span>Wiki</span>
            </button>

            <button @click="openSettings" class="btn btn-secondary">
              <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"/>
                <circle cx="12" cy="12" r="3"/>
              </svg>
              <span>Settings</span>
            </button>
            
            <button @click="openLogsDirectory" class="btn btn-secondary">
              <svg class="icon icon-svg" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"></path>
              </svg>
              <span>Logs</span>
            </button>
            
            <button @click="showImportModal = true" class="btn btn-neutral">
              <svg class="icon" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path>
              </svg>
              <span>Import Mod</span>
            </button>
            
            <button @click="openCreateModal" class="btn btn-primary">
              <svg class="icon" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6"></path>
              </svg>
              <span>New Mod</span>
            </button>
          </div>
        </div>

        <div id="mod-list" class="workspace-grid">
          <p v-if="loadingProjects" class="status-message">Loading workspaces...</p>
          <p v-else-if="errorMessage" class="error-text col-span-full">{{ errorMessage }}</p>
          <p v-else-if="Object.keys(projects).length === 0" class="empty-text col-span-full">
            No mods found. Click "New Mod" to get started.
          </p>

          <div v-else v-for="(config, name) in projects" :key="name" class="card">
            <div class="card-body">
              <h3 class="card-title">{{ name }}</h3>
              <p class="card-subtitle">Click to open in the IDE.</p>
            </div>
            <div class="card-footer">
              <button class="open-mod-btn" @click="openMod(name)">
                Open IDE
              </button>
              <button class="settings-trigger-btn" @click="openSettingsModal(name, config.output_dir)">
                <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"/><circle cx="12" cy="12" r="3"/></svg>
              </button>
            </div>
          </div>
        </div>
      </main>
    </div>

    <div v-if="showImportModal" class="modal-overlay">
      <div class="modal-card max-w-md">
        <h3 class="modal-title">Import Project</h3>
        <p class="modal-description">Select the type of project you would like to import into your workspace.</p>
        
        <div class="modal-options-list">
          <button @click="handleMarshalImport" class="option-row">
            <div class="option-icon-frame">
              <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 8V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v3"/><path d="M21 16v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-3"/><rect width="20" height="8" x="2" y="8" rx="2"/>
              </svg>
            </div>
            <div>
              <div class="option-heading">Marshal IDE Workspace</div>
              <div class="option-subtext">Import a .MarshalIDE archive file</div>
            </div>
          </button>
          
          <button @click="openImporter" class="option-row">
            <div class="option-icon-frame">
              <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z"/>
              </svg>
            </div>
            <div class="flex-grow">
              <div class="flex-space-between">
                <span class="option-heading">HOI4 Mod Folder</span>
              </div>
              <div class="option-subtext">Import existing Paradox mod structures</div>
            </div>
          </button>
        </div>
        
        <div class="modal-actions spacing-top-md">
          <button @click="showImportModal = false" class="btn btn-neutral">Cancel</button>
        </div>
      </div>
    </div>

    <div v-if="showCreateModal" class="modal-overlay">
      <div class="modal-card max-w-md">
        <h3 class="modal-title">Create New Mod Project</h3>
        
        <div class="form-layout">
          <div class="form-group">
            <label class="form-label">Mod Name</label>
            <input type="text" v-model="createForm.name" placeholder="My Awesome Mod" class="form-input">
            <p class="form-help-text">The mod's source code will be created in: <strong>workspaces/ModName/mod/</strong></p>
          </div>
          
          <div class="form-group">
            <label class="form-label">Compiler Output Directory (e.g., HOI4 Mod Folder)</label>
            <div class="input-inline-group">
              <input type="text" :value="createForm.outputDir" placeholder="Select a location..." class="form-input flex-grow" readonly>
              <button @click="browseOutputDirectory" class="btn btn-neutral">Browse</button>
            </div>
            <p class="form-help-text">This is the absolute path where your compiled files will be saved for the game.</p>
          </div>

          <div class="form-checkbox-group">
            <input type="checkbox" v-model="createForm.includeTemplates" id="tmpl-chk">
            <label for="tmpl-chk" class="form-label label-inline">Include Templates</label>
          </div>
        </div>
        
        <div class="modal-actions spacing-top-md">
          <button @click="showCreateModal = false" class="btn btn-neutral">Cancel</button>
          <button @click="createNewMod" class="btn btn-primary" :disabled="!isCreateFormValid || submittingCreate">Create Mod</button>
        </div>
      </div>
    </div>

    <div v-if="showSettingsModal" class="modal-overlay">
      <div class="modal-card max-w-md">
        <h3 class="modal-title">
          Settings for <span class="highlight-title">{{ modToEditName }}</span>
        </h3>
        
        <div class="form-layout">
          <div class="form-group">
            <label class="form-label">Compiler Output</label>
            <div class="input-inline-group">
              <input type="text" :value="settingsFormOutputDir" class="form-input flex-grow" readonly>
              <button @click="browseSettingsOutputDirectory" class="btn btn-neutral">Browse</button>
            </div>
          </div>

          <div class="form-divider-section">
            <label class="form-label uppercase-label">Workspace Management</label>
            <div class="management-grid">
              <button @click="exportWorkspace" class="btn btn-neutral btn-center">
                <svg class="icon icon-svg-sm" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"></path></svg>
                <span>Export</span>
              </button>
              <button @click="triggerDeletionFromSettings" class="btn btn-danger-destructive btn-center">
                <svg class="icon icon-svg-sm" fill="currentColor" viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"></path></svg>
                <span>Delete</span>
              </button>
            </div>
          </div>
        </div>

        <div class="modal-actions spacing-top-lg">
          <button @click="showSettingsModal = false" class="btn btn-neutral">Cancel</button>
          <button @click="saveModSettings" class="btn btn-primary" :disabled="submittingSettings">Save Changes</button>
        </div>
      </div>
    </div>

    <div v-if="showDeleteModal" class="modal-overlay">
      <div class="modal-card max-w-sm">
        <h3 class="modal-title error-title">Confirm Deletion</h3>
        <p class="modal-description descriptive-text">
          <span v-if="deleteError" class="error-alert-banner">ERROR: {{ deleteError }}</span>
          Are you sure you want to permanently delete the mod: <strong class="warning-highlight">{{ modToDeleteName }}</strong>? This action <strong>cannot be undone</strong> and will remove all associated files from your disk.
        </p>
        <div class="modal-actions">
          <button @click="showDeleteModal = false" class="btn btn-neutral">Cancel</button>
          <button @click="confirmDeleteMod" class="btn btn-danger" :disabled="submittingDelete">DELETE MOD</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue';

const projects = ref({});
const loadingProjects = ref(true);
const errorMessage = ref('');

const showImportModal = ref(false);
const showCreateModal = ref(false);
const showSettingsModal = ref(false);
const showDeleteModal = ref(false);

const modToEditName = ref('');
const modToDeleteName = ref('');
const deleteError = ref('');

const submittingCreate = ref(false);
const submittingSettings = ref(false);
const submittingDelete = ref(false);

const createForm = reactive({
  name: '',
  outputDir: '',
  includeTemplates: true
});
const settingsFormOutputDir = ref('');

const isCreateFormValid = computed(() => {
  return createForm.name.trim() && createForm.outputDir.trim();
});

const loadProjectsGrid = async () => {
  loadingProjects.value = true;
  errorMessage.value = '';
  try {
    const result = await window.api.invoke('list-projects', {});
    if (result.success) {
      projects.value = result.projects;
    } else {
      errorMessage.value = `Error loading projects: ${result.message}`;
    }
  } catch (error) {
    errorMessage.value = `FATAL: IPC call failed: ${error.message}`;
  } finally {
    loadingProjects.value = false;
  }
};

onMounted(() => {
  loadProjectsGrid();
});

const openMod = async (projectName) => {
  errorMessage.value = '';
  try {
    // Invoke your existing main.js IPC handler directly
    const result = await window.api.invoke('load-project', projectName);
    
    if (result.success) {
      // Save name for presentation inside the IDE header
      localStorage.setItem('marshal_project_to_load', projectName);
      // Safely switch pages now that INPUT_DIR is established
      window.api.send('switch-page', 'ide');
    } else {
      errorMessage.value = `Failed to load project: ${result.message}`;
    }
  } catch (error) {
    errorMessage.value = `IPC Navigation Error: ${error.message}`;
  }
};

const openLogsDirectory = async () => {
  try {
    const dirResult = await window.api.getLogDirectory();
    if (dirResult.success) {
      await window.api.openPath(dirResult.path);
    }
  } catch (err) {
    console.error(err);
  }
};

const handleMarshalImport = async () => {
  showImportModal.value = false;
  try {
    const result = await window.api.invoke('import-project');
    if (result.success) {
      alert(`Successfully imported: ${result.projectName}`);
      await loadProjectsGrid();
    }
  } catch (err) {
    console.error(err);
  }
};

const openImporter = () => {
  window.api.send('switch-page', 'importing');
  showImportModal.value = false;
};

const openCreateModal = () => {
  createForm.name = '';
  createForm.outputDir = '';
  createForm.includeTemplates = true;
  showCreateModal.value = true;
};

const browseOutputDirectory = async () => {
  try {
    const result = await window.api.invoke('open-directory-dialog', {});
    if (result.success) createForm.outputDir = result.path;
  } catch (error) {
    console.error(error);
  }
};

const createNewMod = async () => {
  if (!isCreateFormValid.value) return;
  submittingCreate.value = true;
  try {
    const result = await window.api.invoke('create-project', { 
      projectName: createForm.name.trim(), 
      outputDir: createForm.outputDir.trim(),
      includeTemplates: createForm.includeTemplates 
    });
    if (result.success) {
      showCreateModal.value = false;
      await loadProjectsGrid(); 
    }
  } catch (error) {
    console.error(error);
  } finally {
    submittingCreate.value = false;
  }
};

const openSettingsModal = (projectName, currentOutputPath) => {
  modToEditName.value = projectName;
  settingsFormOutputDir.value = currentOutputPath || '';
  showSettingsModal.value = true;
};

const browseSettingsOutputDirectory = async () => {
  try {
    const result = await window.api.invoke('open-directory-dialog', {});
    if (result.success) settingsFormOutputDir.value = result.path;
  } catch (error) {
    console.error(error);
  }
};

const saveModSettings = async () => {
  if (!settingsFormOutputDir.value.trim()) return;
  submittingSettings.value = true;
  try {
    const result = await window.api.invoke('save-global-settings', {
      workspaceUpdates: { [modToEditName.value]: settingsFormOutputDir.value.trim() }
    });
    if (result.success) {
      showSettingsModal.value = false;
      await loadProjectsGrid(); 
    }
  } catch (error) {
    console.error(error);
  } finally {
    submittingSettings.value = false;
  }
};

const exportWorkspace = async () => {
  if (!modToEditName.value) return;
  try {
    await window.api.invoke('export-project', modToEditName.value);
  } catch (error) {
    console.error(error);
  }
};

const triggerDeletionFromSettings = () => {
  const currentTarget = modToEditName.value;
  showSettingsModal.value = false;
  deleteError.value = '';
  modToDeleteName.value = currentTarget;
  showDeleteModal.value = true;
};

const confirmDeleteMod = async () => {
  if (!modToDeleteName.value) return;
  submittingDelete.value = true;
  try {
    const result = await window.api.invoke('delete-project', { projectName: modToDeleteName.value });
    if (result.success) {
      await loadProjectsGrid();
      showDeleteModal.value = false;
    } else {
      deleteError.value = result.message;
    }
  } catch (error) { 
    deleteError.value = error.message;
  } finally {
    submittingDelete.value = false;
  }
};

const openWikiInApp = () => {
  window.api.send('switch-page', 'wiki');
};

const openSettings = () => {window.api.send('switch-page', 'settings');};
</script>

<style scoped>
/* ==========================================================================
   WORKSPACE MAIN STRUCTURAL LAYOUT
   ========================================================================== */

.main-container {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  width: 100%;
  flex-grow: 1;
  box-sizing: border-box;
  max-width: 56rem; /* max-w-4xl */
  margin: 0 auto;
  padding: 0 1rem;
}

.header-section {
  text-align: center;
  margin-bottom: 1.5rem; 

  .title {
    font-size: 2.25rem; /* text-4xl */
    font-weight: 800;
    color: #ffffff;
    margin: 0;
    margin-top: 15px;
  }

  .subtitle {
    font-size: 1rem;
    color: #9ca3af;
    margin-top: 0.5rem;
  }
}

.action-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;

  .section-title {
    font-size: 1.25rem;
    font-weight: 600;
    color: #ffffff;
  }
}

/* ==========================================================================
   MODS PROJECT TILES & GRID FRAMEWORK
   ========================================================================== */

.workspace-grid {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: 1.5rem;
  width: 100%;

  @media (min-width: 768px) {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  @media (min-width: 1024px) {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .status-message {
    color: var(--text-muted);
  }
  .error-text {
    color: #f87171; /* text-red-400 */
  }
  .empty-text {
    color: #9ca3af; /* text-gray-400 */
  }
  .col-span-full {
    grid-column: 1 / -1;
  }
}

.card {
  background-color: var(--card-bg);
  border: 1px solid #37373d;
  padding: 1rem;
  border-radius: 0.5rem;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  transition: transform 0.1s ease, box-shadow 0.1s ease;

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
  }

  .card-title {
    font-size: 1.25rem; /* text-xl */
    font-weight: 700;
    color: #ffffff;
    margin: 0 0 0.5rem 0;
  }

  .card-subtitle {
    font-size: 0.875rem; /* text-sm */
    color: #9ca3af; /* text-gray-400 */
    margin: 0 0 1rem 0;
  }

  .card-footer {
    display: flex; 
    justify-content: space-between; 
    align-items: center; 
    border-top: 1px solid #4b5563; 
    padding-top: 0.5rem;

    .open-mod-btn {
      background: none; 
      border: none; 
      color: var(--primary-blue); 
      cursor: pointer; 
      font-size: 0.875rem;
      padding: 0;

      &:hover {
        text-decoration: underline;
      }
    }

    .settings-trigger-btn {
      background: none; 
      border: none; 
      color: #9ca3af; 
      cursor: pointer;
      padding: 0;
      display: inline-flex;

      &:hover {
        color: #ffffff;
      }
    }
  }
}

/* ==========================================================================
   BUTTON ARCHITECTURE & DESIGN HOOKS
   ========================================================================== */

.button-group {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-bottom: 0;
}

.btn {
  padding: 0.5rem 1rem;
  border-radius: 0.25rem; 
  font-size: 0.875rem;     
  font-weight: 600;
  gap: 0.5rem;
  transition: background-color 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  border: none;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;

  .icon {
    width: 20px;
    height: 20px;
  }

  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  &.btn-primary {
    background-color: var(--primary-blue);
    color: #ffffff;
    
    &:hover:not(:disabled) {
      background-color: #006bbd;
    }
  }

  &.btn-secondary {
    background-color: #374151; 
    color: #ffffff;
    
    &:hover:not(:disabled) {
      background-color: #1f2937; 
    }
  }

  &.btn-neutral {
    background-color: #4b5563; 
    color: #ffffff;
    
    &:hover:not(:disabled) {
      background-color: #374151; 
    }
  }

  &.btn-danger {
    background-color: #dc2626;
    color: #ffffff;
    
    &:hover:not(:disabled) {
      background-color: #b91c1c;
    }
  }
  
  &.btn-danger-destructive {
    background-color: #374151;
    color: #ffffff;

    &:hover {
      background-color: #dc2626;
    }
  }

  &.btn-center {
    justify-content: center;
  }
}

.icon-svg-sm {
  width: 1.25rem;
  height: 1.25rem;
}

/* ==========================================================================
   FORM & INTERACTIVE CONTROL SPECIFICATIONS
   ========================================================================== */

.form-layout {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  
  .form-label {
    font-size: 0.875rem;
    font-weight: 500;
    color: #9ca3af;
    margin-bottom: 0.25rem;
  }
  
  .form-input {
    width: 100%;
    padding: 0.5rem;
    background-color: #374151;
    border: 1px solid #4b5563;
    color: #ffffff;
    font-size: 0.875rem;
    outline: none;
    border-radius: 4px;
    box-sizing: border-box;

    &:focus {
      border-color: var(--primary-blue);
    }
  }

  .form-help-text {
    font-size: 0.75rem; 
    color: #6b7280; 
    margin-top: 0.25rem;
  }
}

.form-checkbox-group {
  display: flex; 
  align-items: center; 
  gap: 0.5rem; 
  margin-top: 1rem;

  .label-inline {
    margin-bottom: 0;
  }
}

.form-divider-section {
  margin-top: 1rem; 
  border-top: 1px solid #4b5563; 
  padding-top: 1rem;
}

.input-inline-group {
  display: flex;
  gap: 0.5rem;
}

.management-grid {
  display: grid; 
  grid-template-columns: repeat(2, 1fr); 
  gap: 0.75rem; 
  margin-top: 0.5rem;
}

.uppercase-label {
  text-transform: uppercase; 
  font-size: 0.75rem;
}

/* ==========================================================================
   MODAL DIALOG FRAMEWORKS & OVERLAYS (STRETCH FIX APPLIED)
   ========================================================================== */

.modal-overlay {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.8); 
  z-index: 50;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem; /* Safety spacing for small window frames */

  .modal-card {
    background-color: var(--card-bg);
    padding: 1.5rem; 
    border-radius: 8px;
    border: 1px solid #444;
    box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5); 
    width: 100%;
    max-width: 20rem; /* Fallback safe boundary width */
    box-sizing: border-box;

    /* Fixed explicit class overrides to counteract screen stretching */
    &.max-w-md { 
      max-width: 28rem; 
    }
    
    &.max-w-sm { 
      max-width: 24rem; 
    }
  }

  .modal-title {
    color: #ffffff;
    font-weight: 700;
    margin-top: 0;
    margin-bottom: 0.5rem;
    font-size: 1.25rem;

    &.font-medium { font-size: 1.25rem; }
    &.error-title { color: #ef4444; }
  }

  .modal-description {
    font-size: 0.875rem;
    color: #9ca3af; 
    margin-bottom: 1rem;
    margin-top: 0;

    &.descriptive-text {
      color: #d1d5db;
    }
  }

  .modal-options-list {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .option-row {
    width: 100%;
    display: flex;
    align-items: center;
    padding: 1rem;
    background-color: #374151;
    border: 1px solid #4b5563;
    border-radius: 4px;
    text-align: left;
    cursor: pointer;
    transition: all 0.15s ease;
    
    &:hover:not(.disabled) {
      background-color: #4b5563;
      border-color: var(--primary-blue);
    }
    
    &.disabled {
      background-color: #1f2937;
      opacity: 0.5;
      cursor: not-allowed;
      border-color: transparent;
    }
  }

  .option-icon-frame {
    padding: 0.5rem;
    background-color: #1f2937;
    border-radius: 4px;
    margin-right: 1rem;
    color: #ffffff;
    display: flex;
    flex-shrink: 0; /* Keeps structural icon size from squishing */
  }

  .option-heading {
    font-weight: 700;
    color: #ffffff;
  }

  .option-subtext {
    font-size: 0.75rem;
    color: #9ca3af;
  }

  .modal-actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.5rem; 
  }
}

/* --- Internal Micro-Utility Mappings --- */
.spacing-top-md      { margin-top: 1.5rem; }
.spacing-top-lg      { margin-top: 2rem; }
.highlight-title     { color: var(--primary-blue); }
.warning-highlight   { color: #f59e0b; }
.text-center         { text-align: center; }

.error-alert-banner {
  color: #ef4444; 
  margin-bottom: 0.5rem; 
  display: block;
}
</style>