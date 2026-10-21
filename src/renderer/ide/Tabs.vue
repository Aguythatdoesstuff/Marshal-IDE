<script setup>
import { ref, computed, watch, nextTick, markRaw, toRaw } from 'vue'
import { getMonacoLanguage } from './components/hoi4/config.js'

const props = defineProps({
  activeProjectName: {
    type: String,
    default: 'No Active Project'
  },
  editorInstance: {
    type: Object,
    default: null
  }
})

const emit = defineEmits(['update:active-tab'])

const openTabs = ref([])
const activeTabPath = ref(null)
let isSwitchingTab = false

const closeConfirmVisible = ref(false)
const pendingClosePath = ref(null)

const getWorkspaceTabsStorageKey = () => {
  const workspaceName = localStorage.getItem('marshal_project_to_load') || props.activeProjectName
  return `workspace_tabs_${workspaceName}`
}

const getDefaultEditorMode = (filePath) => {
  return 'monaco'
}

const saveTabState = (pathOverride = null) => {
  const activeEditor = props.editorInstance || window.editorInstance
  const targetPath = pathOverride || activeTabPath.value
  const activeTab = openTabs.value.find(t => t.path === targetPath)

  if (activeEditor && activeTab && activeEditor.getModel() && !activeEditor.getModel().isDisposed()) {
    if (activeEditor.getModel().uri.toString() === window.monaco?.Uri.file(targetPath).toString()) {
      const position = activeEditor.getPosition()
      activeTab.lineNumber = position?.lineNumber || activeTab.lineNumber || 1
      activeTab.scrollTop = activeEditor.getScrollTop()
    }
  }

  const payload = {
    activeTabPath: activeTabPath.value || '',
    tabs: openTabs.value.map(tab => ({
      path: tab.path,
      lineNumber: tab.lineNumber || 1,
      scrollTop: typeof tab.scrollTop === 'number' ? tab.scrollTop : 0,
      editorMode: tab.editorMode || getDefaultEditorMode(tab.path)
    }))
  }

  localStorage.setItem(getWorkspaceTabsStorageKey(), JSON.stringify(payload))
}

const setActiveTab = async (path) => {
  if (isSwitchingTab && activeTabPath.value === path) return

  const previousPath = activeTabPath.value
  if (previousPath) saveTabState(previousPath)

  isSwitchingTab = true
  activeTabPath.value = path
  emit('update:active-tab', path)
  
  const targetTab = openTabs.value.find(t => t.path === path)
  if (!targetTab) {
    isSwitchingTab = false
    return
  }

  await nextTick()

  const activeEditor = props.editorInstance || window.editorInstance
  const monacoGlobal = window.monaco

  if (activeEditor && monacoGlobal) {
    try {
      if (!targetTab.monacoModel || targetTab.monacoModel.isDisposed()) {
        const languageId = getMonacoLanguage(path)
        const modelUri = monacoGlobal.Uri.file(path)
        
        let modelInstance = monacoGlobal.editor.getModel(modelUri)
        
        if (!modelInstance || modelInstance.isDisposed()) {
          modelInstance = monacoGlobal.editor.createModel(targetTab.content, languageId, modelUri)
          modelInstance.onDidChangeContent(() => {
            const currentVal = modelInstance.getValue()
            if (targetTab.content !== currentVal) {
              targetTab.isDirty = true
            }
          })
        }
        
        targetTab.monacoModel = modelInstance ? markRaw(modelInstance) : null
      }

      const cleanNativeModel = toRaw(targetTab.monacoModel)
      if (cleanNativeModel && !cleanNativeModel.isDisposed()) {
        if (activeEditor.getModel() !== cleanNativeModel) {
          activeEditor.setModel(cleanNativeModel)
        }
        
        const currentLanguageId = getMonacoLanguage(path)
        const existingLang = cleanNativeModel.getLanguageId()
        if (existingLang !== currentLanguageId) {
          monacoGlobal.editor.setModelLanguage(cleanNativeModel, currentLanguageId)
        }
      }

      if (targetTab.lineNumber) {
        activeEditor.setPosition({ lineNumber: targetTab.lineNumber, column: 1 })
      }
      if (typeof targetTab.scrollTop === 'number') {
        activeEditor.setScrollTop(targetTab.scrollTop)
      }

    } catch (runtimeErr) {
      console.error('Error during tab activation:', runtimeErr)
    } finally {
      isSwitchingTab = false
      saveTabState(path)
    }
  } else {
    isSwitchingTab = false
  }
}

