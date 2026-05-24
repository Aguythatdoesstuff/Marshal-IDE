<template>
  <div id="app-wrapper">
    <div v-if="currentScreen === 'SPLASH'" class="splash-layer">
      <div class="container">
        <div class="logo" :style="{ backgroundImage: `url(${logoUrl})` }"></div>
        <div class="dots">
          <div class="dot"></div>
          <div class="dot"></div>
          <div class="dot"></div>
        </div>
        <div class="status-text">{{ statusText }}</div>
      </div>
    </div>

    <div v-else-if="currentScreen === 'EULA'" class="eula-layer">
      <Eula />
    </div>

    <div v-else-if="currentScreen === 'WORKSPACE'" class="workspace-layer">
      <WorkspaceSelection />
    </div>

    <div v-else-if="currentScreen === 'WIKI'" class="wiki-layer">
      <Wiki />
    </div>

    <div v-else-if="currentScreen === 'SETTINGS'" class="settings-layer">
      <Settings />
    </div>

    <div v-else-if="currentScreen === 'IDE'" class="ide-layer">
      <Ide />
    </div>

    <div v-if="showWhatsNewModal" class="whats-new-overlay">
      <div class="whats-new-modal">
        <div class="modal-header">
          <h2>What's New in v{{ appVersion }}</h2>
          <span class="version-badge">Latest Update</span>
        </div>
        <div class="modal-body">
          <p>Here's a quick look at the main features and enhancements added to Marshal IDE:</p>
          <div class="changelog-scrollable">
            
            <div class="changelog-section">
              <h3>Added</h3>
              <ul class="features-list">
                <li><strong>Tabbed Interface:</strong> Added full support for multi-file workflows, allowing you to open, view, and switch between multiple scripts simultaneously.</li>
                <li><strong>Smart File Creation Wizard:</strong> Rewrote the "New File" workflow. Instead of forcing automatic extensions, the IDE now features an extension dropdown menu with <strong>Context-Aware Defaults</strong> (selecting a folder auto-selects the recommended compiler extension).</li>
                <li><strong>Isolated Language Architecture:</strong> Extracted over 300 lines of massive Regex-based syntax highlighters out of the main view tier into config files, laying the groundwork to support future Paradox game engines.</li>
                <li><strong>Visual File Browser:</strong> Color-coded file icons (e.g., Green for Focuses, Red for Events) for better spatial recognition.</li>
              </ul>
            </div>

            <div class="changelog-section">
              <h3>Changed</h3>
              <ul class="features-list">
                <li><strong>Frontend Architecture Overhaul:</strong> Fully refactored the frontend stack to use <strong>Vue.js</strong> and <strong>Sass</strong>, paired with a CSS audit and cleanup for drastically improved codebase maintenance.</li>
                <li><strong>Optimized Monaco Core:</strong> Improved and streamlined the Monaco Editor initialization process for better overall editor stability.</li>
                <li><strong>UX & Fluidity Enhancements:</strong> Fine-tuned responsiveness across the board to ensure animations, transitions, and interactions feel faster and more fluent.</li>
                <li><strong>Console Performance & Controls:</strong> Features lag-free resizing with instant mouse movement mapping alongside highly visible buttons to show/hide the panel.</li>
                <li><strong>Prominent Asset Importing:</strong> Redesigned the "Import Img" placement and button to make core workspace actions instantly recognizable.</li>
              </ul>
            </div>

            <div class="changelog-section">
              <h3>Performance & Tools</h3>
              <ul class="features-list">
                <li><strong>Intelligent Sync Engine:</strong> Optimizations to intelligently choose what files were modified since the last session, substantially reducing workspace start times for large mods on lower-end PCs.</li>
                <li><strong>Mod Importer Tool:</strong> Added an importer tool to easily migrate vanilla HoI4 mods into a Marshal IDE workspace.</li>
                <li><strong>Version-Linked Modals:</strong> This "What's New" window will now automatically alert you to major updates upon first launch!</li>
              </ul>
            </div>

          </div>
        </div>
        <div class="modal-footer">
          <button @click="closeWhatsNew" class="dismiss-btn">Got it, let's explore!</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import logoIcon from '../../../build/icon.png'
