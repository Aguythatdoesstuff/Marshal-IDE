<template>
  <div class="workspace-wrapper">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - Importer</span>
    </div>

    <div class="main-container">
      
      <div v-if="!isProcessing" class="card form-card">
        <header class="header-section">
          <h1 class="title">Import Project</h1>
          <p class="subtitle">Configure your workspace mapping</p>
        </header>

        <div class="form-layout">
          <div class="form-group">
            <label class="form-label">Path of Mod to Import</label>
            <div class="input-inline-group">
              <input type="text" v-model="form.sourcePath" placeholder="Select source directory..." class="form-input flex-grow" readonly>
              <button @click="browseSource" class="btn btn-neutral">Browse</button>
            </div>
          </div>

          <div class="form-group">
            <label class="form-label">Workspace Name</label>
            <input type="text" v-model="form.workspaceName" placeholder="My Imported Mod" class="form-input">
          </div>

          <div class="form-checkbox-group spacing-top-md">
            <input type="checkbox" v-model="form.sameAsInput" id="same-output-chk">
            <label for="same-output-chk" class="form-label label-inline">Output path is same as input</label>
          </div>

          <div class="form-group" :class="{ 'disabled-group': form.sameAsInput }">
            <label class="form-label">Mod Output Path</label>
            <div class="input-inline-group">
              <input type="text" :value="computedOutputPath" class="form-input flex-grow" readonly>
              <button @click="browseOutput" class="btn btn-neutral" :disabled="form.sameAsInput">Browse</button>
            </div>
            <p class="form-help-text">Where the compiled mod files will reside.</p>
          </div>
        </div>
        
        <div class="action-footer spacing-top-lg">
          <button @click="goBack" class="btn btn-neutral">Cancel</button>
          <button @click="startImport" class="btn btn-primary" :disabled="!isFormValid">Start Import</button>
        </div>
      </div>

      <div v-else class="processing-container">
        <div class="spinner-section">
          <div v-if="!isDone" class="loader"></div>
          <svg v-else class="success-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline>
          </svg>
          <h2 class="processing-title">{{ isDone ? 'Import Complete!' : 'Processing Import...' }}</h2>
          <p class="processing-subtitle">{{ isDone ? 'Your workspace is ready.' : 'Please do not close the application.' }}</p>
        </div>

        <div class="telemetry-dashboard">
          <div class="metric-card">
            <span class="metric-label">OS CPU Usage</span>
            <span class="metric-value" :class="{ 'text-muted': isDone }">{{ telemetry.cpu }}%</span>
          </div>
          <div class="metric-card">
            <span class="metric-label">RAM Allocation</span>
            <span class="metric-value" :class="{ 'text-muted': isDone }">{{ telemetry.ram }} MB</span>
          </div>
          <div class="metric-card">
            <span class="metric-label">Elapsed Time</span>
            <span class="metric-value timer-text">{{ formattedTime }}</span>
          </div>
        </div>

        <div class="console-box" style="flex: 1; min-height: 250px; max-height: 400px; display: flex; flex-direction: column;">
          <ConsolePanel 
            :customLogs="logs" 
            :isMinimized="false"
            @clear-logs="logs = []"
          />
        </div>
        
        <div class="action-footer justify-center spacing-top-md">
          <button @click="goBack" class="btn btn-primary" :disabled="!isDone">Return to Workspaces</button>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, nextTick } from 'vue';
import ConsolePanel from '../ide/Console.vue';

const isProcessing = ref(false);
const isDone = ref(false);

const form = reactive({
  sourcePath: '',
  workspaceName: '',
  sameAsInput: true,
  customOutputPath: ''
});

// Mock telemetry state ready to be bound to a real IPC backend later
const telemetry = reactive({
  cpu: '0.0',
  ram: '0'
});

const logs = ref([]);
const consoleBodyElement = ref(null);
const timerSeconds = ref(0);
let timerInterval = null;
let fakeDataInterval = null; // Remove this once connected to backend

// Computed Form Validation
const computedOutputPath = computed(() => {
  return form.sameAsInput ? form.sourcePath : form.customOutputPath;
});

const isFormValid = computed(() => {
  if (!form.sourcePath || !form.workspaceName) return false;
  if (!form.sameAsInput && !form.customOutputPath) return false;
  return true;
});

const formattedTime = computed(() => {
  const mins = Math.floor(timerSeconds.value / 60).toString().padStart(2, '0');
  const secs = (timerSeconds.value % 60).toString().padStart(2, '0');
  return `${mins}:${secs}`;
});

