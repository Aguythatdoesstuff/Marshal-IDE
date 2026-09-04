<template>
  <div id="eula-wrapper">
    <div class="titlebar">
      <span class="titlebar-text">Marshal IDE - License Agreement</span>
    </div>
    
    <div class="header-section">
      <h1 class="title">License Agreement</h1>
      <p class="status-badge">{{ statusDisplayName }}</p>
    </div>
    
    <div ref="contentDiv" id="eula-content">{{ eulaContent }}</div>

    <div class="button-group">
      <button @click="declineEula" class="btn btn-decline">
        Decline & Exit
      </button>
      <button @click="acceptEula" class="btn btn-accept" :disabled="isLoadError || eulaContent === 'Loading...'">
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

<style scoped>
/* ==========================================================================
   EULA SPECIFIC MODULE STYLES (Scoped to prevent global bleeding)
   ========================================================================== */

.header-section {
  text-align: center;
  margin-bottom: 1.5rem; 

  .title {
    font-size: 1.5rem; 
    font-weight: 700;   
    color: #ffffff;
    margin: 0;
  }

  .status-badge {
    font-size: 0.75rem; 
    /* Vue magic: hooks directly into script statusColor ref */
    color: v-bind(statusColor);     
    text-transform: uppercase;
    letter-spacing: 0.1em; 
    margin-top: 0.25rem; 
    transition: color 0.2s ease;
  }
}

#eula-content {
  flex-grow: 1;
  background-color: var(--editor-bg);
  border: 1px solid #333;
  padding: 2rem;
  overflow-y: auto;
  font-family: 'Consolas', monospace;
  font-size: 0.85rem;
  white-space: pre-wrap;
  margin: 0 5rem 1.5rem 5rem;
  color: #d4d4d4;
  border-radius: 4px;
  width: calc(100% - 10rem); 
  box-sizing: border-box;
}

/* --- Buttons & Action Layout --- */
.button-group {
  display: flex;
  justify-content: center; 
  gap: 1.5rem; 
  margin-bottom: 1rem; 
}

.btn {
  padding: 0.5rem 2.5rem; 
  border-radius: 0.25rem; 
  font-size: 0.875rem;     
  transition: background-color 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  border: none;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;

  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  &.btn-decline {
    background-color: #374151; 
    font-weight: 600;          
    color: #ffffff;

    &:hover {
      background-color: #4b5563; 
    }
  }

  &.btn-accept {
    background-color: var(--primary-blue);
    font-weight: 700; 
    color: #ffffff;
    box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05); 

    &:hover {
      background-color: #2563eb; 
    }
  }
}

.footer-link-container {
  text-align: center;

  .modal-trigger-btn {
    background: none;
    border: none;
    cursor: pointer;
    font-size: 10px; 
    color: #6b7280;   
    text-decoration: underline;
    text-transform: uppercase;
    letter-spacing: 0.05em; 
    transition: color 0.2s;

    &:hover {
      color: #ffffff;
    }
  }
}

/* --- License Input Code Modal Framework --- */
.modal-overlay {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.8); 
  z-index: 50;
  display: flex;
  align-items: center;
  justify-content: center;

  .modal-card {
    background-color: #2d2d2d;
    padding: 1.5rem; 
    border-radius: 0.25rem;
    border: 1px solid #444;
    box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25); 
    width: 20rem; 
    box-sizing: border-box;
  }

  .modal-title {
    color: #ffffff;
    font-weight: 700;
    margin-bottom: 0.5rem;
    margin-top: 0;
    font-size: 1.25rem;
  }

  .modal-subtitle {
    font-size: 10px;
    color: #9ca3af; 
    margin-bottom: 1rem;
    margin-top: 0;
  }

  .modal-input {
    width: 100%;
    padding: 0.5rem;
    margin-bottom: 1rem;
    background-color: var(--editor-bg);
    border: 1px solid #555;
    color: #ffffff;
    font-size: 0.875rem;
    outline: none;
    border-radius: 0.25rem;
    box-sizing: border-box;

    &:focus {
      border-color: var(--primary-blue);
    }
  }

  .modal-actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.5rem; 

    .btn-modal-cancel {
      background: none;
      border: none;
      cursor: pointer;
      padding: 0.25rem 0.75rem;
      font-size: 0.75rem;
      color: #9ca3af;
      transition: color 0.2s;

      &:hover {
        color: #ffffff;
      }
    }

    .btn-modal-apply {
      border: none;
      cursor: pointer;
      padding: 0.25rem 1rem;
      background-color: var(--primary-blue);
      color: #ffffff;
      font-size: 0.75rem;
      font-weight: 700;
      border-radius: 0.25rem;
      transition: background-color 0.2s;

      &:hover {
        background-color: #2563eb;
      }
    }
  }
}
</style>