import Eula from './Eula.vue'
import WorkspaceSelection from './WorkspaceSelection.vue'
import Wiki from './wiki.vue'
import Settings from './settings.vue'
import Ide from './ide.vue'

export default {
  name: 'App',
  components: {
    Eula, 
    WorkspaceSelection,
    Wiki,
    Settings,
    Ide
  },
  data() {
    return {
      currentScreen: 'SPLASH',
      statusText: 'Starting Marshal IDE',
      logoUrl: logoIcon,
      showWhatsNewModal: false,
      pendingWhatsNew: false,
      appVersion: ''
    }
  },
  async mounted() {
    if (localStorage.getItem('FORCE_SCREEN') === 'WIKI') {
      this.currentScreen = 'WIKI';
      localStorage.removeItem('FORCE_SCREEN');
      return;
    }
    
    if (!window.api || !window.api.invoke) {
      console.error("Electron IPC bridge is not available.");
      this.currentScreen = 'EULA';
      return;
    }

    try {
      const bootState = await window.api.invoke('get-boot-state');
      this.statusText = 'Loading Environment...';
      this.appVersion = bootState.currentVersion || '1.0.0';

      if (bootState.enforceEula) {
        this.currentScreen = 'EULA';
        this.pendingWhatsNew = bootState.showWhatsNew;
      } else {
        this.currentScreen = 'WORKSPACE';
        this.showWhatsNewModal = bootState.showWhatsNew;
      }
    } catch (error) {
      console.error("Failed to fetch boot state:", error);
      this.currentScreen = 'EULA';
    }

    window.api.on('eula-accepted-success', () => {
      this.currentScreen = 'WORKSPACE';
      if (this.pendingWhatsNew) {
        this.showWhatsNewModal = true;
        this.pendingWhatsNew = false;
      }
    });

    // Automatically sanitize incoming router payloads to uppercase
    window.api.on('navigate-to', (targetScreen) => {
      if (targetScreen) {
        this.currentScreen = targetScreen.toUpperCase(); 
      }
    });
  },
  methods: {
    async closeWhatsNew() {
      this.showWhatsNewModal = false;
      if (window.api && window.api.invoke) {
        try {
          await window.api.invoke('dismiss-whats-new');
        } catch (error) {
          console.error("Failed to dismiss What's New modal:", error);
        }
      }
    }
  }
}
</script>

<style lang="scss">
/* ==========================================================================
   GLOBAL APP WINDOW LAYER (Unscoped)
   Forces the Electron shell window to adopt the user's theme color instantly
   ========================================================================== */
body, html {
  margin: 0;
  padding: 0;
  height: 100vh;
  overflow: hidden;
  background-color: var(--editor-bg); /* Uses dynamic user custom theme */
}
</style>

<style scoped lang="scss">
/* ==========================================================================
   SPLASH SCREEN & APP SHELL VIEW (Scoped)
   ========================================================================== */
#app-wrapper {
  height: 100vh;
  width: 100vw;
  color: $text-color; /* Linked to theme token */
  font-family: 'Inter', 'Segoe UI', sans-serif;
}

.splash-layer {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  height: 100vh;
  -webkit-app-region: drag; /* Allows window dragging on the splash screen background */

  .container {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 50px; 
  }

  .logo {
    width: 240px;  
    height: 240px;
    background-repeat: no-repeat;
    background-position: center center;
    background-size: contain;
    
    will-change: transform;
    transform: translateZ(0);
    backface-visibility: hidden;

    animation: heartbeat-star 1.2s infinite ease-in-out;
  }

  .dots {
    display: flex;
    gap: 15px;
    -webkit-app-region: no-drag; /* Prevents dragging from locking up dot interactions */
  }

  .dot {
    width: 14px;  
    height: 14px; 
    background-color: #444; /* Standard static off-state dark neutral */
    border-radius: 50%;
    animation: dotFade 1.6s infinite;

    &:nth-child(2) { animation-delay: 0.2s; }
    &:nth-child(3) { animation-delay: 0.4s; }
  }

  .status-text {
    font-size: 18px; 
    font-weight: 600;
    letter-spacing: 3px;
    text-transform: uppercase;
    color: $text-muted; /* Linked to theme token */
    opacity: 0.8;
    user-select: none; 
    -webkit-user-select: none;
  }
}

