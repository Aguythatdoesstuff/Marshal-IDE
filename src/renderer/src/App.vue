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

    <div v-else-if="currentScreen === 'IMPORTING'" class="importing-layer">
      <Importing />
    </div>

    <div v-if="showWhatsNewModal" class="whats-new-overlay">
      <div class="whats-new-modal">
        <div class="modal-header">
          <h2>What's New in v{{ appVersion }}</h2>
          <span class="version-badge">Latest Update</span>
        </div>
        <div class="modal-body">
          <p>Here's a quick look at the main features and technical enhancements in this update:</p>
          <div class="changelog-scrollable">
            
            <div class="changelog-section">
              <h3>Added</h3>
              <ul class="features-list">
                <li><strong>New Idea Category Support:</strong> Added compiler and importer support for the <code>hidden_ideas</code> / <code>hidden_idea</code> idea type to allow properly defining and parsing hidden national ideas. (Idea types are now dynamic and you are able to make custom idea types no problem and no validation except the basics)</li>
              </ul>
            </div>
            <div class="changelog-section section-fixed">
              <h3>Fixed</h3>
              <ul class="features-list">
                <li><strong>Compiler wide:</strong> Fixed the compiler not overriding the files in the output after first compilation and instead constantly appending lines (caused duplicate code).</li>
                <li><strong>Compiler wide:</strong> Standardized Unicode whitespace normalization (<code>\u00A0</code>, <code>\u202F</code>, etc.) to prevent hidden non-breaking spaces from breaking indentation checks and triggering false "Unknown root-level script header" errors.</li>
                <li><strong>Compiler wide:</strong> Fixed an indentation depth calculation bug by explicitly expanding literal tabs (<code>\t</code>) into 4 standard spaces at the start of line sanitization, preventing lines from incorrectly reading as depth 0 and breaking the block stack state machine.</li>
                <li><strong>ID Encoding &amp; Case Validation:</strong> Adjust compiler validation to allow alphanumeric characters, underscores, and periods (<code>.</code>) in focus/object IDs (e.g., <code>GER_cdu_2.0</code>). By default, permit full capitalization across all IDs. For IDs utilizing non-ASCII special characters/umlauts (e.g., <code>GER_grünes_zeitalte</code>), allow compilation but emit a warning regarding UTF-8 parsing and trigger risks.</li>
                <li><strong>Focus Compiler:</strong> Fixed the focus compiler outputting the prevents and requires blocks as id = some_focus_id instead of focus = some_focus_id</li>
                <li><strong>Compiler:</strong> Fixed compilers calculated final cost to be correct (final cost = -0.02)</li>
                <li><strong>Importer:</strong> Fixed importer outputting ID's incorrectly for national focuses and scripted gui GUI elements.</li>
                <li><strong>Importer:</strong> Fixed decision importing logic to correctly preserve and output cost priority values instead of saving raw unparsed line remnants.</li>
                <li><strong>Importer:</strong> Fixed national focus importing to dynamically track the country scope context and append the necessary <code>for [country id]</code> scoping identifiers.</li>
                <li><strong>Importer:</strong> Fixed importer not outputting x and y coordinates for all focuses.</li>
                <li><strong>Decision Icons &amp; Pictures Support:</strong> Update compiler and importer logic to correctly handle the two distinct asset types within decisions:
                  <ul>
                    <li><strong>Icon:</strong> The small graphic displayed to the left of the category or decision name. The engine handles this dynamically, so it does <em>not</em> strictly require the <code>GFX_</code> prefix.</li>
                    <li><strong>Picture:</strong> The larger graphic displayed to the left of a category's description. The engine is strict here, meaning it <em>requires</em> the <code>GFX_</code> prefix to resolve properly.</li>
                  </ul>
                </li>
                <li><strong>Required Actions:</strong> Update the compiler to explicitly support both the <code>icon</code> and <code>picture</code> syntax definitions, and ensure the importer accurately distinguishes between them (treating <code>icon</code> as prefix-optional and `picture` as prefix-mandatory) during processing.</li>
              </ul>
            </div>
          </div>
          
          <div v-if="updateIsReadyToInstall" class="update-prompt-box">
            <p><strong>An update is ready!</strong> Would you like to install and restart now to apply changes?</p>
            <div class="prompt-actions">
              <button @click="triggerAppUpdate" class="install-now-btn">Update Now</button>
              <button @click="updateIsReadyToInstall = false" class="defer-btn">Later</button>
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
import Importing from './Importing.vue'

