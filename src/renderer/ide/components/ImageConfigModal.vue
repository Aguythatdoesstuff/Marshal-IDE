<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
    <div class="w-full max-w-2xl bg-[#252526] border border-[#333333] rounded-lg shadow-2xl flex flex-col max-h-[90vh] text-[#bbbbbb]">
      
      <!-- Modal Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-[#333333]">
        <h2 class="text-lg font-semibold text-white">Configure Image Imports</h2>
        <button 
          @click="closeModal" 
          class="text-[#858585] hover:text-white transition-colors p-1 rounded hover:bg-[#333333]"
        >
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Modal Body (List of imported files to configure) -->
      <div class="p-6 overflow-y-auto space-y-6 flex-1">
        <div v-for="(file, index) in configuredFiles" :key="index" class="p-4 bg-[#1e1e1e] border border-[#333333] rounded-md space-y-4">
          
          <!-- File Path Display -->
          <div class="text-xs text-[#858585] truncate">
            Source: <span class="text-[#bbbbbb] font-mono">{{ file.originalPath }}</span>
          </div>

          <!-- Safe Name Configuration -->
          <div class="space-y-1">
            <label class="block text-xs font-medium text-white">Target Image Name</label>
            <input 
              type="text" 
              v-model="file.safeName" 
              @input="sanitizeName(index)"
              class="w-full bg-[#2d2d2d] border border-[#333333] rounded px-3 py-2 text-sm text-white focus:outline-none focus:border-[#007acc]"
              placeholder="e.g. my_game_texture"
            />
            <p class="text-[11px] text-[#858585]">
              This image will be normalized to a name safe for the game engine (lowercase, no spaces/special characters).
            </p>
          </div>

          <!-- Image Type Dropdown (Required) -->
          <div class="space-y-1">
            <label class="block text-xs font-medium text-white">What is this image for? <span class="text-red-400">*</span></label>
            <select 
              v-model="file.imageType"
              class="w-full bg-[#2d2d2d] border border-[#333333] rounded px-3 py-2 text-sm text-white focus:outline-none focus:border-[#007acc]"
            >
              <option value="" disabled>Select image type...</option>
              <option value="focus">Focus/Focus Tree</option>
            </select>
          </div>

        </div>
      </div>

      <!-- Modal Footer -->
      <div class="flex items-center justify-end px-6 py-4 border-t border-[#333333] space-x-3">
        <button 
          @click="closeModal"
          class="px-4 py-2 text-sm rounded bg-[#333333] hover:bg-[#3e3e42] text-white transition-colors"
        >
          Cancel
        </button>
        <button 
          @click="handleConfirmImport"
          :disabled="isSubmitting || !isFormValid"
          class="px-4 py-2 text-sm rounded bg-[#007acc] hover:bg-[#0098ff] text-white font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {{ isSubmitting ? 'Importing...' : 'Confirm & Import' }}
        </button>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const props = defineProps({
  isOpen: {
    type: Boolean,
    required: true
  },
  filePaths: {
    type: Array,
    required: true
  }
})

const emit = defineEmits(['update:isOpen', 'imported'])

const isSubmitting = ref(false)

// Local state mapping each file path to its configuration parameters
const configuredFiles = ref(
  props.filePaths.map(filePath => {
    const baseName = filePath.split('\\').pop().split('/').pop().replace(/\.[^/.]+$/, "")
    return {
      originalPath: filePath,
      safeName: sanitizeString(baseName),
      imageType: ''
    }
  })
)

function sanitizeString(str) {
  return str
    .toLowerCase()
    .replace(/[^a-z0-9_]/g, '_')
    .replace(/_+/g, '_')
}

function sanitizeName(index) {
  configuredFiles.value[index].safeName = sanitizeString(configuredFiles.value[index].safeName)
}

const isFormValid = computed(() => {
  if (configuredFiles.value.length === 0) return false
  return configuredFiles.value.every(file => file.safeName.trim() !== '' && file.imageType !== '')
})

function closeModal() {
  emit('update:isOpen', false)
}

async function handleConfirmImport() {
  if (!isFormValid.value) return
  isSubmitting.value = true

  try {
    // 1. Save configuration / update image cache via IPC
    const cachePayload = configuredFiles.value.map(file => ({
      path: file.originalPath,
      safeName: file.safeName,
      imageType: file.imageType
    }))

    const cacheResult = await window.electronAPI.invoke('update-img-cache', cachePayload)
    if (!cacheResult?.success) {
      throw new Error(cacheResult?.message || 'Failed to update image cache.')
    }

    // 2. Finalize import process via IPC
    const finalizeResult = await window.electronAPI.invoke('finalize-img-import', cachePayload)
    if (!finalizeResult?.success) {
      throw new Error(finalizeResult?.message || 'Failed to finalize image import.')
    }

    emit('imported', finalizeResult.imported)
    closeModal()
  } catch (error) {
    console.error('Import process failed:', error)
  } finally {
    isSubmitting.value = false
  }
}
</script>