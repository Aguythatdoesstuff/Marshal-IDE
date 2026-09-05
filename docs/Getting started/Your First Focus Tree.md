# National Focuses

National Focuses in **Marshal Script** move away from the massive nested blocks of vanilla HOI4. Instead, they use a **tab-based** structure that defines timing, positioning, and completion effects in a streamlined, scannable format.

---

### 1. File Naming & Overriding Vanilla Trees
> [!NOTE]
> Without `replace_path` in your `descriptor.mod`, you must name your Marshal file after the target vanilla or mod file you want to override (e.g., to override vanilla `generic.txt`, name your file `generic.focus`). The IDE automatically sets the correct extension based on the folder location.

---

### 2. Defining Focus Trees

#### Default Tree Template
Used for trees shared across multiple nations meeting specific conditions:

```
default tree generic_focus
    reset_on_civilwar = no
    initial_show_position = {
        focus = political_effort
    }
```
### Country-Specific Tree Template
Scoped directly to a specific country tag using the for [TAG] syntax:

```
tree german_tree for GER
    reset_on_civilwar = no
    initial_show_position = {
        focus = other_political_effort
    }
```

### 3. Creating Focuses
Inside a tree block, focuses are declared using the focus keyword followed by the focus ID and duration using takes X days.

Generic Focus Tree Example (generic.focus)
```
default tree generic_focus
    reset_on_civilwar = no
    initial_show_position = {
        focus = political_effort
    }

    focus political_effort takes 10 days
        name "Address the Political Crisis"
        desc "Our nation stands at a crossroads. We must choose a direction."
        sprite "GFX_goal_generic_political_pressure"
        position x14 y0
        on complete
            add_political_power = 50

    focus pro_west_path takes 69 days
        name "The Atlantic Outreach"
        sprite "GFX_focus_generic_approach_the_west"
        require political_effort
        prevents pro_east_path
            neutral_path
            radical_junta_path
        follow position of political_effort
        position x0 y1
        on complete

    focus democratic_reforms takes 67 days
        name "Institutional Democratic Reforms"
        sprite "GFX_focus_ICE_republicanism"
        require pro_west_path
        prevents nato_partnership_prog
        follow position of pro_west_path
        position x-2 y1
        on complete

    focus nato_partnership_prog takes 420 days
        name "NATO Partnership for Peace"
        sprite "GFX_focus_generic_treaty"
        require pro_west_path
        prevents democratic_reforms
        follow position of pro_west_path
        position x0 y1
        on complete

    focus eu_association_agreement takes 123456789 days
        name "EU Association Agreement"
        sprite "GFX_focus_generic_the_council_of_europe"
        require pro_west_path
        follow position of pro_west_path
        position x2 y1
        on complete

    focus judicial_independence takes 125 days
        name "Judicial Independence Act"
        sprite "GFX_focus_generic_improve_the_administration_2"
        require democratic_reforms
        follow position of democratic_reforms
        position x-1 y1
        on complete

    focus joint_nato_training takes 62 days
        name "Joint NATO Training Centers"
        sprite "GFX_focus_generic_military_mission"
        require nato_partnership_prog
        follow position of nato_partnership_prog
        position x0 y1
        on complete

    focus free_trade_negotiations takes 70 days
        name "Western Free Trade Negotiations"
        sprite "GFX_goal_generic_trade"
        require eu_association_agreement
        follow position of eu_association_agreement
        position x1 y1
        on complete
```
Country-Specific Example (german_tree_example.focus)
```
tree german_tree for GER
    reset_on_civilwar = no
    initial_show_position = {
        focus = other_political_effort
    }

    focus other_political_effort takes 10 days
        name "EIN REICH!!"
        desc "Our nation stands at a crossroads. We must choose a direction."
        sprite "GFX_goal_generic_political_pressure"
        position x14 y0
        on complete
            add_political_power = 50
```

### 4. Key Syntax Features
* Duration: Written directly in the focus header as takes X days(or weeks).

* Implicit Localization: Provide strings directly inside quotes for name and desc. The compiler generates .yml files automatically.

* Prerequisites: Use require [focus_id] to set requirements.

* Mutual Exclusions: Use prevents followed by indented focus IDs.

* Dynamic Positioning: Anchor focuses using follow position of [focus_id] and offset them with relative coordinates using position x[val] y[val].

* Completion Effects: Place rewards and logic blocks directly under on complete.

### 5. Why Use Marshal for Focus Trees?
Zero Boilerplate: Avoid manual cost = X calculations, redundant wrapper brackets, and endless curly braces.

* Relative Positioning: Anchoring focus branches with follow position of makes shifting entire trees intuitive without needing to recalculate coordinates manually for every node.

* Direct Pass-Through: Standard HOI4 effects (like add_political_power = 50) execute seamlessly while maintaining clean, tabbed code readability.