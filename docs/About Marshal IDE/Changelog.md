# Changelog

All notable changes to the Marshal IDE and DSL will be documented in this file.

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

### Changed
- **UI Refresh**: Implemented a "Clean UI" philosophy to maximize screen real estate for code.
