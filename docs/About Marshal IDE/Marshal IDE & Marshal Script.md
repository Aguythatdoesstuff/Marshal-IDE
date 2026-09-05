# About Marshal IDE & Marshal Script

## What is Marshal?

Marshal is a specialized development environment and domain-specific language designed to simplify mod development for Hearts of Iron IV.

The project consists of two core components:

- **Marshal IDE** – the development environment used to build and manage mods.
- **Marshal Script** – a high-level Domain Specific Language (DSL) that transpiles into standard Hearts of Iron IV scripting.

Together they provide a streamlined workflow for creating complex mods with less boilerplate, better structure, and integrated tooling.

---

# The Name "Marshal"

The name **Marshal** comes from the military rank **Field Marshal**, a high command rank used in many armed forces.

In Hearts of Iron IV, the **Field Marshal** represents the highest level of strategic command. The name reflects the philosophy behind the project:

> Marshal gives mod developers high-level command over their mod's systems and structure.

Instead of manually managing hundreds of script files and assets, the IDE coordinates and organizes the workflow for you.

---

# Marshal IDE

**Marshal IDE** is the central tool used to develop mods.

It provides an integrated environment built specifically for Hearts of Iron IV modding, including tools for:

- Writing and organizing mod scripts
- Managing mod workspaces
- Automatically generating game-compatible output
- Handling asset integration

The IDE focuses on reducing repetitive work and improving development speed while keeping the final output fully compatible with the game.

---

# Marshal Script (The DSL)

**Marshal Script** is the domain-specific language used inside the IDE.

It provides a higher-level syntax for creating common Hearts of Iron IV mod structures such as:

- Scripted GUI
- Events
- Decisions
- National Focuses
- Spirits and Ideas
- Scripted effects and triggers
- Game rules and on-actions

Marshal Script is **not a replacement for the game's scripting system**.  
Instead, it acts as a layer on top of it.

The DSL is compiled (transpiled with our multi pass transpiler) into the standard scripting format used by Hearts of Iron IV.

---

# Why "Script"?

The name **Marshal Script** reflects the technology used to build the original versions of the language and its execution environment within Marshal IDE.

The early DSL and compiler infrastructure relied heavily on **JavaScript-based tooling**, which powered:

- The original transpiler and parser
- Core internal tooling and automation systems
- Early runtime execution steps

### Evolution of the Stack

While heavy compilation tasks have since migrated to a high-speed C# engine, JavaScript remains the core driver of the editor experience itself. Today, JavaScript powers the entire application shell and user interface:

- **Application Startup:** Launching the IDE initializes the JavaScript runtime that renders the environment, panels, and workspace layouts.
- **UI & Event Handling:** Every visual component—from button clicks and menu actions to panel transitions—is driven by fast, event-based JavaScript routines.
- **Workspace State:** User input, interface updates, and active view state are handled in real time by the front-end process.

Because JavaScript established the foundational architecture—and continues to power everything you see and interact with inside the IDE—the term **Script** remains an accurate reflection of the environment's origins and design philosophy.

---

# Philosophy

Marshal is built around three core ideas:

### Automation
Reduce manual tasks such as asset setup, file synchronization, and repetitive script structures.

### Structure
Provide a consistent and organized way to define mod systems.

### Workflow Integration
Combine editing, compiling, asset management, and debugging into a single environment.

---

# Goal of the Project

The long-term goal of Marshal is to make large-scale Hearts of Iron IV modding:

- Faster
- More structured
- Less error-prone
- Easier to maintain
- Easier to mod
- Easier to expand
- But most importantly more fun to mod!

While still producing fully compatible game files.
