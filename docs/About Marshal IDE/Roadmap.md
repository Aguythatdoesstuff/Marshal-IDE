# Future Updates & Roadmap

This document outlines the planned trajectory for Marshal IDE. Note: Features marked as "Researching" are experimental.

## 🛠 v1.1.0 - The "Quality of Life" Update
### DSL Expansions
- **MIO Support**: Dedicated DSL for Military Industrial Organizations.
- **Country Definitions**: Streamlined syntax for defining new nations and tags.

### Back end DSL Improvements
- **Unified Compiler Bootstrapping**: Implement a standardized initialization function across all compilers to eliminate redundant code for startup sequences and environment setup.
- **Centralized Import Logic**: Refactor the compiler core to use a single, shared module for handling imports, ensuring consistency and reducing maintenance overhead.
- **Architectural Refactoring**: Streamline the back-end by consolidating recurring compiler patterns into a reusable, DRY (Don't Repeat Yourself) framework.

### IDE Architecture & Code Splitting
- **Monaco Configuration Decoupling**: Extract `defineDslLanguages()`, `initMonaco()`, and all associated Monarch tokens/regex rules from `ide_renderer.js` into a dedicated `editor_config.js` or `monaco_setup.js`.
- **Syntax Highlighting Isolation**: Move massive Regex-based syntax highlight definitions (currently 300+ lines) into standalone file or module files to keep the renderer focused purely on the UI state.
- **Provider Modularization**: Separate and improve Autocomplete and Suggestion providers into a `providers/` directory to ensure `ide_renderer.js` remains maintainable as DSL complexity grows.

### IDE & UX Improvements
- **Tabbed Interface**: Support for opening multiple files simultaneously.
- **Visual File Browser**: Color-coded file icons (e.g., Green for Focuses, Red for Events) for better spatial recognition.
- **Task Feedback**: Integrated loading bars for long-running processes.
- **Refined Highlighting**: Context-aware syntax highlighting to fix "leakage" (e.g., ensuring `visible` only highlights within appropriate files).

### Automated User Notifications & Changelog
- **Version-Linked Update Modals**: Implement a "What's New" modal that automatically triggers upon the first launch of a new version to highlight key changes.
- **Smart "Dismiss" Logic**: Add a "Never Show Again" toggle that suppresses the modal for the current session; logic is designed to auto-reset on the next version update to ensure users never miss critical patch notes.
- **Lazy-User Accessibility**: Bridge the gap between the `CHANGELOG.md` and the end-user by surfacing the most relevant updates directly in the IDE UI.

### CSS & Performance
- Full CSS audit and cleanup for better maintnance of the IDE.

---

## 🔭 Long-term Research & Development
- **Asset Suite**: 
    - Built-in `.dds` image previewer.
    - Integrated JPEG/PNG to `.dds` converter.
- **Workflow Tools**:
    - Integrated Git GUI for version control.
    - Legacy Mod Importer to convert vanilla-style code into Marshal DSL.

    
    
## 🛠 v1.2.0 – Expansion Update (TBD)