export default {
  name: 'App',
  components: {
    Eula, 
    WorkspaceSelection,
    Wiki,
    Settings,
    Ide,
    Importing
  },
  data() {
    return {
      currentScreen: 'SPLASH',
      statusText: 'Starting Marshal IDE',
      logoUrl: logoIcon,
      showWhatsNewModal: false,
      pendingWhatsNew: false,
      appVersion: '',
      updateIsReadyToInstall: false
    }
  },
  async mounted() {
    // Listen for downloaded background updates ready for immediate execution
    if (window.api && window.api.on) {
      window.api.on('update-ready-interactive', () => {
        this.updateIsReadyToInstall = true;
        this.showWhatsNewModal = true; // Ensure they see the prompt container
      });
    }
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
    },
    async triggerAppUpdate() {
      if (window.api && window.api.invoke) {
        try {
          await window.api.invoke('execute-immediate-install');
        } catch (error) {
          console.error("Failed to invoke immediate installation routine:", error);
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
  width: 580px; /* Slightly wider to accommodate technical text */
  max-width: 90vw;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  padding: 25px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5);
  color: #e0e0e0;
  animation: modalSlideUp 0.3s ease-out;

  .modal-body {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 0; /* Prevents content from forcing the modal wrapper to overflow screen limits */
  }

  .changelog-scrollable {
    overflow-y: auto;
    padding-right: 12px;
    flex: 1; /* Dynamically shrinks or grows based on display height, keeping the footer locked inside */

    &::-webkit-scrollbar { width: 6px; }
    &::-webkit-scrollbar-thumb { background: #444; border-radius: 3px; }
    &::-webkit-scrollbar-thumb:hover { background: #555; }
  }

  .changelog-section {
    margin-bottom: 22px;
    
    h3 {
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 1.2px;
      color: #007acc; /* Added Section Color */
      margin: 0 0 12px 0;
      border-bottom: 1px solid #2d2d2d;
      padding-bottom: 6px;
    }

    &.section-fixed h3 { color: #d19a66; } /* Distinct color for Fixed */
    &.section-changed h3 { color: #98c379; } /* Distinct color for Changed */
  }

  .update-prompt-box {
    margin-top: 15px;
    padding: 15px;
    background-color: #142c42;
    border: 1px solid #007acc;
    border-radius: 6px;
    text-align: center;

    p {
      margin: 0 0 12px 0;
      font-size: 13px;
      color: #e0e0e0;
    }

    .prompt-actions {
      display: flex;
      justify-content: center;
      gap: 12px;

      .install-now-btn {
        background-color: #98c379;
        color: #1e1e1e;
        border: none;
        padding: 6px 16px;
        border-radius: 4px;
        font-weight: 600;
        cursor: pointer;
        font-size: 12.5px;
        &:hover { background-color: #82b065; }
      }

      .defer-btn {
        background-color: transparent;
        color: #aaa;
        border: 1px solid #444;
        padding: 6px 16px;
        border-radius: 4px;
        cursor: pointer;
        font-size: 12.5px;
        &:hover { color: #fff; border-color: #666; }
      }
    }
  }

  .features-list {
    list-style-type: none;
    padding: 0;
    margin: 0;

    li {
      position: relative;
      padding-left: 20px;
      margin-bottom: 14px;
      font-size: 13.5px;
      line-height: 1.5;
      color: #ccc;

      &::before {
        content: "•";
        position: absolute;
        left: 0;
        color: #555;
        font-size: 18px;
        top: -1px;
      }

      strong { color: #fff; font-weight: 600; }
      code { background: #2d2d2d; padding: 2px 4px; border-radius: 3px; font-family: monospace; font-size: 12px; color: #e06c75; }
    }
  }
  .modal-footer {
    display: flex;
    justify-content: center; /* Centers the button horizontally */
    margin-top: 28px;        /* Adds breathing room between the content list and the button */

    .dismiss-btn {
      background-color: #007acc;
      color: white;
      border: none;
      padding: 12px 28px;    /* Slightly expanded padding for cleaner proportions */
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