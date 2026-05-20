import { defineConfig, externalizeDepsPlugin } from 'electron-vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import { viteStaticCopy } from 'vite-plugin-static-copy'

export default defineConfig({
  main: {
    plugins: [
      externalizeDepsPlugin(),
      viteStaticCopy({
        targets: [
          {
            // Mirror the root structure into out/main
            src: [
              './**/*',
              '!./dist',
              '!./out',
              '!./node_modules',
              '!./public',
              '!./build',
              '!./build/**/*',
              '!./electron.vite.config.js',
              '!./VS',           
              '!./**/.*',         
              '!./**/.vs/**/*'  
            ],
            dest: '.' 
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
    css: {
      preprocessorOptions: {
        scss: {
          api: 'modern-compiler',
          additionalData: `@use "@/assets/_variables" as *;\n`
        }
      }
    },
    plugins: [vue()],
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