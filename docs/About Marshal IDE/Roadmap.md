# Future Updates & Roadmap

This document outlines the planned trajectory for Marshal IDE. Note: Features marked as "Researching" are experimental.

## 🛠 v1.4.0 – Compiler Update
### Compiler changes
    - **MIO Support**: Dedicated DSL for Military Industrial Organizations.(https://hoi4.paradoxwikis.com/Military_industrial_organization_modding)
    - **Division Support**: Dedicated DSL for Division modding.(https://hoi4.paradoxwikis.com/Division_modding)
    - **Unit Support**: Dedicated DSL for Unit modding.(https://hoi4.paradoxwikis.com/Unit_modding)
    - **Equipment Support**: Dedicated DSL for Equipment modding.(https://hoi4.paradoxwikis.com/Equipment_modding)


---
## 🔭 Long-term Research & Development
- **Asset Suite**: 
    - Built-in `.dds` image previewer.
    - Integrated JPEG/PNG to `.dds` converter.
- **Workflow Tools**:
    - Integrated Git GUI for version control.
- ### DSL Expansions
    - **Country Definitions**: Streamlined syntax for defining new nations and tags.
### Compiler changes
    - **Parser**: Shared coordinate saving method for parser, pass over what the syntax is eg: "position" OR "max size" and the raw line itself, and it will return X coordinates and Y coordinates!(this will make all parser work similarly)
    - **Validator**: Make the validators Save the data they check, they check if the ID is correct? also save the ID so that the parser doesn't have to do that exact same thing!(and maybe have a minor difference that leads to a hard to find bug)

- **Visual Workspace Personalization:** Add a dedicated Themes & Appearance tab in Global Application Settings. Users can now customize the IDE environment with selectable accent colors and define custom token colors for syntax highlighting.

