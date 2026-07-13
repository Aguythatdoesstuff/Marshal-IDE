# Future Updates & Roadmap

This document outlines the planned trajectory for Marshal IDE. Note: Features marked as "Researching" are experimental.

## 🛠 v1.2.3 – Bug fixes
- **Fix importer:** Fix importers other bugs when importing test mod.
  - **Decision Icons & Pictures Support:** Update compiler and importer logic to correctly handle the two distinct asset types within decisions:
    - **Icon:** The small graphic displayed to the left of the category or decision name. The engine handles this dynamically, so it does *not* strictly require the `GFX_` prefix.
    - **Picture:** The larger graphic displayed to the left of a category's description. The engine is strict here, meaning it *requires* the `GFX_` prefix to resolve properly.
  - **Required Actions:** Update the compiler to explicitly support both the `icon` and `picture` syntax definitions, and ensure the importer accurately distinguishes between them (treating `icon` as prefix-optional and `picture` as prefix-mandatory) during processing.
  - **Fix Validation False Positives:** Fix a bug in the compiler validation logic that incorrectly throws indentation errors when parsing valid script blocks. The validator must be updated to correctly calculate indentation levels and stop failing on perfectly valid formatting inside complex scopes like `start_civil_war` blocks (such as nested `PREV` and `PREV.PREV` calls). specifically:
  ```
  country event united_kingdom_monarchist.8
    name "The Return of the King"
    desc "King Fuad II has returned! He has called the army to back him instead of the dictatorial government currently ruling Egypt. He appears to have some backers of the... English variety."
    sprite "GFX_report_event_civil_war"
    option "We support the King!"
        hidden_effect = {
            453 = { set_demilitarized_zone = no }
            ENG = {
                set_autonomy = {
                    target = EGY
                    autonomy_state = autonomy_dominion
                    freedom_level = 0.5
                }
            }
        }
        start_civil_war = {
            ideology = fascism
            size = 0.1
            army_ratio = 0.5
            navy_ratio = 0
            air_ratio = 1
            keep_all_characters = yes
            PREV = {
                EGY_abdel_fattah_el_sisi = {
                    set_nationality = PREV.PREV
                }
                promote_character = EGY_abdel_fattah_el_sisi
            }
            PREV.PREV = {
                set_cosmetic_tag = EGY_presidential_forces
            }
        }
        add_to_war = {
            targeted_alliance = ENG
            enemy = PREV
            hostility_reason = asked_to_join
        }
        set_politics = {
            ruling_party = neutrality
            elections_allowed = no
        }
        set_cosmetic_tag = EGY_british_loyalty
        set_party_name = {
            ideology = neutrality
            long_name = EGY_monarchists
            name = EGY_monarchists
        }
  ```
  - **ID Encoding & Case Validation:** Adjust compiler validation to allow alphanumeric characters, underscores, and periods (`.`) in focus/object IDs (e.g., `GER_cdu_2.0`). By default, permit full capitalization across all IDs. For IDs utilizing non-ASCII special characters/umlauts (e.g., `GER_grünes_zeitalte`), allow compilation but emit a warning regarding UTF-8 parsing and trigger risks.
  - **New Idea Category Support:** Add compiler and importer support for the `hidden_ideas` / `hidden_idea` idea type to allow properly defining and parsing hidden national ideas.

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