// Front End Navigation
const goBack = () => {
  window.api.send('switch-page', 'workspace');
};

// Simulated File Browsing Dialogs
const browseSource = async () => {
  try {
    const result = await window.api.invoke('open-directory-dialog', {});
    if (result.success) form.sourcePath = result.path;
  } catch (error) { console.error(error); }
};

const browseOutput = async () => {
  try {
    const result = await window.api.invoke('open-directory-dialog', {});
    if (result.success) form.customOutputPath = result.path;
  } catch (error) { console.error(error); }
};


let hasFatalIpcError = false;

// Set up event listeners for streaming terminal text from main process
onMounted(() => {
  if (window.api && window.api.on) {
    window.api.on('importer-stdout-line', (arg1, arg2) => {
      const message = arg2 !== undefined ? arg2 : arg1;
      console.log(message);

      // Catch structured fatal errors from the C# IPC
      if (typeof message === 'string' && message.includes('[[IPC]]:')) {
        try {
          const jsonStr = message.substring(message.indexOf('[[IPC]]:') + 8);
          const ipcData = JSON.parse(jsonStr);
          if (ipcData.type === 'FatalError' || ipcData.type === 'FatalErrorInfo') {
            hasFatalIpcError = true;
          }
        } catch (e) { /* ignore parse errors from incomplete streams */ }
      }

      logs.value = [...logs.value, { type: 'info', text: message, message: message }];
    });
    window.api.on('importer-stderr-line', (arg1, arg2) => {
      const errorMsg = arg2 !== undefined ? arg2 : arg1;
      console.error(errorMsg);
      logs.value.push({ type: 'error', text: errorMsg, message: errorMsg });
    });
    window.api.on('importer-telemetry', (arg1, arg2) => {
      const data = arg2 !== undefined ? arg2 : arg1;
      if (data) {
        telemetry.cpu = data.cpu;
        telemetry.ram = data.ram;
      }
    });
  }
});

// Start Import sequence
const startImport = async () => {
  isProcessing.value = true;
  isDone.value = false;
  timerSeconds.value = 0;
  logs.value = [];
  
  timerInterval = setInterval(() => {
    timerSeconds.value++;
  }, 1000);

  console.warn(`Initializing workspace creation for ${form.workspaceName}...`);

  try {
    // 1. Create the Workspace folder architecture
    const createResult = await window.api.invoke('create-project', { 
      projectName: form.workspaceName.trim(), 
      outputDir: computedOutputPath.value.trim(),
      includeTemplates: false
    });

    if (!createResult.success) {
      throw new Error(createResult.message || "Failed to finalize project path tracking structures.");
    }
    
    console.warn(`Workspace allocated on disk. Executing native binary...`);

    // 2. Spawn and track execution
    hasFatalIpcError = false; // Reset before run
    const importResult = await window.api.invoke('run-importer', {
      input: form.sourcePath.trim(),
      workspaceName: form.workspaceName.trim()
    });

    clearInterval(timerInterval);
    telemetry.cpu = '0.0';
    telemetry.ram = '0';

    // Check both the explicit IPC error flag and the standard process exit result
    if (!importResult || !importResult.success || hasFatalIpcError) {
      throw new Error((importResult && importResult.message) || "Importer crashed or encountered a fatal error.");
    }

    isDone.value = true;
    console.log(`Import operation finalized successfully! All targets written.`);

  } catch (error) {
    clearInterval(timerInterval);
    telemetry.cpu = '0.0';
    telemetry.ram = '0';
    isDone.value = true; 
    console.log(`Fatal Error: ${error.message}`, 'error');
    
    // Push the final error to the console UI
    logs.value = [...logs.value, { type: 'error', text: `Fatal Error: ${error.message}`, message: `Fatal Error: ${error.message}` }];
    
    // Cleanup the workspace since it failed
    try {
      await window.api.invoke('delete-project', { projectName: form.workspaceName.trim() });
    } catch (cleanupErr) {
      console.error("Cleanup path removal failed:", cleanupErr);
    }
    
    // Show the mandated popup
    alert("Failed to import.\n\nBefore reporting any bugs and error:\n1. Run the mod in the game itself\n2. Fix all warnings and errors reported by the game engine\n3. Run again.\n\nStill crashing/failing? Report the crash!");
  }
};
</script>

