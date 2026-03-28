# Marshal IDE & Marshal Script

**Marshal** is a specialized development environment and domain-specific language (DSL) designed to simplify mod development for Hearts of Iron IV. By moving away from "bracket-counting" and manual file management, Marshal gives mod developers high-level command over their mod's systems.

## Key Features:

* **Unified DSL**: Support for creating Events, Decisions, National Focuses, Spirits, and Scripted GUIs in a single, clean syntax.
* **The Power of Tabs**: Uses indentation rather than curly brackets to define structure, eliminating "missing bracket" errors.
* **Instant GFX Importer**: One-click .dds integration that automatically generates spriteTypes and handles pathing logic.
* **Automated Output Synchronization**: Deleting a file or code block in the IDE automatically removes the corresponding generated files in your mod output.
* **Implicit Localization**: Provide text directly in quotes for names and descriptions; Marshal automatically generates the .yml files and keys for you.
* **Advanced Scripted GUI**: A high-level syntax for UI creation, including draggable windows and simplified progress bars.

## Getting Started:

To begin using Marshal, follow these core steps:

1. **Create a Mod**: Use the Paradox Launcher to create a standard mod folder.
2. **Setup Workspace**: Open Marshal IDE and create a workspace to house your Marshal source files.
3. **Set Output Path**: Point your workspace settings to the mod folder created by the launcher.
4. **Source is King**: Always edit inside your Marshal Workspace; the IDE will manage the generated output for you.

> See Getting started/Setup Mod inside docs or built in wiki for more infromation

> [!IMPORTANT]
> **Do not delete the descriptor.mod** created by the Paradox Launcher. The IDE cannot currently edit or generate this file, and the game will not recognize your mod without it.

---

## Documentation & Guides:

Learn more about the software and its specific systems through our documentation, Documentation can be found in docs folder or by clicking the wiki button in app.

## License:

All use of this software is subject to our license terms. Please read the **LISENCE.txt** file in the root directory for full details on usage and permissions.
