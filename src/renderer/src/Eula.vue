<template>
  <div id="eula-wrapper">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - License Agreement</span>
    </div>
    
    <div class="header-section">
      <h1 class="title">License Agreement</h1>
      <p 
        class="status-badge" 
        :style="{ color: statusColor }"
      >
        {{ statusDisplayName }}
      </p>
    </div>
    
    <div ref="contentDiv" id="eula-content">
      {{ eulaContent }}
    </div>

    <div class="button-group">
      <button @click="declineEula" class="btn btn-decline">
        Decline & Exit
      </button>
      <button @click="acceptEula" class="btn btn-accept">
        Accept & Continue
      </button>
    </div>

    <div class="footer-link-container">
      <button @click="openModal" class="modal-trigger-btn">
        I have a license code
      </button>
    </div>

    <div v-if="isModalOpen" class="modal-overlay">
      <div class="modal-card">
        <h2 class="modal-title">License Code</h2>
        <p class="modal-subtitle">Enter your private access code to unlock special terms.</p>
        
        <input 
          ref="modalInput"
          type="text" 
          class="modal-input" 
          placeholder="Enter code here..."
          v-model="rawInput"
          @keypress.enter="submitCode"
        >
        
        <div class="modal-actions">
          <button @click="closeModal" class="btn-modal-cancel">Cancel</button>
          <button @click="submitCode" class="btn-modal-apply">Apply</button>
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
const statusColor = ref("#60a5fa"); // Default Blue
const isModalOpen = ref(false);
const rawInput = ref("");
let activeCode = "";

// Template references (to manipulate actual DOM elements when necessary)
const contentDiv = ref(null);
const modalInput = ref(null);

// --- Methods ---
const loadLicense = async (code = "") => {
  try {
    // Calls your Electron preload API
    const result = await window.api.invoke('get-eula-text', code);
    
    eulaContent.value = result.content;
    activeCode = code;
    statusDisplayName.value = result.displayName;

    // Color logic
    statusColor.value = result.isSpecial ? "#4ade80" : "#60a5fa";

    // Reset scroll container on the next DOM tick
    nextTick(() => {
      if (contentDiv.value) {
        contentDiv.value.scrollTop = 0;
      }
    });
  } catch (err) {
    eulaContent.value = "Error: Could not load license.";
  }
};

// --- Lifecycle ---
onMounted(() => {
  loadLicense(""); // Initial load
});

// --- UI Actions ---
const openModal = () => {
  isModalOpen.value = true;
  // Wait for modal to render in DOM before focusing the input
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