# Future Updates & Roadmap

This document outlines the planned trajectory for Marshal IDE. Note: Features marked as "Researching" are experimental.
## 🛠 v1.2.2 – bug fixes
### Fixes
- **importer:** Fixing importer to output correct dsl afther the new compiler refractor it now outputs incorrect syntax that gives errors
- **compiler:** Fixing compilers calculated final cost to be correct (final cost = -0.02)

## 🛠 v1.3.0 – UX Update
### UX changes
- **Tailwind CSS Architecture Migration:** Replace the legacy Sass preprocessor setup with Tailwind CSS. Leverage utility-first classes to drastically accelerate frontend layout changes, while utilizing Tailwind's compiler to purge unused styles and generate a highly optimized, lightweight production CSS bundle.
- **In-Line "Jump to Error" Navigation:** Error listings in the bottom console are now fully interactive. Clicking a validation error line instantly targets the specific file tab, opens it, and - drops the editor cursor directly onto the offending line for immediate fixing
- **Persistent Tab States Across Sessions:** The IDE now caches open tab layouts per project. Switching workspaces or reopening a mod instantly restores the exact files that were open, in their precise tab order.
- **Drag-and-Drop Sidebar Image Importing:** Expanded the asset workflow by allowing users to drag .dds files directly from the OS file manager and drop them anywhere onto the left-hand PROJECT FILES sidebar. The IDE automatically routes them into the project's background GFX directory and refreshes the tree instantly.
- **Visual Workspace Personalization:** Add a dedicated Themes & Appearance tab in Global Application Settings. Users can now customize the IDE environment with selectable accent colors and define custom token colors for syntax highlighting.

### Compiler changes
- **Parser**: Shared coordinate saving method for parser, pass over what the syntax is eg: "position" OR "max size" and the raw line itself, and it will return X coordinates and Y coordinates!(this will make all parser work similarly)
- **Validator**: Make the validators Save the data they check, they check if the ID is correct? also save the ID so that the parser doesn't have to do that exact same thing!(and maybe have a minor difference that leads to a hard to find bug)

## 🛠 v1.4.0 – Compiler Update
### Compiler changes
- **Scripted Gui Scale**: add width and heighth to be a % instead of only coordinates

---
## 🔭 Long-term Research & Development
- **Asset Suite**: 
    - Built-in `.dds` image previewer.
    - Integrated JPEG/PNG to `.dds` converter.
- **Workflow Tools**:
    - Integrated Git GUI for version control.
- ### DSL Expansions
    - **MIO Support**: Dedicated DSL for Military Industrial Organizations.
    - **Country Definitions**: Streamlined syntax for defining new nations and tags.
