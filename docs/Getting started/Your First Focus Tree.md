# National Focuses

National Focuses in Marshal Script move away from the massive nested blocks of vanilla HOI4. Instead, they use a **tab-based** structure that defines timing, positioning, and effects in a streamlined, readable way.

---

### 1. Defining a Tree
You can define a **Default Tree** (for all countries meeting a factor) or a **Country Specific Tree** linked to a specific tag (remember to correctly override vanilla files!).

#### Default Tree Template
```
default tree generic_tree
    reset_on_civilwar = no
    country = {
        factor = 1
    }
    initial_show_position = {
        focus = political_effort
    }
```
Country Specific Template
```
tree german_tree_id for GER
    reset_on_civilwar = yes
```
### 2. Creating a Focus
Inside the tree block, focuses are defined using the focus keyword followed by the ID and the duration.
```
focus pro_west_path takes 70 days
    # Requirements and Exclusions
    require political_effort
    prevents pro_east_path
        neutral_path
        radical_junta_path

    # Positioning
    follow position of political_effort
    position x-12 y1

    on complete
        add_popularity = {
            ideology = democratic
            popularity = 0.05
        }
        every_country = {
            limit = {
                has_ideology = democratic
            }
            add_opinion_modifier = {
                target = GER
                modifier = {
                    generic_focus_allign_with_us = hjkjkh
                }
            }
        }
```
### 3. Key Syntax Features

* **Duration**: Use `takes X days` directly in the header. No more `cost = X` math.
* **Localization**: Use the `name` field with a string. Marshal handles the `.yml` generation automatically.
* **Requirements**: Use `require [ID]` for prerequisites.
* **Mutual Exclusions**: Use `prevents` followed by a list of IDs.
* **Positioning**: Use `follow position of [ID]` to anchor a focus, then use `position x[val] y[val]` to offset it.
* **Completion Effects**: All rewards go inside the `on complete` block.

---

### 4. Why Use Marshal for Focuses?

* **Zero Boilerplate**: You don't need to manually define search filters or redundant wrapper blocks.
* **Visual Logic**: The `follow position` system makes it much easier to move entire branches of a tree at once without recalculating every coordinate.
* **Clean Effects**: Marshal passes standard HOI4 effects (like `add_popularity`) directly through to the compiler while keeping the script scannable.
