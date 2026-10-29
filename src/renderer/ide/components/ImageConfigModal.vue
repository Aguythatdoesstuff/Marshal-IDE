<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
    <div class="w-full max-w-2xl bg-[#252526] border border-[#333333] rounded-lg shadow-2xl flex flex-col max-h-[90vh] text-[#bbbbbb] relative">
      
      <!-- Loading Overlay -->
      <div v-if="isSubmitting" class="absolute inset-0 z-20 bg-[#252526]/95 backdrop-blur-xs flex flex-col items-center justify-center space-y-3 rounded-lg">
        <svg class="animate-spin h-8 w-8 text-[#007acc]" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
        <span class="text-sm font-medium text-white tracking-wide">{{ isConfigUpdate ? 'Saving Changes...' : 'Importing...' }}</span>
      </div>

      <!-- Modal Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-[#333333]">
        <h2 class="text-lg font-semibold text-white">{{ isConfigUpdate ? 'Configure Image' : 'Configure Image Imports' }}</h2>
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
            <div class="relative">
              <input 
                type="text" 
                v-model="file.safeName" 
                :disabled="isConfigUpdate"
                @input="handleNameInput(index, $event)"
                class="w-full bg-[#2d2d2d] border border-[#333333] rounded px-3 py-2 text-sm text-white focus:outline-none focus:border-[#007acc] disabled:opacity-50 disabled:cursor-not-allowed"
                placeholder="e.g. my_game_texture"
              />
              <transition name="fade">
                <div v-if="file.showCharWarning" class="absolute right-2 top-1/2 -translate-y-1/2 bg-red-500/90 text-white text-[11px] px-2.5 py-1 rounded shadow-lg pointer-events-none flex items-center gap-1.5 backdrop-blur-sm">
                  <svg class="w-3.5 h-3.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                  </svg>
                  <span>Only a-z, 0-9, and _ allowed</span>
                </div>
              </transition>
            </div>
            <p class="text-[11px] text-[#858585]">
              {{ isConfigUpdate ? 'Image name cannot be changed here. Use file rename context options instead.' : 'This image will be normalized to a name safe for the game engine (lowercase, no spaces/special characters).' }}
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
              <option value="ideas">Ideas/National spirits</option>
              <option value="flag">Country Flag</option>
              <option value="equipment">Equipment / Technology</option>
              <option value="event">Event Picture</option>
              <option value="fallback">Standard / Fallback (Uncompressed dds)</option>
            </select>
          </div>

          <!-- Extra Conditional Information Fields (Misc info loaded & updated when triggered from ide.vue) -->
          <transition name="fade">
            <!-- Country Flag Extra Info -->
            <div v-if="file.imageType === 'flag'" class="space-y-1 pl-3 border-l-2 border-[#007acc]">
              <label class="block text-xs font-medium text-white">Country Tag <span class="text-red-400">*</span></label>
              <input 
                type="text" 
                v-model="file.countryTag"
                class="w-full bg-[#2d2d2d] border border-[#333333] rounded px-3 py-2 text-sm text-white focus:outline-none focus:border-[#007acc]"
                placeholder="e.g. GER, USA, FRA"
                maxlength="3"
                @input="file.countryTag = file.countryTag.toUpperCase()"
              />
              <p class="text-[11px] text-[#858585]">3-letter tag of the country this flag belongs to.</p>
            </div>

            <!-- Equipment Extra Info -->
            <div v-else-if="file.imageType === 'equipment'" class="space-y-1 pl-3 border-l-2 border-[#007acc]">
              <label class="block text-xs font-medium text-white">Equipment ID <span class="text-red-400">*</span></label>
              <input 
                type="text" 
                v-model="file.equipmentId"
                class="w-full bg-[#2d2d2d] border border-[#333333] rounded px-3 py-2 text-sm text-white focus:outline-none focus:border-[#007acc]"
                placeholder="e.g. infantry_weapons_1"
              />
              <p class="text-[11px] text-[#858585]">Equipment or technology ID this image belongs to.</p>
            </div>
          </transition>

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
          {{ isSubmitting ? (isConfigUpdate ? 'Saving Changes...' : 'Importing...') : (isConfigUpdate ? 'Save Changes' : 'Confirm & Import') }}
        </button>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'

const props = defineProps({
  isOpen: {
    type: Boolean,
    required: true
  },
  filePaths: {
    type: Array,
    required: true
  },
  isConfigUpdate: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['update:isOpen', 'imported', 'saved'])

const isSubmitting = ref(false)
const configuredFiles = ref([])

watch(() => [props.filePaths, props.isOpen], async ([newPaths]) => {
  if (newPaths && newPaths.length > 0 && props.isOpen) {
    const files = []
    for (const filePath of newPaths) {
      let safeName = ''
      let imageType = ''
      let countryTag = ''
      let equipmentId = ''
      let miscProps = {}

      // When triggered via Update Image Type in ide.vue (isConfigUpdate = true), load existing config & misc info
      if (props.isConfigUpdate && window.api && window.api.invoke) {
        try {
          const res = await window.api.invoke('get-img-config', filePath)
          if (res && res.success && res.config) {
            safeName = res.config.safeName || ''
            imageType = res.config.imageType || ''
            countryTag = res.config.countryTag || ''
            equipmentId = res.config.equipmentId || ''
            
            // Capture any other misc properties stored in config.json
            const excludedKeys = ['safeName', 'imageType', 'importedFrom', 'updatedAt', 'thumbnail', 'imagePreviews', 'countryTag', 'equipmentId']
            for (const [k, v] of Object.entries(res.config)) {
              if (!excludedKeys.includes(k)) {
                miscProps[k] = v
              }
            }
          }
        } catch (err) {
          console.warn('[ImageConfigModal] Failed to fetch existing image config:', err)
        }
      }

      if (!safeName) {
        const baseName = filePath.split('\\').pop().split('/').pop().replace(/\.[^/.]+$/, "")
        safeName = sanitizeString(baseName)
      }

      files.push({
        originalPath: filePath,
        safeName,
        imageType,
        countryTag,
        equipmentId,
        ...miscProps,
        showCharWarning: false,
        warningTimeout: null
      })
    }
    configuredFiles.value = files
  }
}, { immediate: true })

function sanitizeString(str) {
  return str
    .normalize('NFD') // Decompose accented characters (e.g. é -> e + ´)
    .replace(/[\u0300-\u036f]/g, '') // Strip diacritic marks
    .toLowerCase()
    .replace(/[^a-z0-9_]/g, '_') // Replace anything not a-z, 0-9, or underscore with an underscore
    .replace(/_+/g, '_') // Collapse consecutive underscores into a single one
}

function sanitizeName(index) {
  configuredFiles.value[index].safeName = sanitizeString(configuredFiles.value[index].safeName)
}

function handleNameInput(index, event) {
  const rawValue = event.target.value;
  if (/[^a-zA-Z0-9_]/.test(rawValue)) {
    const fileItem = configuredFiles.value[index];
    fileItem.showCharWarning = true;
    if (fileItem.warningTimeout) clearTimeout(fileItem.warningTimeout);
    fileItem.warningTimeout = setTimeout(() => {
      if (fileItem) fileItem.showCharWarning = false;
    }, 2500);
  }
  sanitizeName(index);
}

const isFormValid = computed(() => {
  if (configuredFiles.value.length === 0) return false
  return configuredFiles.value.every(file => {
    if (!file.safeName.trim() || !file.imageType) return false
    if (file.imageType === 'flag' && !file.countryTag?.trim()) return false
    if (file.imageType === 'equipment' && !file.equipmentId?.trim()) return false
    return true
  })
})

function closeModal() {
  emit('update:isOpen', false)
}

async function handleConfirmImport() {
  if (!isFormValid.value) return
  isSubmitting.value = true

  try {
    const payload = configuredFiles.value.map(file => {
      const { originalPath, safeName, imageType, countryTag, equipmentId, showCharWarning, warningTimeout, ...misc } = file
      const item = {
        path: originalPath,
        safeName,
        imageType,
        ...misc
      }
      if (imageType === 'flag') {
        item.countryTag = countryTag.trim().toUpperCase()
      } else if (imageType === 'equipment') {
        item.equipmentId = equipmentId.trim()
      }
      return item
    })

    if (props.isConfigUpdate) {
      const updateResult = await window.api.invoke('update-img-config', payload)
      if (!updateResult?.success) {
        throw new Error(updateResult?.message || 'Failed to update image configuration.')
      }

      for (const file of configuredFiles.value) {
        await window.api.invoke('compile-file', {
          filePath: file.originalPath,
          outputOptions: undefined
        })
      }

      emit('saved', payload)
    } else {
      const cacheResult = await window.api.invoke('update-img-cache', payload)
      if (!cacheResult?.success) {
        throw new Error(cacheResult?.message || 'Failed to update image cache.')
      }

      const finalizeResult = await window.api.invoke('finalize-img-import', payload)
      if (!finalizeResult?.success) {
        throw new Error(finalizeResult?.message || 'Failed to finalize image import.')
      }

      emit('imported', finalizeResult.imported)
    }

    closeModal()
  } catch (error) {
    console.error('Operation failed:', error)
  } finally {
    isSubmitting.value = false
  }
}
</script>