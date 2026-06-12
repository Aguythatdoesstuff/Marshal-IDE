# Marshal IDE & Marshal Script

**Marshal** is a high-performance development environment and domain-specific language (DSL) designed to revolutionize mod development for Hearts of Iron IV. By moving away from tedious "bracket-counting" and manual file management, Marshal gives mod developers high-level command over their mod's systems through a modern, streamlined workflow.

---

## Key Features

* **Blazing-Fast C# Compiler Stack**: Powered by a completely refactored C# backend that maximizes compilation velocity, minimizes resource overhead, and delivers deep syntactic error-checking.
* **Deep Syntax Validation & Error Tracking**: Integrated validation passes catch malformed expressions, layout anomalies, and structural depth issues immediately. Find problems instantly via the dedicated **Error Tab** in the integrated console.(Note: they may sometimes be incorrect however unlikley)
* **Multi-File Tabbed Interface**: Boost your productivity with a full multi-file workflow. Open, view, and seamlessly switch between multiple scripts simultaneously.
* **The Power of Tabs**: Uses clean indentation rather than messy curly brackets to define structure, completely eliminating "missing bracket" errors.
* **Intelligent Output Synchronization**: The IDE internally tracks file states for Git-friendly management. Deleting a file or code block automatically cleans up corresponding generated files, while an optimized sync engine ensures lightning-fast workspace startup times.
* **Visual File Browser**: Navigate your project effortlessly with color-coded file icons (e.g., Green for Focuses, Red for Events) built for quick spatial recognition.
* **Smart File Creation Wizard**: A folder-aware file creation system featuring extension dropdowns that automatically recommend the proper compiler extension based on your target folder.
* **Instant GFX Importer**: Redesigned, prominent one-click `.dds` integration. Drop your assets in, and Marshal automatically handles pathing logic and generates the required `spriteTypes`.
* **Vanilla Mod Importer**: Features a dedicated migration tool to effortlessly import existing vanilla Hearts of Iron IV mods straight into a Marshal IDE workspace.
* **Implicit Localization**: Provide text directly in quotes for names and descriptions; Marshal automatically generates the organized `.yml` files and localization keys for you.
* **Advanced Scripted GUI**: High-level syntax for UI creation, supporting complex components like draggable windows and simplified progress bars in a fraction of the code.

---

## Getting Started

To begin using Marshal, follow these core steps:

1. **Create or Import a Mod**: Use the Paradox Launcher to create a standard mod folder, or use the built-in **Importer Tool** to port an existing vanilla mod.
2. **Setup Workspace**: Open Marshal IDE and create a workspace to house your Marshal source files.
3. **Set Output Path**: Point your workspace settings to your target Hearts of Iron IV mod folder.
4. **Source is King**: Always edit inside your Marshal Workspace; the IDE will automatically manage, compile, and synchronize the generated output files for you.

> See the *Getting Started/Setup Mod* guide inside the `docs` folder or click the built-in **Wiki** button in the app for more detailed information.

> [!IMPORTANT]
> **Do not delete the `descriptor.mod`** created by the Paradox Launcher. The IDE does not edit or generate this file, and the game will not recognize your mod without it.

---

## Documentation & Guides

Learn more about the software and its specific syntax systems through our comprehensive documentation. Guides can be accessed directly via the `docs` folder in your installation directory or by clicking the **Wiki** button inside the IDE application.

---

## License:

All use of this software is subject to our license terms. Please read the **LISENCE.txt** file in the root directory for full details on usage and permissions.
