<template>
  <div id="eula-wrapper" class="bg-marshal-sidebar font-sans text-marshal-text">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - License Agreement</span>
    </div>
    
    <div class="mb-6 text-center">
      <h1 class="text-2xl font-bold text-white">License Agreement</h1>
      <p class="mt-1 text-xs uppercase tracking-widest transition-colors" :style="{ color: statusColor }">{{ statusDisplayName }}</p>
    </div>
    
    <div ref="contentDiv" id="eula-content" class="mx-20 mb-6 w-[calc(100%-10rem)] flex-1 overflow-y-auto rounded border border-gray-700 bg-marshal-editor p-8 font-mono text-[0.85rem] text-gray-300">{{ eulaContent }}</div>

    <div class="mb-4 flex justify-center gap-6">
      <button @click="declineEula" class="inline-flex items-center justify-center rounded bg-gray-700 px-10 py-2 text-sm font-semibold text-white transition hover:bg-gray-600 disabled:cursor-not-allowed disabled:opacity-50">
        Decline & Exit
      </button>
      <button @click="acceptEula" class="inline-flex items-center justify-center rounded bg-marshal-primary px-10 py-2 text-sm font-bold text-white shadow-lg transition hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50" :disabled="isLoadError || eulaContent === 'Loading...'">
        Accept & Continue
      </button>
    </div>

    <div class="text-center">
      <button @click="openModal" class="text-[10px] uppercase tracking-wider text-gray-500 underline transition hover:text-white">
        I have a license code
      </button>
    </div>

    <div v-if="isModalOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-black/80">
      <div class="w-80 rounded border border-gray-600 bg-gray-800 p-6 shadow-2xl">
        <h2 class="mb-2 text-xl font-bold text-white">License Code</h2>
        <p class="mb-4 text-[10px] text-gray-400">Enter your private access code to unlock special terms.</p>
        
        <input 
          ref="modalInput"
          type="text" 
          class="mb-4 w-full rounded border border-gray-600 bg-marshal-editor p-2 text-sm text-white outline-none focus:border-marshal-primary" 
          placeholder="Enter code here..."
          v-model="rawInput"
          @keypress.enter="submitCode"
        >
        
        <div class="flex justify-end gap-2">
          <button @click="closeModal" class="px-3 py-1 text-xs text-gray-400 transition hover:text-white">Cancel</button>
          <button @click="submitCode" class="rounded bg-marshal-primary px-4 py-1 text-xs font-bold text-white transition hover:bg-blue-600">Apply</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue';

// --- Reactive State ---
const eulaContent = ref("Loading...");
const statusDisplayName = ref("Standard User License");
const statusColor = ref("#60a5fa"); // Reactive variable targeted by CSS v-bind()
const isModalOpen = ref(false);
const rawInput = ref("");
const isLoadError = ref(false);
let activeCode = "";

// Template references
const contentDiv = ref(null);
const modalInput = ref(null);

const loadLicense = async (code = "") => {
  try {
    isLoadError.value = false;
    const result = await window.api.invoke('get-eula-text', code);
    
    // Check if the backend caught an internal error and returned an error block
    if (result.displayName === "Error" || result.content.startsWith("Error:")) {
      isLoadError.value = true;
    }

    eulaContent.value = result.content;
    activeCode = code;
    statusDisplayName.value = result.displayName;

    // Color updates dynamically and triggers the v-bind() below
    statusColor.value = result.isSpecial ? "#4ade80" : "#60a5fa";

    nextTick(() => {
      if (contentDiv.value) {
        contentDiv.value.scrollTop = 0;
      }
    });
  } catch (err) {
    isLoadError.value = true;
    eulaContent.value = "Error: Could not load license.";
    statusDisplayName.value = "Error";
    statusColor.value = "#ef4444"; // Red color indicator for error
  }
};

// --- Lifecycle ---
onMounted(() => {
  loadLicense(""); 
});

// --- UI Actions ---
const openModal = () => {
  isModalOpen.value = true;
  nextTick(() => {
    if (modalInput.value) modalInput.value.focus();
  });
};

const closeModal = () => {
  isModalOpen.value = false;
  rawInput.value = "";
};

const submitCode = () => {
  const code = rawInput.value.trim();
  loadLicense(code);
  closeModal();
};

const acceptEula = () => {
  window.api.send('accept-eula', activeCode);
};

const declineEula = () => {
  window.api.send('decline-eula');
};
</script>