# Marshal IDE & Marshal Script

**Marshal** is a specialized development environment and domain-specific language (DSL) designed to streamline and modernize mod development for Hearts of Iron IV. Named after the highest rank of strategic command, Marshal gives developers high-level authority over their mod's systems, replacing tedious "bracket-counting" and manual file management with an automated, highly structured, and fun workflow.

---

## Core Components

* **Marshal IDE**: The main application environment used to build, organize, and manage mod projects, workspace states, and asset integration.
* **Marshal Script**: A high-level DSL (transpiled via a multi-pass transpiler) that abstracts complex Paradox scripting into a clean, tab-indented structure for Focuses, Events, Decisions, Ideas, Scripted GUIs, and more.

---

## Key Features

* **Blazing-Fast C# Compiler Stack**: Powered by a high-performance C# backend pipeline that maximizes transpilation velocity and minimizes resource overhead.
* **Deep Syntax Validation & Error Tracking**: Integrated syntactic error-checking catches malformed expressions, layout anomalies, and structural depth issues. Jump directly to offending code lines via the dedicated **Error Tab** in the console. (Syntax validation does not validate HoI4 code that is being passed through; that's the game engine's job.)
* **Compiler Warning Tab**: A dedicated console tab that surfaces non-breaking compiler warnings and best-practice recommendations.
* **Multi-File Tabbed Interface & Session Persistence**: Open and switch between multiple scripts simultaneously. Tab layouts, active views, and filetree folder states automatically restore across sessions.
* **Indentation-Based Structure**: Uses clean tab indentation rather than curly brackets to define code blocks, eliminating missing-bracket syntax errors.
* **Intelligent Output Synchronization**: Automatically cleans up generated output files when source blocks or files are deleted, keeping build folders lean and Git-friendly.
* **Refactored Smart Image Importer**:
  * **Wide Format Support**: Import virtually any image format, including `.dds`, `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tga`, `.webp`, `.tiff`, `.tif`, and `.hdr`.
  * **Interactive Type Config Modal**: Specify image types (e.g., set `PLACEHOLDER.png` as an `idea` image type) and custom attributes (such as equipment IDs or country tags) before importing. Every selected image has its intended role explicitly defined.
  * **Automated Asset & Prefix Handling**: The compiler automatically compresses and converts images into the game engine's preferred format while injecting required prefixes (e.g., `idea_`) and formatting output tailored to the image type.
  * **Image Type Editing**: Need to fix an assignment? Simply right-click (light click) an image and select **Update Image Type**. *(Note: Direct renaming is currently disabled to prevent metadata loss from file watchers mistaking renames for deletions. To rename, delete and reimport under a new name, which enforces safe naming rules like using underscores instead of spaces).*
  * **Filetree Previews & Lightbox View**: Thumbnail image previews appear directly within the project filetree. Clicking an image opens a full-size preview modal for effortless spatial recognition.
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