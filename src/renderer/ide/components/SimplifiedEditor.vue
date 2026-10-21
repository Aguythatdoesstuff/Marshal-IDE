<script setup>
import { computed } from 'vue'
import DragDropEditor from './DragDropEditor.vue'

const props = defineProps({
  filePath: {
    type: String,
    required: true
  },
  content: {
    type: String,
    default: ''
  }
})

const emit = defineEmits(['update:content', 'switch-to-code'])

// Extract file extension to determine which editor module to load
const fileExtension = computed(() => {
  if (!props.filePath) return ''
  const lastDot = props.filePath.lastIndexOf('.')
  return lastDot !== -1 ? props.filePath.substring(lastDot).toLowerCase() : ''
})

// Map file extensions to their respective visual/simplified editor components
const currentEditorComponent = computed(() => {
  switch (fileExtension.value) {
    case '.script':
      return DragDropEditor
    default:
      return null
  }
})
</script>

<template>
  <div class="flex flex-col h-full w-full bg-marshal-editor text-marshal-text select-none">
    <component 
      v-if="currentEditorComponent"
      :is="currentEditorComponent"
      :file-path="filePath"
      :content="content"
      @update:content="(val) => emit('update:content', val)"
      @switch-to-code="emit('switch-to-code')"
    />
    <div v-else class="flex flex-col items-center justify-center h-full w-full p-6 text-center text-marshal-muted">
      <h2 class="text-base font-semibold text-white mb-2">No Simplified Editor Available</h2>
      <p class="text-xs mb-4">
        There is no registered visual or simplified editor for files with extension 
        <span class="font-mono text-marshal-primary">{{ fileExtension || 'none' }}</span>.
      </p>
    </div>
  </div>
</template>