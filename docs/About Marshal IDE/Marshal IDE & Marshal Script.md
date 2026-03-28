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

- Events
- Decisions
- National Focuses
- Spirits and Ideas
- Scripted effects and triggers
- Game rules and on-actions

Marshal Script is **not a replacement for the game's scripting system**.  
Instead, it acts as a layer on top of it.

The DSL is compiled (transpiled) into the standard scripting format used by Hearts of Iron IV.

---

# Why "Script"?

The name **Marshal Script** reflects the technology used to build the language.

The DSL and its compiler infrastructure are heavily built around **JavaScript-based tooling**, which powers:

- The transpiler
- Parts of the IDE runtime
- Internal tooling and automation systems

Because of this foundation, the term **Script** was chosen to represent the language layer of the project.

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

While still producing fully compatible game files.
