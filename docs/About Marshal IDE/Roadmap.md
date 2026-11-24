# Future Updates & Roadmap

This document outlines the planned trajectory for Marshal IDE. Note: Features marked as "Researching" are experimental.

## [1.6.0] - Importer Expansion & Editor Memory Optimization

### Importer Support (v1.4.0 DSL Parity)
- **Unit Importer Support**: Import existing HOI4 Unit modding files into the dedicated Unit DSL.
- **Equipment Importer Support**: Import existing HOI4 Equipment modding files into the dedicated Equipment DSL.
- **OOB Importer Support**: Import existing HOI4 Order of Battle (OOB) files into the dedicated OOB DSL.
- **Country Importer Support**: Import existing HOI4 Country history/creation files into the dedicated Country DSL.
- **Importer Architecture & Stability**:
  - Refactor importer modules to maintain clean separation of concerns and prevent monolithic parser/mapper growth as DSL coverage expands.
  - Resolve edge-case parsing and conversion bugs uncovered during multi-DSL import testing.

### Editor Tabs & Memory Management
- **Image Tab LRU Cap**: Enforce a strict maximum of 5 concurrently loaded full-resolution image preview tabs using Least Recently Used (LRU) eviction to prevent JavaScript heap out-of-memory crashes.
- **Background Tab Idle Unload Timer**: Automatically unload background tabs after 10 minutes of inactivity to keep renderer memory usage bounded during long sessions.
- **Workspace Non-Breaking Cache Cleanup**:
  - Add a **Delete Non-Breaking Cache** action inside the per-workspace settings in the workspace selection menu.
  - Clear non-essential cached state (such as saved open tab history and generated image caches) while preserving critical workspace configuration files (`config.json`).

---
## 🔭 Long-term Research & Development
- **Workflow Tools**:
    - Integrated Git GUI for version control.
    - Drag & Drop Code Editor (visual node-based or block-based script editing).

- **Visual Workspace Personalization:** Add a dedicated Themes & Appearance tab in Global Application Settings. Users can now customize the IDE environment with selectable accent colors and define custom token colors for syntax highlighting.

