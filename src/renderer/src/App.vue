<template>
  <div id="app-wrapper" class="h-screen w-screen font-sans text-marshal-text">
    <div v-if="currentScreen === 'SPLASH'" class="flex h-screen flex-col items-center justify-center [app-region:drag]">
      <div class="flex flex-col items-center gap-[50px]">
        <div class="h-60 w-60 animate-heartbeat-star bg-contain bg-center bg-no-repeat" :style="{ backgroundImage: `url(${logoUrl})` }"></div>
        <div class="flex gap-[15px] [app-region:no-drag]">
          <div class="h-3.5 w-3.5 animate-dot-fade rounded-full bg-gray-700"></div>
          <div class="h-3.5 w-3.5 animate-dot-fade rounded-full bg-gray-700 [animation-delay:200ms]"></div>
          <div class="h-3.5 w-3.5 animate-dot-fade rounded-full bg-gray-700 [animation-delay:400ms]"></div>
        </div>
        <div class="select-none text-lg font-semibold uppercase tracking-[3px] text-marshal-muted opacity-80">{{ statusText }}</div>
      </div>
    </div>

    <Eula v-else-if="currentScreen === 'EULA'" />
    <WorkspaceSelection v-else-if="currentScreen === 'WORKSPACE'" />
    <Wiki v-else-if="currentScreen === 'WIKI'" />
    <Settings v-else-if="currentScreen === 'SETTINGS'" />
    <Ide v-else-if="currentScreen === 'IDE'" />
    <Importing v-else-if="currentScreen === 'IMPORTING'" />

    <!-- What's New Modal -->
    <div v-if="showWhatsNewModal" class="fixed inset-0 z-[9999] flex items-center justify-center bg-black/75 p-4 backdrop-blur [app-region:no-drag]">
      <div class="flex max-h-[85vh] w-full max-w-2xl flex-col rounded-lg border border-gray-700 bg-marshal-editor p-6 text-gray-200 shadow-2xl animate-modal-slide-up">
        <div class="flex items-center justify-between gap-4">
          <h2 class="text-lg font-semibold text-white">What's New in v{{ appVersion }}</h2>
          <span class="rounded-full bg-marshal-primary/20 px-2.5 py-1 text-xs font-semibold text-marshal-primary">Latest Update</span>
        </div>

        <div class="mt-2 flex min-h-0 flex-1 flex-col">
          <p class="mb-4 text-sm text-gray-300">Here's a quick look at the main features and technical enhancements in this update:</p>
          
          <div class="min-h-0 flex-1 overflow-y-auto pr-3 space-y-6">
            <!-- Added Section -->
            <div>
              <h3 class="mb-3 border-b border-gray-800 pb-1.5 text-xs font-bold uppercase tracking-widest text-marshal-primary">Added</h3>
              <ul class="list-disc space-y-2.5 pl-5 text-sm leading-relaxed text-gray-300 marker:text-gray-600">
                <li>
                  <strong class="font-semibold text-white">Country Support:</strong> Dedicated DSL for country creation and modding 
                  <a href="https://hoi4.paradoxwikis.com/Country_creation" target="_blank" rel="noopener noreferrer" class="ml-1 inline-flex items-center text-xs text-sky-400 hover:underline">(Wiki Reference)</a>
                </li>
                <li>
                  <strong class="font-semibold text-white">Unit Support:</strong> Dedicated DSL for Unit modding 
                  <a href="https://hoi4.paradoxwikis.com/Unit_modding" target="_blank" rel="noopener noreferrer" class="ml-1 inline-flex items-center text-xs text-sky-400 hover:underline">(Wiki Reference)</a>
                </li>
                <li>
                  <strong class="font-semibold text-white">Equipment Support:</strong> Dedicated DSL for Equipment modding 
                  <a href="https://hoi4.paradoxwikis.com/Equipment_modding" target="_blank" rel="noopener noreferrer" class="ml-1 inline-flex items-center text-xs text-sky-400 hover:underline">(Wiki Reference)</a>
                </li>
                <li>
                  <strong class="font-semibold text-white">OOB Support:</strong> Dedicated DSL for Order of Battle modding 
                  <a href="https://hoi4.paradoxwikis.com/Division_modding" target="_blank" rel="noopener noreferrer" class="ml-1 inline-flex items-center text-xs text-sky-400 hover:underline">(Wiki Reference)</a>
                </li>
                <li>
                  <strong class="font-semibold text-white">Syntax Highlighting:</strong>
                  <ul class="mt-1.5 list-circle space-y-1 pl-5 text-gray-300">
                    <li>Added syntax highlighting for History DSL (Country support)</li>
                    <li>Added syntax highlighting for Equipment DSL</li>
                    <li>Added syntax highlighting for OOB (Order of Battle) DSL</li>
                    <li>Added syntax highlighting for Unit DSL</li>
                  </ul>
                </li>
                <li>
                  <strong class="font-semibold text-white">Documentation:</strong>
                  <p class="mt-0.5 text-gray-300">Added Getting Started documentation for Country, Unit, Equipment, and OOB modding DSLs.</p>
                </li>
              </ul>
            </div>

            <!-- Changed Section -->
            <div>
              <h3 class="mb-3 border-b border-gray-800 pb-1.5 text-xs font-bold uppercase tracking-widest text-indigo-400">Changed</h3>
              <ul class="list-disc space-y-2.5 pl-5 text-sm leading-relaxed text-gray-300 marker:text-gray-600">
                <li>
                  <strong class="font-semibold text-white">What's New Modal:</strong> Updated release notes content for the current version.
                </li>
              </ul>
            </div>

            <!-- Fixed Section -->
            <div>
              <h3 class="mb-3 border-b border-gray-800 pb-1.5 text-xs font-bold uppercase tracking-widest text-orange-400">Fixed</h3>
              <ul class="list-disc space-y-2.5 pl-5 text-sm leading-relaxed text-gray-300 marker:text-gray-600">
                <li>
                  <strong class="font-semibold text-white">Event Compiler:</strong> Resolved an off-by-one state leak bug where event options were shifting into subsequent events or duplicating. Added explicit state flushing on event boundaries to ensure open option blocks are committed to their parent event before transitioning to new events or namespaces.
                </li>
                <li>
                  <strong class="font-semibold text-white">Validator:</strong> Prevented incorrect depth errors after single-line pass-through syntax for the DSL syntax in specific cases.
                </li>
              </ul>
            </div>
          </div>

          <!-- Update Prompt -->
          <div v-if="updateIsReadyToInstall" class="mt-4 rounded-md border border-marshal-primary bg-sky-950/50 p-4 text-center">
            <p class="mb-3 text-sm"><strong>An update is ready!</strong> Would you like to install and restart now?</p>
            <div class="flex justify-center gap-3">
              <button @click="triggerAppUpdate" class="rounded bg-emerald-400 px-4 py-1.5 text-xs font-semibold text-gray-900 transition hover:bg-emerald-300">Update Now</button>
              <button @click="updateIsReadyToInstall = false" class="rounded border border-gray-700 px-4 py-1.5 text-xs text-gray-400 transition hover:border-gray-500 hover:text-white">Later</button>
            </div>
          </div>
        </div>

        <div class="mt-6 flex justify-center">
          <button @click="closeWhatsNew" class="rounded bg-marshal-primary px-7 py-2.5 text-sm font-semibold text-white transition hover:bg-blue-700">Got it, let's explore!</button>
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
import DragDropEditor from '../ide/components/DragDropEditor.vue';
import SimplifiedEditor from '../ide/components/SimplifiedEditor.vue';

