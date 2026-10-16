# Your First Unit Template

The **Unit DSL** allows you to define battalion types, combat statistics, equipment requirements, and unit classifications for your mod. Instead of manually cross-referencing multiple sub-folders in vanilla HOI4, you can declare complete unit definitions inside clean `.unit` files.

---

### 1. Basic Metadata and Localization
Every unit definition begins with basic identification settings, including abbreviations, localized labels, and 3D map model bindings.

```
unit artillery 
    abbreviation "ART"
    name "Artillery"
    desc "Artillery"
    unit model "artillery"
```

---

### 2. Unit Types, Categories, and Equipment Requirements
Specify the internal unit classifications (such as line infantry or support), UI categories, and the specific equipment types and quantities needed to field the unit.

```
    unit types some_other_type
        infantry
        support

    unit categories category_divisional_support_battalions
        category_support_battalions
        category_army
        category_artillery
        category_support_artillery

    required equipment
        artillery_equipment = 12
```

---

### 3. Tactical Attributes and Combat Scaling
Define how the unit behaves on the map, including priority settings, organization weight, combat width, training requirements, and supply consumption.

```
    map_icon_category = infantry
    priority = 0
    ai_priority = 0
    active = yes
    affects_speed = no
    is_artillery_brigade = yes
    regimental = no
    combat_width = 0

    manpower = 300
    training_time = 120
    max_organisation = 0
    default_morale = 0.1
    max_strength = 0.2
    weight = 0.1

    supply_consumption = 0.16
```

---

### 4. Modifiers and Ability Caps
Apply baseline combat multipliers or modifiers—such as support penalties or airborne deployment restrictions.

```
    # Support nerfs to combat abilities
    defense = -0.5
    breakthrough = -0.5
    soft_attack = -0.5
    hard_attack = -0.5

    can_be_parachuted = no
```

---

### 5. Key Advantages
- **Declarative Structure:** Clearly group unit categories, sub-types, and required equipment within intuitive block scopes.
- **Unified Unit Setup:** Manage stats, combat width, supply requirements, and 3D model references together in a single source file.
- **Integrated Modifiers:** Apply base stat modifiers (such as support nerfs or parachuting abilities) directly alongside core unit parameters.