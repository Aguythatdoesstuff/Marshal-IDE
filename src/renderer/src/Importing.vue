<template>
  <div class="workspace-wrapper min-h-screen bg-marshal-sidebar font-sans text-marshal-text">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - Importer</span>
    </div>

    <div class="mx-auto flex w-full max-w-3xl flex-1 flex-col items-center px-4 py-8">
      
      <div v-if="!isProcessing" class="w-full rounded-lg border border-gray-700 bg-marshal-card p-8 shadow-xl">
        <header class="mb-8 border-b border-gray-700 pb-4 text-center">
          <h1 class="text-3xl font-bold text-white">Import Project</h1>
          <p class="mt-2 text-marshal-muted">Configure your workspace mapping</p>
        </header>

        <div class="flex flex-col gap-5">
          <div class="flex flex-col">
            <label class="mb-1 text-sm font-medium text-gray-400">Path of Mod to Import</label>
            <div class="flex gap-2">
              <input type="text" v-model="form.sourcePath" placeholder="Select source directory..." class="grow rounded border border-gray-600 bg-gray-700 p-2 text-white outline-none focus:border-marshal-primary" readonly>
              <button @click="browseSource" class="inline-flex items-center justify-center rounded bg-gray-600 px-4 py-2 font-semibold text-white transition hover:bg-gray-700">Browse</button>
            </div>
          </div>

          <div class="flex flex-col">
            <label class="mb-1 text-sm font-medium text-gray-400">Workspace Name</label>
            <input type="text" v-model="form.workspaceName" placeholder="My Imported Mod" class="rounded border border-gray-600 bg-gray-700 p-2 text-white outline-none focus:border-marshal-primary">
          </div>

          <div class="mt-4 flex items-center gap-2">
            <input type="checkbox" v-model="form.sameAsInput" id="same-output-chk">
            <label for="same-output-chk" class="text-sm font-medium text-gray-400">Output path is same as input</label>
          </div>

          <div class="flex flex-col" :class="{ 'pointer-events-none opacity-50': form.sameAsInput }">
            <label class="mb-1 text-sm font-medium text-gray-400">Mod Output Path</label>
            <div class="flex gap-2">
              <input type="text" :value="computedOutputPath" class="grow rounded border border-gray-600 bg-gray-700 p-2 text-white outline-none focus:border-marshal-primary" readonly>
              <button @click="browseOutput" class="inline-flex items-center justify-center rounded bg-gray-600 px-4 py-2 font-semibold text-white transition hover:bg-gray-700 disabled:cursor-not-allowed disabled:opacity-50" :disabled="form.sameAsInput">Browse</button>
            </div>
            <p class="mt-1 text-xs text-gray-500">Where the compiled mod files will reside.</p>
          </div>
        </div>
        
        <div class="mt-8 flex justify-end gap-3">
          <button @click="goBack" class="inline-flex items-center justify-center rounded bg-gray-600 px-4 py-2 font-semibold text-white transition hover:bg-gray-700">Cancel</button>
          <button @click="startImport" class="inline-flex items-center justify-center rounded bg-marshal-primary px-4 py-2 font-semibold text-white transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50" :disabled="!isFormValid">Start Import</button>
        </div>
      </div>

      <div v-else class="flex w-full flex-col gap-8">
        <div class="flex flex-col items-center text-center">
          <div v-if="!isDone" class="mb-4 h-12 w-12 animate-spin rounded-full border-4 border-white/10 border-l-marshal-primary"></div>
          <svg v-else class="mb-4 h-12 w-12 text-emerald-500" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline>
          </svg>
          <h2 class="text-2xl text-white">{{ isDone ? 'Import Complete!' : 'Processing Import...' }}</h2>
          <p class="mt-2 text-marshal-muted">{{ isDone ? 'Your workspace is ready.' : 'Please do not close the application.' }}</p>
        </div>

        <div class="grid grid-cols-3 gap-4">
          <div class="flex flex-col items-center rounded-md border border-gray-700 bg-marshal-editor p-4">
            <span class="mb-2 text-xs font-bold uppercase text-marshal-muted">OS CPU Usage</span>
            <span class="text-2xl font-bold tabular-nums text-white" :class="{ 'text-marshal-muted': isDone }">{{ telemetry.cpu }}%</span>
          </div>
          <div class="flex flex-col items-center rounded-md border border-gray-700 bg-marshal-editor p-4">
            <span class="mb-2 text-xs font-bold uppercase text-marshal-muted">RAM Allocation</span>
            <span class="text-2xl font-bold tabular-nums text-white" :class="{ 'text-marshal-muted': isDone }">{{ telemetry.ram }} MB</span>
          </div>
          <div class="flex flex-col items-center rounded-md border border-gray-700 bg-marshal-editor p-4">
            <span class="mb-2 text-xs font-bold uppercase text-marshal-muted">Elapsed Time</span>
            <span class="text-2xl font-bold tabular-nums text-marshal-primary">{{ formattedTime }}</span>
          </div>
        </div>

        <div class="flex h-[250px] max-h-[400px] min-h-[250px] flex-1 flex-col overflow-hidden rounded-md border border-gray-700 bg-black">
          <ConsolePanel 
            :customLogs="logs" 
            :isMinimized="false"
            @clear-logs="logs = []"
          />
        </div>
        
        <div class="mt-4 flex justify-center">
          <button @click="goBack" class="inline-flex items-center justify-center rounded bg-marshal-primary px-4 py-2 font-semibold text-white transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50" :disabled="!isDone">Return to Workspaces</button>
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