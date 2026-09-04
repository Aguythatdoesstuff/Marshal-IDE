<template>
  <div class="workspace-wrapper min-h-screen bg-marshal-sidebar font-sans text-marshal-text">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE</span>
    </div>

    <div class="mx-auto flex w-full max-w-5xl flex-1 flex-col items-stretch px-6 sm:px-10 lg:px-16">
      <header class="mb-6 text-center">
        <h1 class="mt-[15px] text-4xl font-extrabold text-white">Marshal IDE</h1>
        <p class="mt-2 text-base text-gray-400">Select a Mod or Create a New Project</p>
      </header>

      <main class="flex-1">
        <div class="mb-6 flex flex-wrap items-center justify-between gap-4">
          <h2 class="text-xl font-semibold text-white">Available Mods</h2>
          
          <div class="flex flex-wrap justify-end gap-3">
            <button @click="openWikiInApp" class="inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded bg-gray-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-800 disabled:cursor-not-allowed disabled:opacity-50">
              <svg class="h-5 w-5 shrink-0" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/>
                <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/>
              </svg>
              <span>Wiki</span>
            </button>

            <button @click="openSettings" class="inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded bg-gray-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-800 disabled:cursor-not-allowed disabled:opacity-50">
              <svg class="h-5 w-5 shrink-0" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"/>
                <circle cx="12" cy="12" r="3"/>
              </svg>
              <span>Settings</span>
            </button>
            
            <button @click="openLogsDirectory" class="inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded bg-gray-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-800 disabled:cursor-not-allowed disabled:opacity-50">
              <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"></path>
              </svg>
              <span>Logs</span>
            </button>
            
            <button @click="showImportModal = true" class="inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded bg-gray-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-700 disabled:cursor-not-allowed disabled:opacity-50">
              <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path>
              </svg>
              <span>Import Mod</span>
            </button>
            
            <button @click="openCreateModal" class="inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded bg-marshal-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50">
              <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6"></path>
              </svg>
              <span>New Mod</span>
            </button>
          </div>
        </div>

        <div id="mod-list" class="grid w-full grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
          <p v-if="loadingProjects" class="text-marshal-muted">Loading workspaces...</p>
          <p v-else-if="errorMessage" class="col-span-full text-red-400">{{ errorMessage }}</p>
          <p v-else-if="Object.keys(projects).length === 0" class="col-span-full text-gray-400">
            No mods found. Click "New Mod" to get started.
          </p>

          <div v-else v-for="(config, name) in projects" :key="name" class="flex flex-col justify-between rounded-lg border border-gray-700 bg-marshal-card p-4 transition hover:-translate-y-0.5 hover:shadow-lg">
            <div>
              <h3 class="mb-2 text-xl font-bold text-white">{{ name }}</h3>
              <p class="mb-4 text-sm text-gray-400">Click to open in the IDE.</p>
            </div>
            <div class="flex items-center justify-between border-t border-gray-600 pt-2">
              <button class="p-0 text-sm text-marshal-primary hover:underline" @click="openMod(name)">
                Open IDE
              </button>
              <button class="inline-flex p-0 text-gray-400 hover:text-white" @click="openSettingsModal(name, config.output_dir)">
                <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"/><circle cx="12" cy="12" r="3"/></svg>
              </button>
            </div>
          </div>
        </div>
      </main>
    </div>

    <div v-if="showImportModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4">
      <div class="w-full max-w-sm rounded-lg border border-gray-600 bg-marshal-card p-6 shadow-2xl">
        <h3 class="mb-2 text-xl font-bold text-white">Import Project</h3>
        <p class="mb-4 text-sm text-gray-400">Select the type of project you would like to import into your workspace.</p>
        
        <div class="flex flex-col gap-3">
          <button @click="handleMarshalImport" class="flex w-full items-center rounded border border-gray-600 bg-gray-700 p-4 text-left transition hover:border-marshal-primary hover:bg-gray-600">
            <div class="mr-4 flex shrink-0 rounded bg-gray-800 p-2 text-white">
              <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 8V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v3"/><path d="M21 16v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-3"/><rect width="20" height="8" x="2" y="8" rx="2"/>
              </svg>
            </div>
            <div>
              <div class="font-bold text-white">Marshal IDE Workspace</div>
              <div class="text-xs text-gray-400">Import a .MarshalIDE archive file</div>
            </div>
          </button>
          
          <button @click="openImporter" class="flex w-full items-center rounded border border-gray-600 bg-gray-700 p-4 text-left transition hover:border-marshal-primary hover:bg-gray-600">
            <div class="mr-4 flex shrink-0 rounded bg-gray-800 p-2 text-white">
              <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z"/>
              </svg>
            </div>
            <div class="grow">
              <div class="flex items-center justify-between">
                <span class="font-bold text-white">HOI4 Mod Folder</span>
              </div>
              <div class="text-xs text-gray-400">Import existing Paradox mod structures</div>
            </div>
          </button>
        </div>
        
        <div class="mt-6 flex justify-end gap-2">
          <button @click="showImportModal = false" class="inline-flex items-center justify-center gap-2 rounded bg-gray-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-700">Cancel</button>
        </div>
      </div>
    </div>

    <div v-if="showCreateModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4">
      <div class="w-full max-w-sm rounded-lg border border-gray-600 bg-marshal-card p-6 shadow-2xl">
        <h3 class="mb-2 text-xl font-bold text-white">Create New Mod Project</h3>
        
        <div class="flex flex-col gap-4">
          <div class="flex flex-col">
            <label class="mb-1 text-sm font-medium text-gray-400">Mod Name</label>
            <input type="text" v-model="createForm.name" placeholder="My Awesome Mod" class="w-full rounded border border-gray-600 bg-gray-700 p-2 text-sm text-white outline-none focus:border-marshal-primary">
            <p class="mt-1 text-xs text-gray-500">The mod's source code will be created in: <strong>workspaces/ModName/mod/</strong></p>
          </div>
          
          <div class="flex flex-col">
            <label class="mb-1 text-sm font-medium text-gray-400">Compiler Output Directory (e.g., HOI4 Mod Folder)</label>
            <div class="flex gap-2">
              <input type="text" :value="createForm.outputDir" placeholder="Select a location..." class="grow rounded border border-gray-600 bg-gray-700 p-2 text-sm text-white outline-none focus:border-marshal-primary" readonly>
              <button @click="browseOutputDirectory" class="inline-flex items-center justify-center gap-2 rounded bg-gray-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-700">Browse</button>
            </div>
            <p class="mt-1 text-xs text-gray-500">This is the absolute path where your compiled files will be saved for the game.</p>
          </div>

          <div class="mt-4 flex items-center gap-2">
            <input type="checkbox" v-model="createForm.includeTemplates" id="tmpl-chk">
            <label for="tmpl-chk" class="text-sm font-medium text-gray-400">Include Templates</label>
          </div>
        </div>
        
        <div class="mt-6 flex justify-end gap-2">
          <button @click="showCreateModal = false" class="inline-flex items-center justify-center gap-2 rounded bg-gray-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-700">Cancel</button>
          <button @click="createNewMod" class="inline-flex items-center justify-center gap-2 rounded bg-marshal-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50" :disabled="!isCreateFormValid || submittingCreate">Create Mod</button>
        </div>
      </div>
    </div>

    <div v-if="showSettingsModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4">
      <div class="w-full max-w-sm rounded-lg border border-gray-600 bg-marshal-card p-6 shadow-2xl">
        <h3 class="mb-2 text-xl font-bold text-white">
          Settings for <span class="text-marshal-primary">{{ modToEditName }}</span>
        </h3>
        
        <div class="flex flex-col gap-4">
          <div class="flex flex-col">
            <label class="mb-1 text-sm font-medium text-gray-400">Compiler Output</label>
            <div class="flex gap-2">
              <input type="text" :value="settingsFormOutputDir" class="grow rounded border border-gray-600 bg-gray-700 p-2 text-sm text-white outline-none focus:border-marshal-primary" readonly>
              <button @click="browseSettingsOutputDirectory" class="inline-flex items-center justify-center gap-2 rounded bg-gray-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-700">Browse</button>
            </div>
          </div>

          <div class="mt-4 border-t border-gray-600 pt-4">
            <label class="text-xs font-medium uppercase text-gray-400">Workspace Management</label>
            <div class="mt-2 grid grid-cols-2 gap-3">
              <button @click="exportWorkspace" class="inline-flex h-9 w-fit min-w-24 items-center justify-center gap-2 justify-self-start whitespace-nowrap rounded bg-gray-600 px-3 py-1.5 text-sm font-semibold text-white transition hover:bg-gray-700">
                <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"></path></svg>
                <span>Export</span>
              </button>
              <button @click="triggerDeletionFromSettings" class="inline-flex h-9 w-fit min-w-24 items-center justify-center gap-2 justify-self-start whitespace-nowrap rounded bg-gray-700 px-3 py-1.5 text-sm font-semibold text-white transition hover:bg-marshal-danger">
                <svg class="h-5 w-5 shrink-0" fill="currentColor" viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"></path></svg>
                <span>Delete</span>
              </button>
            </div>
          </div>
        </div>

        <div class="mt-8 flex justify-end gap-2">
          <button @click="showSettingsModal = false" class="inline-flex items-center justify-center gap-2 rounded bg-gray-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-700">Cancel</button>
          <button @click="saveModSettings" class="inline-flex items-center justify-center gap-2 rounded bg-marshal-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50" :disabled="submittingSettings">Save Changes</button>
        </div>
      </div>
    </div>

    <div v-if="showDeleteModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4">
      <div class="w-full max-w-sm rounded-lg border border-gray-600 bg-marshal-card p-6 shadow-2xl">
        <h3 class="mb-2 text-xl font-bold text-red-500">Confirm Deletion</h3>
        <p class="mb-4 text-sm text-gray-300">
          <span v-if="deleteError" class="mb-2 block text-red-500">ERROR: {{ deleteError }}</span>
          Are you sure you want to permanently delete the mod: <strong class="text-marshal-warning">{{ modToDeleteName }}</strong>? This action <strong>cannot be undone</strong> and will remove all associated files from your disk.
        </p>
        <div class="flex justify-end gap-2">
          <button @click="showDeleteModal = false" class="inline-flex items-center justify-center gap-2 rounded bg-gray-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-700">Cancel</button>
          <button @click="confirmDeleteMod" class="inline-flex items-center justify-center gap-2 rounded bg-marshal-danger px-4 py-2 text-sm font-semibold text-white transition hover:bg-marshal-danger-dark disabled:cursor-not-allowed disabled:opacity-50" :disabled="submittingDelete">DELETE MOD</button>
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