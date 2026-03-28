import { defineConfig, externalizeDepsPlugin } from 'electron-vite'
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
              '!./electron.vite.config.js'
            ],
            dest: '.' 
          }
        ]
      })
    ],
    build: {
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
    root: resolve(__dirname, 'public'),
    build: {
      outDir: resolve(__dirname, 'out/renderer'),
      rollupOptions: {
        input: {
          index: resolve(__dirname, 'public/index.html'),
          eula: resolve(__dirname, 'public/eula.html'),
          ide: resolve(__dirname, 'public/ide.html'),
          settings: resolve(__dirname, 'public/settings.html'),
          wiki: resolve(__dirname, 'public/wiki.html'),
          loading: resolve(__dirname, 'public/loading.html')
        }
      }
    }
  }
})
