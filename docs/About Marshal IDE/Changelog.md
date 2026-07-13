# Changelog

All notable changes to the Marshal IDE and DSL will be documented in this file.

## [1.2.2] - 2026-07-13
### Fixed
- **Compiler wide:** - Fixed the compiler not overriding the files in the output after first compilation and instead constantly appending lines (caused duplicate code).
  - Standardized Unicode whitespace normalization (`\u00A0`, `\u202F`, etc.) to prevent hidden non-breaking spaces from breaking indentation checks and triggering false "Unknown root-level script header" errors.
  - Fixed an indentation depth calculation bug by explicitly expanding literal tabs (`\t`) into 4 standard spaces at the start of line sanitization, preventing lines from incorrectly reading as depth 0 and breaking the block stack state machine.
- **Focus Compiler:** Fixed the focus compiler outputting the prevents and requires blocks as id = some_focus_id instead of focus = some_focus_id
- **compiler:** Fixed compilers calculated final cost to be correct (final cost = -0.02)
- **Importer:** Fixed importer outputting ID's incorrectly for national focuses and scripted gui GUI elements
  - Fixed decision importing logic to correctly preserve and output cost priority values instead of saving raw unparsed line remnants.
  - Fixed national focus importing to dynamically track the country scope context and append the necessary `for [country id]` scoping identifiers.

## [1.2.1] - 2026-06-15

### Added
- **Update Management**: Added available update modal.

### Fixed
- **Focus Localization**: Fixed spelling mistake in error message if the time unit in a focus are incorrect.
- **Focus Tree Parser**: Fixed the focus tree parser parsing the position coordinates incorrectly.


## [1.2.0] - 2026-06-13

### Added
- **Console**: Added Error tab in the console showing all validation errors caught by compiler helping you find syntax errors and such as much as it can
- **Compiler Syntax Validation**: Integrated deep syntactic error-checking directly into the compilation pass to catch malformed expressions, structural depth issues, and layout anomalies out of the gate.

### Fixed
- **Monaco Rendering Lifecycle**: Fixed an unhandled rendering exception (`Cannot read properties of undefined (reading 'domNode')`) caused by a race condition where global text editor references (`window.editorInstance`) persisted as stale, disposed instances across workspace switches, leading subsequent views to bypass proper initialization and render layers onto a dead layout container.
- **Window Event Memory Leaks**: Fixed a memory leak where inline arrow functions for the `resize` window event listener could not be correctly targeted by `removeEventListener`, causing layout cycles to execute operations on dead elements after a component unmount.
- **Logger Timing Accuracy**: Fixed an issue where buffered startup logs and cross-environment streams lost timing fidelity; the logger now accurately preserves micro-delta clocks (+0.00ms) and absolute chronological timestamps regardless of initialization wait sequences.
- **Navigation Wrapper Lifecycle**: Fixed a bug where clicking "Exit Workspace" forced an full window refresh that reset the app's internal boot initialization state, erroneously dropping users back onto the EULA acceptance screen instead of the workspace selection menu.
- **Syntax highlights**: Fixed multi line comments not being highlighted.
- **Console Layout:** Fixed the processing console panel expanding infinitely.(inside the importing page)

### Changed
- **Compiler Stack Refactor**: Completely refactored the pipeline from Node.js into a high-performance C# backend, maximizing compilation velocity, dropping resource overhead, and establishing a strictly modular architecture that is easier to maintain and expand.
- **Unified Logger Interceptor**: Moved the console interception engine directly into the core logger module, ensuring all standard Node environment outputs are automatically captured across the entire backend lifecycle without manual code setup in the main process thread.


## [1.1.0] - 2026-05-26

### Added
- **Tabbed Interface**: Added full support for multi-file workflows, allowing you to open, view, and switch between multiple scripts simultaneously.
- **Smart File Creation Wizard**: Rewrote the "New File" workflow. Instead of forcing automatic extensions, the IDE now features an extension dropdown menu. 
    - **Context-Aware Defaults**: Selecting a target folder automatically auto-selects the recommended compiler extension, safeguarding your build from syntax errors and preventing uncompilable "gibberish" output.
- **Isolated Language Architecture**: Successfully extracted over 300 lines of massive Regex-based syntax highlighters and configuration maps out of the main view tier into `../ide/components/hoi4/config.js`, laying the architectural groundwork to easily support future Paradox game engines down the line.
- **Visual File Browser**: Color-coded file icons (e.g., Green for Focuses, Red for Events) for better spatial recognition.
- **Version-Linked Update Modals**: Implement a "What's New" modal that automatically triggers upon the first launch of a new version to highlight key changes.
- Added a importer tool to be able to easily import vanilla Hoi4 mods into a Marshal IDE workspace.

### Changed
- **Intelligent Sync Engine**: Optimized the workspace sync engine to more smartly detect which files were modified since the last session. This delivers a substantial reduction in workspace startup times, particularly beneficial for large mods and lower-end hardware.
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