export default {
  name: 'App',
  components: { Eula, WorkspaceSelection, Wiki, Settings, Ide, Importing, DragDropEditor, SimplifiedEditor },
  data() {
    return { currentScreen: 'SPLASH', statusText: 'Starting Marshal IDE', logoUrl: logoIcon, showWhatsNewModal: false, pendingWhatsNew: false, appVersion: '', updateIsReadyToInstall: false }
  },
  async mounted() {
    if (window.api && window.api.on) window.api.on('update-ready-interactive', () => { this.updateIsReadyToInstall = true; this.showWhatsNewModal = true })
    if (localStorage.getItem('FORCE_SCREEN') === 'WIKI') { this.currentScreen = 'WIKI'; localStorage.removeItem('FORCE_SCREEN'); return }
    if (!window.api || !window.api.invoke) { console.error('Electron IPC bridge is not available.'); this.currentScreen = 'EULA'; return }
    try {
      const bootState = await window.api.invoke('get-boot-state')
      this.statusText = 'Loading Environment...'
      this.appVersion = bootState.currentVersion || '1.0.0'
      if (bootState.enforceEula) { this.currentScreen = 'EULA'; this.pendingWhatsNew = bootState.showWhatsNew }
      else { this.currentScreen = 'WORKSPACE'; this.showWhatsNewModal = bootState.showWhatsNew }
    } catch (error) { console.error('Failed to fetch boot state:', error); this.currentScreen = 'EULA' }
    window.api.on('eula-accepted-success', () => { this.currentScreen = 'WORKSPACE'; if (this.pendingWhatsNew) { this.showWhatsNewModal = true; this.pendingWhatsNew = false } })
    window.api.on('navigate-to', targetScreen => { if (targetScreen) this.currentScreen = targetScreen.toUpperCase() })
  },
  methods: {
    async closeWhatsNew() { this.showWhatsNewModal = false; if (window.api && window.api.invoke) { try { await window.api.invoke('dismiss-whats-new') } catch (error) { console.error("Failed to dismiss What's New modal:", error) } } },
    async triggerAppUpdate() { if (window.api && window.api.invoke) { try { await window.api.invoke('execute-immediate-install') } catch (error) { console.error('Failed to invoke immediate installation routine:', error) } } }
  }
}
</script>
