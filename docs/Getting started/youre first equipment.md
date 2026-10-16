# Your First Equipment Template

The **Equipment** dsl allows you to define custom military hardware, weapons, and vehicles for your mod. Instead of dealing with fragmented, multi-file structures in vanilla HOI4, you can define equipment archetypes, cascading stats, and sequential variants inside a single, clean `.equipment` file.

---

### 1. Defining Equipment Types (Archetypes)
An equipment type acts as the parent archetype or baseline for a class of weapons. It establishes global stats (like reliability, cost, and combat capabilities) that nested variants inherit, while also specifying which unit types can actually utilize the equipment.

```
define type anti_air_equipment available from 1934
    sprite "archetype_anti_air_equipment"
    # internally its: type = <type>    # Internal type: what kind of unit can use this equipment
    for units 
        anti_air
        infantry # adding infantry type
    
    group_by = archetype
    
    interface_category = interface_category_land

    # Misc Abilities
    reliability = 0.8
    maximum_speed = 4

    # Defensive Abilities
    defense = 4
    breakthrough = 1
    hardness = 0
    armor_value = 0

    # Offensive Abilities
    soft_attack = 3
    hard_attack = 7
    ap_attack = 25
    air_attack = 19

    # Space taken in convoy
    lend_lease_cost = 3

    build_cost_ic = 4
    resources = {
        steel = 2
    }
```

---

### 2. Nesting Equipment Variants
Sub-models and sequential upgrades (like tier 1 and tier 2 versions) are nested directly inside their parent type. Because `anti_air_equipment_1` is scoped inside `anti_air_equipment`, the transpiler automatically knows it inherits from that archetype.

```
    equipment anti_air_equipment_1 available from 1936 # because anti_air_equipment_1 is inside anti_air_equipment, it uses anti_air_equipment as its archetype
        name "My cool equipment"
        desc "My verry long description"
        short desc "cool desc"

        name for GER "My cool equipment"
        desc for GER "My verry long description"
        short desc for GER "cool desc"

        priority = 80
        visual_level = 0
```

---

### 3. Iterating on Successive Models
You can nest further tiers (like `anti_air_equipment_2`) inside previous models to establish upgrade pathways. Nested variants only require you to declare stats that change or scale up from the base type or previous tier.

```
        equipment anti_air_equipment_2 available from 1940 # clearly parent = anti_air_equipment_1 archetype = anti_air_equipment
            name "My cool equipment2"
            desc "My verry long description2"
            short desc "cool desc2"

            name for GER "My cool equipment2"
            desc for GER "My verry long description2"
            short desc for GER "cool desc2"
            
            priority = 80
            visual_level = 1
            
            soft_attack = 3.5
            hard_attack = 11
            ap_attack = 60
            air_attack = 25

            build_cost_ic = 5
            resources = {
                steel = 2
            }
```

---

### 4. Key Advantages
* **Hierarchical Nesting:** Cleanly group variants under their parent archetypes without needing complex cross-file ID cross-referencing.
* **Stat Inheritance:** Base stats declared at the type level automatically apply to all child equipment variants unless overridden.
* **Localized Names & Descriptions:** Easily support custom naming and flavor text for specific tags (e.g., `name for GER`) right inside the block structure.
```