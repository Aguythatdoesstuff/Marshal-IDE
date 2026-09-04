<template>
  <div class="flex h-screen flex-col overflow-hidden bg-marshal-editor font-sans text-marshal-text">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - Global Settings</span>
    </div>

    <div class="flex-1 overflow-y-auto px-8 pb-24 pt-12">
      <header class="mx-auto mb-8 max-w-4xl">
        <h1 class="text-3xl font-bold text-white">Global Application Settings</h1>
        <p class="mt-2 text-marshal-muted">Configure IDE preferences and system logging</p>
      </header>

      <div class="mx-auto max-w-4xl">
        <nav class="mb-6 flex gap-6 border-b border-marshal-border">
          <span 
            class="cursor-pointer border-b-2 border-transparent px-1 py-3 text-sm text-marshal-muted transition hover:text-white"
            :class="{ 'border-marshal-primary font-semibold text-marshal-primary': activeTab === 'workspace' }"
            @click="switchTab('workspace')"
          >
            Workspace Settings
          </span>
          <span 
            class="cursor-pointer border-b-2 border-transparent px-1 py-3 text-sm text-marshal-muted transition hover:text-white"
            :class="{ 'border-marshal-primary font-semibold text-marshal-primary': activeTab === 'logger' }"
            @click="switchTab('logger')"
          >
            Logger Settings
          </span>
        </nav>
        
        <div>
          
          <div v-if="activeTab === 'workspace'" class="animate-fade-in">
            <h3 class="mb-2 text-xl font-semibold text-white">Mod Output Directories</h3>
            <p class="mb-6 text-sm text-marshal-muted">Configure the final compilation destination for each of your local mods.</p>
            
            <div class="overflow-hidden rounded-lg border border-marshal-border bg-marshal-card">
              <div class="grid grid-cols-[minmax(8rem,0.8fr)_minmax(12rem,2fr)_auto] gap-4 border-b border-marshal-border bg-white/[0.02] px-5 py-3 text-xs font-semibold uppercase tracking-wider text-marshal-muted">
                <span>Mod Name</span><span>Output Directory Path</span><span class="text-right">Action</span>
              </div>

              <div>
                <div v-if="loadingProjects" class="p-6 text-center text-sm text-marshal-muted animate-pulse">
                  Loading workspace data...
                </div>

                <div v-else-if="Object.keys(projects).length === 0" class="p-6 text-center text-sm text-marshal-muted">
                  No mods found. Create one on the main screen to configure its output directory.
                </div>

                <div 
                  v-else
                  v-for="(config, name) in projects" 
                  :key="name"
                  class="grid grid-cols-[minmax(8rem,0.8fr)_minmax(12rem,2fr)_auto] items-center gap-4 border-b border-marshal-border px-5 py-3 last:border-0"
                >
                  <span class="truncate text-sm text-white" :title="name">{{ name }}</span>
                  <input 
                    type="text" 
                    :value="config.output_dir || ''" 
                    class="min-w-0 rounded border border-marshal-border bg-marshal-editor px-3 py-2 text-sm text-marshal-muted outline-none" 
                    readonly
                    placeholder="No output directory set..."
                  >
                  <div class="flex justify-end">
                    <button 
                      class="rounded bg-gray-700 px-3 py-1.5 text-sm font-semibold text-white transition hover:bg-marshal-primary"
                      :class="{ 'bg-marshal-primary': activeDialogProject === name }"
                      @click="handleWorkspaceEdit(name)"
                    >
                      Browse...
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div v-if="activeTab === 'logger'" class="animate-fade-in">
            <h3 class="mb-2 text-xl font-semibold text-white">Application Logging Preferences</h3>
            <p class="mb-6 text-sm text-marshal-muted">Control how system log files are managed and rotated by the application.</p>
            
            <div class="rounded-lg border border-marshal-border bg-marshal-card p-5">
              <div class="flex flex-wrap items-center justify-between gap-6">
                <div class="flex flex-col gap-1">
                  <label class="text-sm font-semibold text-white">Max Archived Log Size</label>
                  <span class="max-w-xl text-sm text-marshal-muted">
                    When total archived logs exceed this limit, oldest files are deleted (minimum: 10 MB).
                  </span>
                </div>
                <div class="flex items-center gap-4">
                  <div class="flex items-center rounded border border-marshal-border bg-marshal-editor">
                    <input 
                      type="number" 
                      v-model.number="maxLogSize" 
                      step="5" 
                      class="w-20 bg-transparent px-3 py-2 text-white outline-none"
                      @input="setUnsavedChanges(true)"
                    >
                    <span class="px-2 text-sm text-marshal-muted">MB</span>
                  </div>
                  <button class="text-sm text-marshal-primary hover:underline" @click="resetSetting('logger-max-size')">
                    Reset to Default
                  </button>
                </div>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>

    <footer class="fixed bottom-0 left-0 right-0 flex items-center justify-between border-t border-marshal-border bg-marshal-sidebar px-8 py-4">
      <button class="rounded border border-marshal-border px-3 py-2 text-sm text-marshal-text transition hover:border-red-500 hover:bg-red-500/10 hover:text-red-400" @click="handleResetAllToDefault">
        Reset All to Default
      </button>
      <div class="flex gap-3">
        <button class="rounded bg-gray-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-600" @click="handleCloseClick">
          Close
        </button>
        <button 
          class="rounded bg-marshal-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="isSaving"
          @click="saveGlobalSettings(false)"
        >
          <span v-if="isSaving" class="mr-2 inline-block h-3 w-3 animate-spin rounded-full border-2 border-white/30 border-t-white"></span>
          {{ isSaving ? 'Saving...' : (hasUnsavedChanges ? 'Save Changes *' : 'Save Global Settings') }}
        </button>
      </div>
    </footer>
    
    <div v-if="showUnsavedModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4">
      <div class="w-full max-w-md rounded-lg border border-marshal-border bg-marshal-card p-6 shadow-2xl">
        <div class="flex items-center gap-3">
          <svg class="h-6 w-6 text-amber-400" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
          <h3 class="text-lg font-semibold text-white">Unsaved Changes</h3>
        </div>
        <p class="my-4 text-sm text-marshal-muted">
          You have made changes to your global preferences. Do you want to <strong>save</strong> them before closing?
        </p>
        <div class="flex justify-end gap-3">
          <button class="rounded bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700" @click="closeSettingsPage">Discard</button>
          <button class="rounded bg-gray-700 px-4 py-2 text-sm font-semibold text-white hover:bg-gray-600" @click="showUnsavedModal = false">Cancel</button>
          <button class="rounded bg-marshal-primary px-4 py-2 text-sm font-semibold text-white hover:bg-blue-600 disabled:opacity-50" :disabled="isSaving" @click="saveGlobalSettings(true)">Save & Close</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';

