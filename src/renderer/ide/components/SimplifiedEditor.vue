<script setup>
import { computed, watch } from 'vue'
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

const SUPPORTED_EDITOR_VIEWS = {
  '.script': DragDropEditor,
}

const fileExtension = computed(() => {
  if (!props.filePath) return ''
  return '.' + props.filePath.split('.').pop().toLowerCase()
})

const resolvedEditorComponent = computed(() => {
  return SUPPORTED_EDITOR_VIEWS[fileExtension.value] || null
})

// Safely watch for unsupported components instead of executing side-effects in computed getters
watch(resolvedEditorComponent, (component) => {
  if (!component) {
    console.log(`Unsupported file format for simplified editor: "${props.filePath}". Routing back to Monaco code editor.`)
    emit('switch-to-code')
  }
}, { immediate: true })
</script>

<template>
  <div class="flex flex-col h-full w-full bg-slate-900 text-slate-100 overflow-hidden">
    <component 
      :is="resolvedEditorComponent" 
      v-if="resolvedEditorComponent"
      :file-path="filePath"
      :content="content"
      @update:content="(val) => emit('update:content', val)"
    />
    <div v-else class="flex flex-1 items-center justify-center p-6 text-center text-slate-400">
      <p>Unsupported file for simplified editor, Redirecting to main code editor...</p>
    </div>
  </div>
</template>