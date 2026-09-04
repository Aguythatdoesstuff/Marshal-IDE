import { defineConfig, externalizeDepsPlugin } from 'electron-vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import tailwindcss from '@tailwindcss/vite'
import { viteStaticCopy } from 'vite-plugin-static-copy'

export default defineConfig({
  main: {
    plugins: [
      externalizeDepsPlugin(),
      viteStaticCopy({
        targets: [
          {
            // Mirrors the entire root structure directly into build/vite/main
            src: [
              '**/*',
              '!dist',
              '!out',
              '!node_modules',
              '!public',
              '!build',
              '!build/**/*',
              '!electron.vite.config.js',
              '!VS',           
              '!**/.*',         
              '!**/.vs/**/*',
              '!c#',               
              '!c#/**/*'        
            ],
            dest: '.' // Targets the main outDir directly
          }
        ]
      })
    ],
    build: {
      outDir: resolve(__dirname, 'build/vite/main'),
      bytecode: true,
      lib: { entry: resolve(__dirname, 'main.js') },
      rollupOptions: {
        output: {
          format: 'cjs',
          entryFileNames: 'index.cjs'
        }
      }
    }
  },
  preload: {
    plugins: [externalizeDepsPlugin()],
    build: {
      outDir: resolve(__dirname, 'build/vite/preload'),
      bytecode: true,
      lib: { entry: resolve(__dirname, 'src/preload.js') },
      rollupOptions: {
        output: {
          format: 'cjs',
          entryFileNames: 'index.cjs'
        }
      }
    }
  },
  renderer: {
    root: resolve(__dirname, 'src/renderer'),
    resolve: {
      alias: {
        '@': resolve(__dirname, 'src/renderer/src')
      }
    },
    plugins: [
      vue(),
      tailwindcss()
    ],
    build: {
      outDir: resolve(__dirname, 'build/vite/renderer'),
      rollupOptions: {
        input: {
          index: resolve(__dirname, 'src/renderer/index.html'),
        }
      }
    }
  }
})