// --- State Variables ---
const activeTab = ref('workspace');
const hasUnsavedChanges = ref(false);
const showUnsavedModal = ref(false);
const loadingProjects = ref(false);
const isSaving = ref(false);
const activeDialogProject = ref(null); 

const maxLogSize = ref(100);
const projects = ref({});

const DEFAULT_SETTINGS = {
  logger: {
    max_archived_mb: 20,
    MIN_LOG_SIZE: 10
  }
};

let initialSettingsData = { logger: {} };

// --- Lifecycle Methods ---
onMounted(async () => {
  await loadGlobalSettings();
  await loadWorkspaceModList();
});

// --- Tab Navigation Methods ---
function switchTab(tabName) {
  window.api.log.info(`Switching to settings tab: ${tabName}`, 'Renderer-Settings-UI');
  activeTab.value = tabName;
  if (tabName === 'workspace') {
    loadWorkspaceModList();
  }
}

// --- Loading Data via IPC ---
async function loadGlobalSettings() {
  window.api.log.info('Fetching logger settings from main process...', 'Renderer-Settings-UI');
  try {
    const result = await window.api.invoke('load-all-global-settings');
    if (result.success && result.settings?.logger) {
      initialSettingsData = {
        logger: {
          ...DEFAULT_SETTINGS.logger,
          max_archived_mb: result.settings.logger.max_archived_mb || DEFAULT_SETTINGS.logger.max_archived_mb
        }
      };
    } else {
      initialSettingsData = JSON.parse(JSON.stringify(DEFAULT_SETTINGS));
    }
  } catch (error) {
    window.api.log.error(`[ERROR] Load failed: ${error.message}`, 'Renderer-Settings-UI');
    initialSettingsData = JSON.parse(JSON.stringify(DEFAULT_SETTINGS));
  }

  maxLogSize.value = initialSettingsData.logger.max_archived_mb;
  setUnsavedChanges(false);
}

async function loadWorkspaceModList() {
  loadingProjects.value = true;
  window.api.log.info('Requesting workspace project list.', 'Renderer-Settings-IPC');
  try {
    const result = await window.api.invoke('list-projects', {});
    if (result.success && result.projects) {
      projects.value = result.projects;
      window.api.log.info(`${Object.keys(result.projects).length} projects rendered.`, 'Renderer-Settings-UI');
    } else {
      projects.value = {};
      window.api.log.warning('[WARNING] No projects found to display.', 'Renderer-Settings-UI');
    }
  } catch (error) {
    window.api.log.error(`[ERROR] Failed to load workspace list: ${error.message}`, 'Renderer-Settings-IPC');
  } finally {
    loadingProjects.value = false;
  }
}

