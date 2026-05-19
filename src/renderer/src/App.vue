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
  </div>
</template>

<script>
import logoIcon from '../../../build/icon.png'
import Eula from './Eula.vue'
import WorkspaceSelection from './WorkspaceSelection.vue'
import Wiki from './wiki.vue'

export default {
  name: 'App',
  components: {
    Eula, 
    WorkspaceSelection,
    Wiki
  },
  data() {
    return {
      currentScreen: 'SPLASH',
      statusText: 'Starting Marshal IDE',
      logoUrl: logoIcon // FIX: Registered here so the template can read it!
    }
  },
  async mounted() {
    // Check if loading a standalone browser-exported snapshot layout
    if (localStorage.getItem('FORCE_SCREEN') === 'WIKI') {
      this.currentScreen = 'WIKI';
      localStorage.removeItem('FORCE_SCREEN'); // Clear flag cleanly
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

      if (bootState.enforceEula) {
        this.currentScreen = 'EULA';
      } else {
        this.currentScreen = 'WORKSPACE';
      }
    } catch (error) {
      console.error("Failed to fetch boot state:", error);
      this.currentScreen = 'EULA';
    }

    window.api.on('eula-accepted-success', () => {
      this.currentScreen = 'WORKSPACE';
    });

    window.api.on('navigate-to', (targetScreen) => {
      if (targetScreen) {
        this.currentScreen = targetScreen; 
      }
    });
  }
}
</script>

<style lang="scss">
body, html {
  margin: 0;
  padding: 0;
  height: 100vh;
  overflow: hidden;
  background-color: #1e1e1e;
}

#app-wrapper {
  height: 100vh;
  width: 100vw;
  color: #cccccc;
  font-family: 'Inter', 'Segoe UI', sans-serif;
}

.splash-layer {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  height: 100vh;
  -webkit-app-region: drag;

  .container {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 50px; 
  }

  .logo {
    width: 240px;  
    height: 240px;
    
    /* FIX: Explicitly separated layout properties */
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
    -webkit-app-region: no-drag;
  }

  .dot {
    width: 14px;  
    height: 14px; 
    background-color: #444;
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
    color: #858585;
    opacity: 0.8;
    user-select: none; 
    -webkit-user-select: none;
  }
}

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
    background-color: #007acc; 
  }
}
</style>