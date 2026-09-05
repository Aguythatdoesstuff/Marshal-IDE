# Marshal IDE & Marshal Script

**Marshal** is a specialized development environment and domain-specific language (DSL) designed to streamline and modernize mod development for Hearts of Iron IV. Named after the highest rank of strategic command, Marshal gives developers high-level authority over their mod's systems, replacing tedious "bracket-counting" and manual file management with an automated, highly structured, and fun workflow.

---

## Core Components

* **Marshal IDE**: The main application environment used to build, organize, and manage mod projects, workspace states, and asset integration.
* **Marshal Script**: A high-level DSL (transpiled via a multi-pass transpiler) that abstracts complex Paradox scripting into a clean, tab-indented structure for Focuses, Events, Decisions, Ideas, Scripted GUIs, and more.

---

## Key Features

* **Blazing-Fast C# Compiler Stack**: Powered by a high-performance C# backend pipeline that maximizes transpilation velocity and minimizes resource overhead.
* **Deep Syntax Validation & Error Tracking**: Integrated syntactic error-checking catches malformed expressions, layout anomalies, and structural depth issues. Jump directly to offending code lines via the dedicated **Error Tab** in the console. (syntax validation does not validate hoi4 code that is being passed through, thats the game engines job)
* **Compiler Warning Tab**: A dedicated console tab that surfaces non-breaking compiler warnings and best-practice recommendations.
* **Multi-File Tabbed Interface & Session Persistence**: Open and switch between multiple scripts simultaneously. Tab layouts, active views, and filetree folder states automatically restore across sessions.
* **Indentation-Based Structure**: Uses clean tab indentation rather than curly brackets to define code blocks, eliminating missing-bracket syntax errors.
* **Intelligent Output Synchronization**: Automatically cleans up generated output files when source blocks or files are deleted, keeping build folders lean and Git-friendly.
* **Visual File Browser & Asset Import**: Color-coded file icons provide immediate spatial recognition. Drag-and-drop `.dds` images directly into the project sidebar to automatically route assets into GFX directories.
* **Instant GFX Importer**: One-click `.dds` integration automatically generates required `spriteTypes` and pathing logic—just reference the asset directly in code with `GFX_` prefixes.
* **Vanilla Mod Importer**: Built-in migration tool to effortlessly import existing vanilla Hearts of Iron IV mods into a Marshal workspace.
* **Implicit Localization**: Provide plain text inside quotes for names and descriptions; Marshal generates organized `.yml` files and localization keys automatically.
* **Advanced Scripted GUI**: High-level UI declarations for complex components (e.g., draggable windows, simplified horizontal progress bars) in a fraction of standard game code.
* **Modern Tailwind Frontend**: Built on Vue.js and Tailwind CSS for a lightweight, highly responsive, and fluid editor user interface.

---

## Getting Started

To get started, click the built-in **Wiki** button inside the app. If you haven't installed the IDE yet, navigate to the `docs` folder and open the **Getting Started** section to read more information.

> [!IMPORTANT]
> **Do not delete the `descriptor.mod`** created by the Paradox Launcher. The IDE does not generate or manage this file, and Hearts of Iron IV requires it to load your mod.

---

## Philosophy & Goals

* **Automation**: Eliminate repetitive setup tasks like manual GFX pathing, `spriteTypes` declarations, and output file cleanup.
* **Structure**: Enforce clean, consistent code structures across all major mod systems.
* **Workflow Integration**: Consolidate editing, transpiling, asset management, and error tracking into a unified interface.
* **Goal**: To make large-scale Hearts of Iron IV modding faster, less error-prone, easier to maintain, and significantly more enjoyable.

---

## Documentation & Guides

Access full documentation and syntax guides through the `docs` folder in your installation directory or by clicking the **Wiki** button inside the IDE application.

---

## License

All use of this software is subject to our license terms. Please read the **LICENSE.txt** file in the root directory for full details on usage and permissions.