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
            <button @click="showWikiModal = true" class="btn btn-secondary">
              <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/>
                <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/>
              </svg>
              <span>Wiki</span>
            </button>

            <button @click="navigateToSettings" class="btn btn-secondary">
              <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"/>
                <circle cx="12" cy="12" r="3"/>
              </svg>
              <span>Settings</span>
            </button>
            
            <button @click="openLogsDirectory" class="btn btn-secondary">
              <svg class="icon" style="width: 20px; height: 20px;" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
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
          <p v-else-if="errorMessage" class="text-red-400 col-span-full">{{ errorMessage }}</p>
          <p v-else-if="Object.keys(projects).length === 0" class="text-gray-400 col-span-full">
            No mods found. Click "New Mod" to get started.
          </p>

          <div v-else v-for="(config, name) in projects" :key="name" class="card">
            <div>
              <h3 class="text-xl font-bold text-white mb-2">{{ name }}</h3>
              <p class="text-sm text-gray-400 mb-4">Click to open in the IDE.</p>
            </div>
            <div class="flex justify-between items-center pt-2 border-t border-gray-600" style="display: flex; justify-content: space-between; align-items: center; border-top: 1px solid #4b5563; padding-top: 0.5rem;">
              <button class="text-sm text-primary-blue hover:underline open-mod-btn" style="background: none; border: none; color: #007acc; cursor: pointer; font-size: 0.875rem;" @click="openMod(name)">
                Open IDE
              </button>
              <button style="background: none; border: none; color: #9ca3af; cursor: pointer;" @click="openSettingsModal(name, config.output_dir)">
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
          
          <button class="option-row disabled" disabled>
            <div class="option-icon-frame">
              <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z"/>
              </svg>
            </div>
            <div class="flex-grow-layout">
              <div class="flex-space-between">
                <span class="option-heading">HOI4 Mod Folder</span>
                <span class="badge badge-incoming">Soon</span>
              </div>
              <div class="option-subtext">Import existing Paradox mod structures</div>
            </div>
          </button>
        </div>
        
        <div class="modal-actions mt-6" style="margin-top: 1.5rem;">
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
            <p class="form-help-text" style="font-size: 0.75rem; color: #6b7280; margin-top: 0.25rem;">The mod's source code will be created in: <strong>workspaces/ModName/mod/</strong></p>
          </div>
          
          <div class="form-group">
            <label class="form-label">Compiler Output Directory (e.g., HOI4 Mod Folder)</label>
            <div class="input-inline-group">
              <input type="text" :value="createForm.outputDir" placeholder="Select a location..." class="form-input flex-grow" readonly>
              <button @click="browseOutputDirectory" class="btn btn-neutral">Browse</button>
            </div>
            <p class="form-help-text" style="font-size: 0.75rem; color: #6b7280; margin-top: 0.25rem;">This is the absolute path where your compiled files will be saved for the game.</p>
          </div>

          <div style="display: flex; align-items: center; gap: 0.5rem; margin-top: 1rem;">
            <input type="checkbox" v-model="createForm.includeTemplates" id="tmpl-chk">
            <label for="tmpl-chk" class="form-label" style="margin-bottom: 0;">Include Templates</label>
          </div>
        </div>
        
        <div class="modal-actions" style="margin-top: 1.5rem;">
          <button @click="showCreateModal = false" class="btn btn-neutral">Cancel</button>
          <button @click="createNewMod" class="btn btn-primary" :disabled="!isCreateFormValid || submittingCreate">Create Mod</button>
        </div>
      </div>
    </div>

    <div v-if="showSettingsModal" class="modal-overlay">
      <div class="modal-card max-w-md">
        <h3 class="modal-title">
          Settings for <span style="color: #007acc;">{{ modToEditName }}</span>
        </h3>
        
        <div class="form-layout">
          <div class="form-group">
            <label class="form-label">Compiler Output</label>
            <div class="input-inline-group">
              <input type="text" :value="settingsFormOutputDir" class="form-input flex-grow" readonly>
              <button @click="browseSettingsOutputDirectory" class="btn btn-neutral">Browse</button>
            </div>
          </div>

          <div style="margin-top: 1rem; border-top: 1px solid #4b5563; padding-top: 1rem;">
            <label class="form-label" style="text-transform: uppercase; font-size: 0.75rem;">Workspace Management</label>
            <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 0.75rem; margin-top: 0.5rem;">
              <button @click="exportWorkspace" class="btn btn-neutral" style="justify-content: center;">
                <svg class="icon" style="width: 1.25rem; height: 1.25rem;" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"></path></svg>
                <span>Export</span>
              </button>
              <button @click="triggerDeletionFromSettings" class="btn btn-neutral" style="justify-content: center; background-color: #374151;" onmouseover="this.style.backgroundColor='#dc2626'" onmouseout="this.style.backgroundColor='#374151'">
                <svg class="icon" style="width: 1.25rem; height: 1.25rem;" fill="currentColor" viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"></path></svg>
                <span>Delete</span>
              </button>
            </div>
          </div>
        </div>

        <div class="modal-actions" style="margin-top: 2rem;">
          <button @click="showSettingsModal = false" class="btn btn-neutral">Cancel</button>
          <button @click="saveModSettings" class="btn btn-primary" :disabled="submittingSettings">Save Changes</button>
        </div>
      </div>
    </div>

    <div v-if="showDeleteModal" class="modal-overlay">
      <div class="modal-card max-w-sm">
        <h3 class="modal-title" style="color: #ef4444;">Confirm Deletion</h3>
        <p class="modal-description" style="color: #d1d5db;">
          <span v-if="deleteError" style="color: #ef4444; margin-bottom: 0.5rem; display: block;">ERROR: {{ deleteError }}</span>
          Are you sure you want to permanently delete the mod: <strong style="color: #f59e0b;">{{ modToDeleteName }}</strong>? This action <strong>cannot be undone</strong> and will remove all associated files from your disk.
        </p>
        <div class="modal-actions">
          <button @click="showDeleteModal = false" class="btn btn-neutral">Cancel</button>
          <button @click="confirmDeleteMod" class="btn btn-danger" :disabled="submittingDelete">DELETE MOD</button>
        </div>
      </div>
    </div>

    <div v-if="showWikiModal" class="modal-overlay">
      <div class="modal-card max-w-sm">
        <h2 class="modal-title font-medium">Open Wiki</h2>
        <p class="modal-description text-center">How would you like to view the documentation?</p>
        
        <div class="flex-column-stack">
          <button @click="openWikiInApp" class="btn btn-primary w-full text-center" style="justify-content: center;">Open In-App</button>
          <button @click="openWikiInBrowser" class="btn btn-secondary w-full text-center" style="justify-content: center;">Open in Browser</button>
          <button @click="showWikiModal = false" class="btn-text-link">Cancel</button>
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
const showWikiModal = ref(false);

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

const navigateToSettings = () => window.api.send('switch-page', 'settings');
const openMod = (projectName) => {
  localStorage.setItem('marshal_project_to_load', projectName);
  window.api.send('switch-page', 'ide');
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

const openWikiInBrowser = () => {
  window.api.invoke('open-wiki-external'); 
  showWikiModal.value = false;
};

const openWikiInApp = () => {
  window.api.send('switch-page', 'wiki');
  showWikiModal.value = false;
};
</script>