const preloadFileTab = async (filePath, lineNumber, scrollTop, savedEditorMode) => {
  try {
    let existingTab = openTabs.value.find(t => t.path === filePath)
    if (existingTab) return existingTab

    const res = await window.api.invoke('get-file-content', { filePath })
    if (res && (res.success !== false)) {
      const rawContent = typeof res === 'string' ? res : (res.content || '')
      const monacoGlobal = window.monaco
      let tabModel = null
      
      if (monacoGlobal) {
        const languageId = getMonacoLanguage(filePath)
        const modelUri = monacoGlobal.Uri.file(filePath)
        tabModel = monacoGlobal.editor.getModel(modelUri)
        
        if (!tabModel || tabModel.isDisposed()) {
          tabModel = monacoGlobal.editor.createModel(rawContent, languageId, modelUri)
          tabModel.onDidChangeContent(() => {
            const currentVal = tabModel.getValue()
            const tabItem = openTabs.value.find(t => t.path === filePath)
            if (tabItem && tabItem.content !== currentVal) {
              tabItem.isDirty = true
            }
          })
        }
      }

      const newTab = {
        name: filePath.split(/[\\/]/).pop(),
        path: filePath,
        content: rawContent,
        isDirty: false,
        lineNumber: lineNumber || 1,
        scrollTop: scrollTop || 0,
        editorMode: savedEditorMode || getDefaultEditorMode(filePath),
        monacoModel: tabModel ? markRaw(tabModel) : null 
      }
      openTabs.value.push(newTab)
      return newTab
    }
  } catch (err) {
    console.error('Failed to preload tab:', filePath, err)
  }
  return null
}

const openTab = async (node) => {
  if (!node || node.isDir) return
  try {
    let existingTab = openTabs.value.find(t => t.path === node.path)
    if (!existingTab) {
      const res = await window.api.invoke('get-file-content', { filePath: node.path })
      if (res && (res.success !== false)) {
        const rawContent = typeof res === 'string' ? res : (res.content || '')
        const monacoGlobal = window.monaco
        let tabModel = null
        
        if (monacoGlobal) {
          const languageId = getMonacoLanguage(node.path)
          const modelUri = monacoGlobal.Uri.file(node.path)
          tabModel = monacoGlobal.editor.getModel(modelUri)
          
          if (!tabModel || tabModel.isDisposed()) {
            tabModel = monacoGlobal.editor.createModel(rawContent, languageId, modelUri)
            tabModel.onDidChangeContent(() => {
              const currentVal = tabModel.getValue()
              const tabItem = openTabs.value.find(t => t.path === node.path)
              if (tabItem && tabItem.content !== currentVal) {
                tabItem.isDirty = true
              }
            })
          }
        }

        existingTab = {
          name: node.name,
          path: node.path,
          content: rawContent,
          isDirty: false,
          lineNumber: 1,
          scrollTop: 0,
          editorMode: getDefaultEditorMode(node.path),
          monacoModel: tabModel ? markRaw(tabModel) : null 
        }
        openTabs.value.push(existingTab)
      }
    }
    await setActiveTab(node.path)
    saveTabState()
  } catch (err) {
    console.error('Failed during file selection loading sequence:', err)
  }
}

const closeTab = (path) => {
  const tabIndex = openTabs.value.findIndex(t => t.path === path)
  if (tabIndex === -1) return
  const targetTab = openTabs.value[tabIndex]

  if (targetTab.isDirty) {
    pendingClosePath.value = path
    closeConfirmVisible.value = true
    return
  }
  executeForceCloseTab(path)
}

const executeForceCloseTab = (path) => {
  const tabIndex = openTabs.value.findIndex(t => t.path === path)
  if (tabIndex === -1) return

  const targetTab = openTabs.value[tabIndex]
  const activeEditor = props.editorInstance || window.editorInstance

  if (activeTabPath.value === path && activeEditor && window.monaco) {
    if (openTabs.value.length > 1) {
      const nextIndex = tabIndex === openTabs.value.length - 1 ? tabIndex - 1 : tabIndex + 1
      const nextTab = openTabs.value[nextIndex]
      if (nextTab && nextTab.monacoModel && !nextTab.monacoModel.isDisposed()) {
        activeEditor.setModel(toRaw(nextTab.monacoModel))
      } else {
        const emptyModel = window.monaco.editor.createModel('', 'plaintext')
        activeEditor.setModel(emptyModel)
      }
      activeTabPath.value = openTabs.value[nextIndex].path
      emit('update:active-tab', activeTabPath.value)
    } else {
      activeTabPath.value = null
      emit('update:active-tab', null)
      const emptyModel = window.monaco.editor.createModel('', 'plaintext')
      activeEditor.setModel(emptyModel)
    }
  }

  if (targetTab?.monacoModel?.dispose && !targetTab.monacoModel.isDisposed()) {
    targetTab.monacoModel.dispose()
  }

  openTabs.value.splice(tabIndex, 1)
  saveTabState()
}

const restoreTabState = async () => {
  const storedState = localStorage.getItem(getWorkspaceTabsStorageKey())
  if (!storedState) return

  try {
    const payload = JSON.parse(storedState)
    if (!Array.isArray(payload.tabs)) return

    const loadPromises = payload.tabs.map(tab => {
      if (tab?.path) return preloadFileTab(tab.path, tab.lineNumber, tab.scrollTop, tab.editorMode)
      return null
    })
    
    await Promise.all(loadPromises)

    if (payload.activeTabPath && openTabs.value.some(tab => tab.path === payload.activeTabPath)) {
      const activeTab = openTabs.value.find(tab => tab.path === payload.activeTabPath)
      await setActiveTab(payload.activeTabPath)
      const activeEditor = props.editorInstance || window.editorInstance
      if (activeEditor && activeTab) {
        if (activeTab.lineNumber) activeEditor.setPosition({ lineNumber: activeTab.lineNumber, column: 1 })
        if (typeof activeTab.scrollTop === 'number') activeEditor.setScrollTop(activeTab.scrollTop)
      }
    }
  } catch (err) {
    console.warn('Failed to restore workspace tab state:', err)
  }
}

