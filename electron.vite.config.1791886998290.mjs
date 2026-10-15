// electron.vite.config.js
import { defineConfig, externalizeDepsPlugin } from "electron-vite";
import vue from "@vitejs/plugin-vue";
import { resolve } from "path";
import tailwindcss from "@tailwindcss/vite";
import { viteStaticCopy } from "vite-plugin-static-copy";
var __electron_vite_injected_dirname = "C:\\Users\\Kacper\\Desktop\\Projects\\Marshal-IDE";
var electron_vite_config_default = defineConfig({
  main: {
    plugins: [
      externalizeDepsPlugin(),
      viteStaticCopy({
        targets: [
          {
            // Mirrors the entire root structure directly into build/vite/main
            src: [
              "**/*",
              "!dist",
              "!out",
              "!node_modules",
              "!public",
              "!build",
              "!build/**/*",
              "!electron.vite.config.js",
              "!VS",
              "!**/.*",
              "!**/.vs/**/*",
              "!c#",
              "!c#/**/*"
            ],
            dest: "."
            // Targets the main outDir directly
          }
        ]
      })
    ],
    build: {
      sourcemap: true,
      outDir: resolve(__electron_vite_injected_dirname, "build/vite/main"),
      bytecode: true,
      lib: { entry: resolve(__electron_vite_injected_dirname, "main.js") },
      rollupOptions: {
        output: {
          format: "cjs",
          entryFileNames: "index.cjs"
        }
      }
    }
  },
  preload: {
    plugins: [externalizeDepsPlugin()],
    build: {
      outDir: resolve(__electron_vite_injected_dirname, "build/vite/preload"),
      bytecode: true,
      lib: { entry: resolve(__electron_vite_injected_dirname, "src/preload.js") },
      rollupOptions: {
        output: {
          format: "cjs",
          entryFileNames: "index.cjs"
        }
      }
    }
  },
  renderer: {
    root: resolve(__electron_vite_injected_dirname, "src/renderer"),
    resolve: {
      alias: {
        "@": resolve(__electron_vite_injected_dirname, "src/renderer/src")
      }
    },
    plugins: [
      vue(),
      tailwindcss()
    ],
    build: {
      outDir: resolve(__electron_vite_injected_dirname, "build/vite/renderer"),
      rollupOptions: {
        input: {
          index: resolve(__electron_vite_injected_dirname, "src/renderer/index.html")
        }
      }
    }
  }
});
export {
  electron_vite_config_default as default
};