<style scoped lang="scss">
/* Inherit core colors and variables from your global theme */
$editor-bg: var(--editor-bg, #1e1e1e);
$card-bg: var(--card-bg, #252526);
$primary-blue: var(--primary-blue, #007acc);
$text-color: var(--text-color, #cccccc);
$text-muted: var(--text-muted, #858585);

.main-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 100%;
  max-width: 48rem;
  margin: 2rem auto;
  padding: 0 1rem;
}

.card {
  background-color: $card-bg;
  border: 1px solid #37373d;
  padding: 2rem;
  border-radius: 8px;
  width: 100%;
  box-shadow: 0 10px 25px rgba(0,0,0,0.3);
}

.header-section {
  text-align: center;
  margin-bottom: 2rem;
  border-bottom: 1px solid #37373d;
  padding-bottom: 1rem;

  .title { margin: 0; font-size: 1.75rem; color: #fff; }
  .subtitle { margin: 0.5rem 0 0 0; color: $text-muted; }
}

/* Form Layout */
.form-layout { display: flex; flex-direction: column; gap: 1.25rem; }
.form-group {
  display: flex; flex-direction: column;
  &.disabled-group { opacity: 0.5; pointer-events: none; }
}

.form-label { font-size: 0.875rem; font-weight: 500; color: #9ca3af; margin-bottom: 0.25rem; }
.form-input {
  padding: 0.5rem; background-color: #374151; border: 1px solid #4b5563; 
  color: #fff; border-radius: 4px; outline: none;
  &:focus { border-color: $primary-blue; }
}
.input-inline-group { display: flex; gap: 0.5rem; }
.flex-grow { flex-grow: 1; }
.form-help-text { font-size: 0.75rem; color: #6b7280; margin-top: 0.25rem; }
.form-checkbox-group { display: flex; align-items: center; gap: 0.5rem; }
.label-inline { margin-bottom: 0; cursor: pointer; }

/* Buttons */
.action-footer { display: flex; justify-content: flex-end; gap: 0.75rem; }
.justify-center { justify-content: center; }
.btn {
  padding: 0.5rem 1rem; border-radius: 4px; font-weight: 600; cursor: pointer;
  border: none; display: inline-flex; align-items: center; transition: 0.2s;
  &:disabled { opacity: 0.5; cursor: not-allowed; }
}
.btn-primary { background: $primary-blue; color: #fff; &:hover:not(:disabled) { background: #006bbd; } }
.btn-neutral { background: #4b5563; color: #fff; &:hover:not(:disabled) { background: #374151; } }

/* Processing & Telemetry View */
.processing-container {
  width: 100%; display: flex; flex-direction: column; gap: 2rem;
}

.spinner-section {
  display: flex; flex-direction: column; align-items: center; text-align: center;
}
.loader {
  border: 4px solid rgba(255, 255, 255, 0.1); border-left-color: $primary-blue;
  border-radius: 50%; width: 50px; height: 50px; animation: spin 1s linear infinite;
  margin-bottom: 1rem;
}
@keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }

.success-icon {
  width: 50px; height: 50px; color: #10b981; margin-bottom: 1rem;
}

.processing-title { font-size: 1.5rem; color: #fff; margin: 0; }
.processing-subtitle { color: $text-muted; margin-top: 0.5rem; }

/* Telemetry Dashboard */
.telemetry-dashboard {
  display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem;
}
.metric-card {
  background: #1e1e1e; border: 1px solid #37373d; padding: 1rem;
  border-radius: 6px; display: flex; flex-direction: column; align-items: center;
}
.metric-label { font-size: 0.75rem; text-transform: uppercase; color: $text-muted; margin-bottom: 0.5rem; font-weight: 700; }
.metric-value { font-size: 1.5rem; font-weight: 700; color: #fff; font-variant-numeric: tabular-nums; }
.timer-text { color: $primary-blue; }

/* Console Box */
.console-box {
  background: #111; border: 1px solid #37373d; border-radius: 6px; 
  overflow-y: auto; overflow-x: hidden; display: flex; flex-direction: column; height: 250px;
}
.console-header {
  background: #1e1e1e; padding: 0.5rem 1rem; font-size: 0.75rem; 
  font-weight: 600; color: $text-muted; border-bottom: 1px solid #37373d;
}
.console-body {
  flex-grow: 1; overflow-y: auto; padding: 0.75rem 1rem;
  font-family: 'JetBrains Mono', monospace; font-size: 0.8rem;
}
.log-line { margin-bottom: 0.25rem; word-break: break-all; }
.log-time { color: #666; margin-right: 0.5rem; }
.system { color: #4fc1ff; }
.info { color: #cccccc; }
.muted { color: #666; font-style: italic; }
.success { color: #10b981; font-weight: bold; }

/* Spacing Utils */
.spacing-top-md { margin-top: 1rem; }
.spacing-top-lg { margin-top: 2rem; }
</style>