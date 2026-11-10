# Future Updates & Roadmap

This document outlines the planned trajectory for Marshal IDE. Note: Features marked as "Researching" are experimental.

## 🛠 v1.5.1 – Stability and Bug Fixes
### Major Bug Fix
    - **Watcher and Compiler Close Orchestration**: 
        - Fix a bug that causes archiving of the current session to fail and the compiler to not close at all(causing ghost processes) when in a workspace and closing the entire app. This issue was due to the watcher and compiler not properly disconnecting and closing. Refractoring the way the watcher and compiler disconnect/close to ensure a clean shutdown sequence.
### Code Cleanup & Architecture
    - **Main Process De-bloating (`main.js`)**: 
        - Refactor and modularize IPC handler registration away from `main.js` into dedicated domain modules to reduce file bloat and improve maintainability.
### Reliability & Error Handling
    - **Fatal Error Watchdog (Sidecar Process)**:
        - Introduced a lightweight, native C# companion watchdog process spawned directly by the main application at startup. It runs independently to stream application `stderr` into a dedicated `watchdog.log` file within the metadata directory. Upon detecting a fatal crash, it copies the log to the desktop as an accessible crash report and triggers a native OS notification as a bulletproof fallback layer.
---
## 🔭 Long-term Research & Development
- **Workflow Tools**:
    - Integrated Git GUI for version control.
    - Drag & Drop Code Editor (visual node-based or block-based script editing).

- **Visual Workspace Personalization:** Add a dedicated Themes & Appearance tab in Global Application Settings. Users can now customize the IDE environment with selectable accent colors and define custom token colors for syntax highlighting.