const activeTabObj = computed(() => openTabs.value.find(t => t.path === activeTabPath.value))
const activeTabEditorMode = computed(() => activeTabObj.value ? (activeTabObj.value.editorMode || getDefaultEditorMode(activeTabPath.value)) : 'monaco')
const activeTabContent = computed(() => activeTabObj.value ? activeTabObj.value.content : '')

const toggleActiveEditorView = () => {
  if (!activeTabObj.value) return
  activeTabObj.value.editorMode = activeTabObj.value.editorMode === 'dragdrop' ? 'monaco' : 'dragdrop'
  saveTabState()
  if (activeTabObj.value.editorMode === 'monaco') {
    nextTick(() => setActiveTab(activeTabObj.value.path))
  }
}

const updateActiveTabContent = (newContent) => {
  if (!activeTabObj.value) return
  activeTabObj.value.content = newContent
  activeTabObj.value.isDirty = true
  if (activeTabObj.value.monacoModel && !activeTabObj.value.monacoModel.isDisposed()) {
    activeTabObj.value.monacoModel.setValue(newContent)
  }
}

const switchToCodeView = () => {
  if (activeTabObj.value) {
    setTimeout(() => {
      activeTabObj.value.editorMode = 'monaco'
      saveTabState()
      nextTick(() => setActiveTab(activeTabObj.value.path))
    }, 1500)
  }
}

defineExpose({
  openTabs,
  activeTabPath,
  activeTabObj,
  activeTabEditorMode,
  activeTabContent,
  openTab,
  closeTab,
  setActiveTab,
  saveTabState,
  restoreTabState,
  toggleActiveEditorView,
  updateActiveTabContent,
  switchToCodeView
})
</script>

<template>
  <div v-if="openTabs.length > 0" class="flex h-[35px] shrink-0 overflow-x-auto overflow-y-hidden border-b border-marshal-border bg-black/15">
    <div 
      v-for="tab in openTabs" 
      :key="tab.path" 
      :class="['inline-flex h-full shrink-0 cursor-pointer select-none items-center border-r border-marshal-border bg-black/10 px-3.5 text-xs text-marshal-muted transition hover:bg-white/[0.02] hover:text-marshal-text', { 'border-t-2 border-t-marshal-primary bg-marshal-editor font-medium text-white': activeTabPath === tab.path }]"
      @click="setActiveTab(tab.path)"
    >
      <span class="max-w-[120px] truncate">{{ tab.name }}</span>
      <span v-if="tab.isDirty" class="ml-1.5 text-[10px] text-rose-600">●</span>
      <span class="ml-2 flex h-3.5 w-3.5 items-center justify-center rounded text-sm hover:bg-white/10 hover:text-white" @click.stop="closeTab(tab.path)">×</span>
    </div>
  </div>

  <div class="fixed inset-0 z-[200] flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm" v-if="closeConfirmVisible" @keydown.esc="pendingClosePath = null; closeConfirmVisible = false;" tabindex="-1">
    <div class="w-full max-w-sm rounded-md border border-marshal-border bg-marshal-card p-6 shadow-2xl">
      <h3 class="mb-2 text-[15px] font-semibold text-white">Unsaved Document Changes</h3>
      <p class="mb-4 text-[13px] leading-relaxed text-marshal-muted">
        This file contains unsaved changes. Would you like to synchronize changes to disk storage before closing it?
      </p>
      <div class="mt-4 flex justify-end gap-2">
        <button class="rounded border border-marshal-border bg-white/5 px-3.5 py-1.5 text-xs font-semibold text-marshal-text hover:bg-white/10" @click="pendingClosePath = null; closeConfirmVisible = false;">Cancel</button>
        <button class="rounded border border-red-500 bg-red-500/15 px-3.5 py-1.5 text-xs font-semibold text-red-400 hover:bg-red-500 hover:text-white" @click="() => { const p = pendingClosePath; pendingClosePath = null; closeConfirmVisible = false; const t = openTabs.find(x => x.path === p); if(t) t.isDirty = false; executeForceCloseTab(p); }">Discard Changes</button>
        <button class="rounded bg-marshal-primary px-3.5 py-1.5 text-xs font-semibold text-white hover:bg-blue-600" @click="async () => { await setActiveTab(pendingClosePath); const p = pendingClosePath; pendingClosePath = null; closeConfirmVisible = false; executeForceCloseTab(p); }">Save & Close</button>
      </div>
    </div>
  </div>
</template>