/* ==========================================================================
   WHAT'S NEW MODAL OVERLAY
   ========================================================================== */
.whats-new-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background-color: rgba(0, 0, 0, 0.75);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 9999;
  backdrop-filter: blur(4px);
  -webkit-app-region: no-drag;
}

.whats-new-modal {
  background-color: #1e1e1e;
  border: 1px solid #333;
  border-radius: 8px;
  width: 550px;
  max-width: 90vw;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  padding: 25px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5);
  color: #e0e0e0;
  animation: modalSlideUp 0.3s ease-out;

  .changelog-scrollable {
    overflow-y: auto;
    padding-right: 8px;
    max-height: 50vh;

    &::-webkit-scrollbar {
      width: 6px;
    }
    &::-webkit-scrollbar-thumb {
      background: #444;
      border-radius: 3px;
    }
    &::-webkit-scrollbar-thumb:hover {
      background: #555;
    }
  }

  .changelog-section {
    margin-bottom: 20px;
    
    h3 {
      font-size: 13px;
      text-transform: uppercase;
      letter-spacing: 1px;
      color: #007acc;
      margin: 0 0 10px 0;
      border-bottom: 1px solid #2d2d2d;
      padding-bottom: 4px;
    }
  }

  .modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid #333;
    padding-bottom: 15px;
    margin-bottom: 15px;

    h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 600;
      color: #fff;
    }

    .version-badge {
      background-color: #107c10;
      color: #fff;
      padding: 4px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: bold;
      text-transform: uppercase;
      letter-spacing: 1px;
    }
  }

  .modal-body {
    p {
      font-size: 14px;
      line-height: 1.5;
      color: #aaa;
      margin-bottom: 20px;
    }

    .features-list {
      list-style-type: none;
      padding: 0;
      margin: 0 0 25px 0;

      li {
        position: relative;
        padding-left: 20px;
        margin-bottom: 12px;
        font-size: 14px;
        line-height: 1.4;

        &::before {
          content: "•";
          position: absolute;
          left: 0;
          color: #007acc;
          font-size: 18px;
          top: -2px;
        }

        strong {
          color: #fff;
        }
      }
    }
  }

  .modal-footer {
    display: flex;
    justify-content: flex-end;

    .dismiss-btn {
      background-color: #007acc;
      color: white;
      border: none;
      padding: 10px 20px;
      border-radius: 4px;
      cursor: pointer;
      font-weight: 600;
      font-size: 14px;
      transition: background-color 0.2s;

      &:hover {
        background-color: #0062a3;
      }
    }
  }
}

@keyframes modalSlideUp {
  from {
    transform: translateY(20px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

/* ==========================================================================
   ANIMATIONS & KEYFRAMES
   ========================================================================== */
@keyframes heartbeat-star {
  0% {
    transform: scale(1) translateZ(0);
    filter: drop-shadow(0 10px 20px rgba(0,0,0,0.4)) brightness(1);
  }
  14% {
    transform: scale(1.06) translateZ(0);
    filter: drop-shadow(0 15px 30px rgba(255, 215, 0, 0.2)) brightness(1.2);
  }
  28% {
    transform: scale(1) translateZ(0);
  }
  42% {
    transform: scale(1.09) translateZ(0);
    filter: drop-shadow(0 20px 40px rgba(255, 215, 0, 0.3)) brightness(1.3);
  }
  70%, 100% {
    transform: scale(1) translateZ(0);
    filter: drop-shadow(0 10px 20px rgba(0,0,0,0.4)) brightness(1);
  }
}

@keyframes dotFade {
  0%, 100% { 
    opacity: 1; 
    transform: scale(1); 
    background-color: #444;
  }
  50% { 
    opacity: 1; 
    transform: scale(1.3); 
    background-color: $primary-blue; /* Pulse flash matches custom primary brand accent */
  }
}
</style>