// --- Mutators & Handlers ---
function setUnsavedChanges(state) {
  hasUnsavedChanges.value = state;
  if (state) {
    window.api.log.warning('[UNSAVED] Settings state changed to UNSAVED.', 'Renderer-Settings-State');
  } else {
    window.api.log.info('Settings state changed to SAVED.', 'Renderer-Settings-State');
  }
}

async function handleWorkspaceEdit(projectName) {
  activeDialogProject.value = projectName;
  const currentPath = projects.value[projectName]?.output_dir || '';
  window.api.log.info(`Opening directory dialog for mod: ${projectName}`, 'Renderer-Settings-UI');
  
  try {
    const result = await window.api.invoke('open-directory-dialog', { defaultPath: currentPath });
    if (result.success && result.path && result.path !== currentPath) {
      projects.value[projectName].output_dir = result.path;
      setUnsavedChanges(true);
      window.api.log.info(`New output path selected for ${projectName}: ${result.path}`, 'Renderer-Settings-UI');
    } else if (!result.path) {
      window.api.log.warning("[WARNING] Directory dialog cancelled.", 'Renderer-Settings-UI');
    }
  } catch (error) {
    window.api.log.error(`[ERROR] IPC call 'open-directory-dialog' failed: ${error.message}`, 'Renderer-Settings-IPC');
  } finally {
    activeDialogProject.value = null;
  }
}

function resetSetting(settingId) {
  window.api.log.warning(`[RESET] Attempting to reset setting: ${settingId} to default.`, 'Renderer-Settings-Reset');
  if (settingId === 'logger-max-size') {
    maxLogSize.value = DEFAULT_SETTINGS.logger.max_archived_mb;
    window.api.log.info('Logger max size reset to default value.', 'Renderer-Settings-Reset');
    setUnsavedChanges(true);
  }
}

function handleResetAllToDefault() {
  const confirmed = confirm('Are you sure you want to reset ALL global settings to default? This cannot be undone!');
  if (!confirmed) {
    window.api.log.info('User cancelled "Reset All" action.', 'Renderer-Settings-Reset');
    return;
  }
  maxLogSize.value = DEFAULT_SETTINGS.logger.max_archived_mb;
  window.api.log.warning('[RESET] All global settings reset to default state.', 'Renderer-Settings-Reset');
  setUnsavedChanges(true);
}

// --- Persistence Action ---
async function saveGlobalSettings(shouldClose = false) {
  window.api.log.info('Attempting to save global settings.', 'Renderer-Settings-Save');
  
  const parsedSize = parseInt(maxLogSize.value);
  const MIN_LOG_SIZE = DEFAULT_SETTINGS.logger.MIN_LOG_SIZE;
  
  if (isNaN(parsedSize) || parsedSize < MIN_LOG_SIZE) {
    alert(`Max Archived Log Size must be a number greater than or equal to ${MIN_LOG_SIZE} MB.`);
    window.api.log.error(`[ERROR] Save failed: Validation minimum breach.`, 'Renderer-Settings-Validation');
    return;
  }

  isSaving.value = true;

  const workspaceUpdates = {};
  Object.keys(projects.value).forEach(projectName => {
    workspaceUpdates[projectName] = projects.value[projectName].output_dir || '';
  });

  const settingsData = {
    logger: { max_archived_mb: parsedSize },
    workspaceUpdates
  };

  try {
    const saveResult = await window.api.invoke('save-global-settings', settingsData);
    if (saveResult.success) {
      initialSettingsData.logger.max_archived_mb = parsedSize;
      setUnsavedChanges(false);
      
      // Replacing native alert with a subtle console/log interaction is ideal in a real app,
      // but keeping alert per original code structure, just styling the flow.
      if (!shouldClose) alert('Settings Saved Successfully!');
      
      if (shouldClose) {
        closeSettingsPage();
      }
    } else {
      alert(`Failed to save settings: ${saveResult.message}`);
    }
  } catch (error) {
    window.api.log.error(`[ERROR] IPC save invocation crashed: ${error.message}`, 'Renderer-Settings-IPC');
  } finally {
    isSaving.value = false;
  }
}

// --- Navigation Close Routing ---
function handleCloseClick() {
  if (hasUnsavedChanges.value) {
    showUnsavedModal.value = true;
  } else {
    closeSettingsPage();
  }
}

function closeSettingsPage() {
  showUnsavedModal.value = false;
  window.api.log.info('User closed global settings page.', 'Renderer-Navigation');
  window.api.send('switch-page', 'index');
}
</script>

