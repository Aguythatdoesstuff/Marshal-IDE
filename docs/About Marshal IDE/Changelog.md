# Changelog

All notable changes to the Marshal IDE and DSL will be documented in this file.

## [1.1.0] - 2026-05-23

### Added
- **Tabbed Interface**: Added full support for multi-file workflows, allowing you to open, view, and switch between multiple scripts simultaneously.
- **Smart File Creation Wizard**: Rewrote the "New File" workflow. Instead of forcing automatic extensions, the IDE now features an extension dropdown menu. 
    - **Context-Aware Defaults**: Selecting a target folder automatically auto-selects the recommended compiler extension, safeguarding your build from syntax errors and preventing uncompilable "gibberish" output.
- **Isolated Language Architecture**: Successfully extracted over 300 lines of massive Regex-based syntax highlighters and configuration maps out of the main view tier into `../ide/components/hoi4/config.js`, laying the architectural groundwork to easily support future Paradox game engines down the line.
- **Visual File Browser**: Color-coded file icons (e.g., Green for Focuses, Red for Events) for better spatial recognition.
- **Version-Linked Update Modals**: Implement a "What's New" modal that automatically triggers upon the first launch of a new version to highlight key changes.
- Added a importer tool to be able to easily import vanilla Hoi4 mods into a Marshal IDE workspace.

### Changed
-- **Intelligent Sync Engine**: Optimized the workspace sync engine to more smartly detect which files were modified since the last session. This delivers a substantial reduction in workspace startup times, particularly beneficial for large mods and lower-end hardware.
- **Frontend Architecture Overhaul**: Fully refactored the frontend stack to use **Vue.js** and **sass**, paired with a comprehensive CSS audit and cleanup for drastically improved long-term codebase maintenance.
- **Optimized Monaco Core**: Improved and streamlined the Monaco Editor initialization process for better overall editor stability.
- **UX & Fluidity Enhancements**: Fine-tuned application responsiveness across the board to ensure animations, transitions, and interactions feel faster and more fluent.
- **Console Performance & Controls**: 
    - **Lag-Free Resizing**: Upgraded the console divider tracker; dragging the console height now matches your mouse movement instantly without stuttering.
    - **Clearer Visibility Toggles**: Added highly visible buttons to hide and unhide the console panel, while preserving the intuitive "drag to bottom to minimize" gesture.
- **Prominent Asset Importing**: Redesigned the "Import Img" placement and button to make core workspace importing actions instantly recognizable.

---

## [1.0.0] - 2026-03-27
### Added
- **Marshal IDE Core**: First stable release featuring a built-in Wiki, workspace selection, A andvanced proprietary logger with auto cleanup and archiving to keep the system, and optimized startup speeds.
- **Automated Output Synchronization (Deletion Handler)**: 
    - **Live Cleanup**: Deleting a file or code block within the IDE automatically removes the corresponding generated files in your mod output, preventing "ghost" files from bloating your build.
    - **Git-Friendly State Management**: The IDE tracks file states internally, ensuring that automated deletions are handled predictably for version control systems like Git.
- **Unified DSL**: Support for creating:
    - Decisions and Events
    - National Focuses
    - National Spirits and Ideas
    - Scripts (Game Rules, On Actions, Scripted Effects, and Triggers)
    - **Advanced Scripted GUI**: A high-level syntax for UI creation including `draggable window` blocks.
    - **Simplified Progress Bars**: Simplified `horizontal bar` instead of around 4 files with different syntax just do it in 1 clean one!.
- **Instant GFX Importer**: One-click `.dds` integration. The IDE automatically generates `spriteTypes` and handles pathing logic—just inside youre code just type in `GFX_` image name and go!.
- **Integrated Console**: Real-time logs from the compiler and IDE runtime. 
- **UI Refresh**: Implemented a "Clean UI" philosophy to maximize screen real estate for code.