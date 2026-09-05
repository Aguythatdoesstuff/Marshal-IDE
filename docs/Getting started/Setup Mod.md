# Setup a Marshal Mod

Before using Marshal IDE, you must first create a standard Hearts of Iron IV mod using the Paradox Launcher. Marshal integrates directly with this mod and manages its output automatically.

Once configured, you should **never need to manually interact with the mod output folder again**.

---

# Step 1 — Create a Mod in the Paradox Launcher

1. Open the Paradox Launcher.
2. Navigate to the **Mods** section.
3. Click **Create Mod**.
4. Fill in the basic information:
   - Name
   - Version
   - Tags (minimum 1 required)

The launcher will generate a new mod folder inside your HOI4 mod directory.

This folder will act as the **output target** for Marshal.

---

# Step 2 — Create a Workspace in Marshal IDE

Open **Marshal IDE** and create a new workspace.

A workspace contains your **Marshal source files**, which are compiled into the standard Hearts of Iron IV scripting format.

Your workspace is where you will actually write and organize your mod content.

---

# Step 3 — Set the Output Path

Inside the workspace settings, configure the **Output Path** to point to the mod folder created by the Paradox Launcher.

Example structure:

HOI4/mod/your_mod_name/ ← Set this as the output path


Marshal will now compile and synchronize all generated files directly into this mod folder.

---

# How the Workflow Works

Once the output path is configured:

1. You write **Marshal Script** inside the IDE.
2. Marshal **transpiles** the code into HOI4-compatible script files.
3. The generated files are written directly into your mod folder.
4. The IDE automatically keeps everything synchronized.

If a source file is renamed or deleted, Marshal also removes the corresponding generated files.

This prevents **ghost files** from accumulating in your mod.

---

# Important

The mod folder configured as your **Output Path** should be treated strictly as **generated output**. To ensure your project remains stable, follow these rules:

1. **Avoid Overwriting Output Files**: The IDE will freely overwrite compiler-generated files in your target mod directory. Never place original source assets or custom non-compiler files under names that match generated output files, or they will be overwritten without confirmation.
2. **Source is King**: Use the workspace as the central hub for your project. Unsupported features or non-DSL files can exist outside the IDE without issue, but anything transpiled should originate within your workspace scripts.
3. **Protect the Descriptor**: The Marshal IDE does not currently edit or generate `descriptor.mod` files. Always keep the launcher-generated descriptor intact in your target mod directory.

> [!IMPORTANT]
> **Do not delete the `descriptor.mod`** created by the Paradox Launcher. Without this specific file, Hearts of Iron IV will not recognize your mod, even if your Marshal Script compiles perfectly.

---
---

### Project Structure & Extensions

Marshal IDE uses folder-aware defaults to streamline file creation. 

When you create a file inside a designated directory (such as `events/`), the file creation wizard automatically selects the recommended compiler extension (e.g., `.event`). 

While you can technically place other file types inside different folders, using an extension that does not match the file's content will cause syntax checks to fail and generate malformed syntax errors in your console. 

Key details to keep in mind:
* **Recommended Extensions:** The IDE automatically selects the proper extension based on your target folder so you rarely have to adjust it manually.
* **Folder Creation:** Creating custom folders directly within the IDE is currently disabled to ensure auto-selection works reliably.
---

# Next Step

Continue to **Getting Started → Your First Event** to learn how to create your first piece of mod content using Marshal Script.
