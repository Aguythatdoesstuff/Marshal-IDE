# Your First Order of Battle Template

The **Order of Battle (OOB) DSL** allows you to define starting military forces, unit templates, naval fleets, air wing deployments, and initial production lines within a single unified syntax. Instead of managing complex nested brackets across multiple HOI4 order of battle files, the transpiler handles the structure, placement scope, and equipment mapping automatically.

---

### 1. Division Templates
Division templates define unit compositions on a 2D grid matrix ($X \times Y$ coordinates) alongside support unit attachments.

```
division template "My cool template"
    infantry = { x = 0 y = 0 }
    cavalry = { x = 0 y = 1 }
    camelry = { x = 0 y = 2 }
    motorized = { x = 0 y = 3 }
    mechanized = { x = 0 y = 4 }

    light_armor = { x = 1 y = 0 }
    medium_armor = { x = 1 y = 1 }
    heavy_armor = { x = 1 y = 2 }
    super_heavy_armor = { x = 1 y = 3 }
    modern_armor = { x = 1 y = 4 }

    marine = { x = 2 y = 0 }
    paratrooper = { x = 2 y = 1 }
    mountaineers = { x = 2 y = 2 }
    bicycle_battalion = { x = 2 y = 3 }
    amphibious_mechanized = { x = 2 y = 4 }

    artillery_brigade = { x = 3 y = 0 }
    anti_tank_brigade = { x = 3 y = 1 }
    anti_air_brigade = { x = 3 y = 2 }
    rocket_artillery_brigade = { x = 3 y = 3 }
    armored_car = { x = 3 y = 4 }

    mot_artillery_brigade = { x = 4 y = 0 }
    mot_anti_tank_brigade = { x = 4 y = 1 }
    mot_anti_air_brigade = { x = 4 y = 2 }
    mot_rocket_artillery_brigade = { x = 4 y = 3 }
    motorized_rocket_brigade = { x = 4 y = 4 }

    support units
        support_artillery = { x = 0 y = 0 }
        engineer = { x = 0 y = 1 }
        recon = { x = 0 y = 2 }
        field_hospital = { x = 0 y = 3 }
        logistics_company = { x = 0 y = 4 }
```

---

### 2. Division Deployment
Divisions are placed directly onto specific province IDs using declared division templates, complete with initial experience, equipment, and manpower modifiers.

```
place division "1st cool division" at 9392 using template "My cool template"
    start_experience_factor = 0.5
    start_equipment_factor = 1.0
    start_manpower_factor = 1.0
```

---

### 3. Air Wings Deployment
Air wings are deployed directly to states id's with valid airbases, specifying owner tags, equipment archetypes, and counts.

```
place airwings at 500
    fighter_equipment_0 = { owner = "GER" amount = 100 }
    cv_fighter_equipment_0 = { owner = "GER" amount = 50 } # Carrier Variant
    jet_fighter_equipment_1 = { owner = "GER" amount = 50 }

    CAS_equipment_1 = { owner = "GER" amount = 100 }
    cv_CAS_equipment_1 = { owner = "GER" amount = 50 }

    nav_bomber_equipment_1 = { owner = "GER" amount = 100 }
    cv_nav_bomber_equipment_1 = { owner = "GER" amount = 50 }

    heavy_fighter_equipment_1 = { owner = "GER" amount = 100 }
    scout_plane_equipment_1 = { owner = "GER" amount = 20 }

    tac_bomber_equipment_1 = { owner = "GER" amount = 100 }
    jet_tac_bomber_equipment_1 = { owner = "GER" amount = 50 }
    strat_bomber_equipment_1 = { owner = "GER" amount = 100 }
    jet_strat_bomber_equipment_1 = { owner = "GER" amount = 50 }

    rocket_suicide_equipment_1 = { owner = "GER" amount = 100 }
```

---

### 4. Fleet and Task Force Deployment
Fleets and nested task forces are placed at specific naval base location IDs. The transpiler handles both Man the Guns (MtG) designer naval units and legacy non-designer naval units.

```
# MtG Designer Fleet (uses specified ship design defined in country history)
place fleet "My cool fleet 1" at 10980
    place taskforce "Battleship taskforce" at 10980
        ship "HMS Goodnight" using archetype ship_hull_heavy_1 for GER
            using design "My cool design" # Defined inside the country history file
            category "battleship"
            pride_of_the_fleet = yes

# Legacy Fleet (omitting "using design" targets legacy naval mechanics)
place fleet "Legacy cool fleet" at 12364
    place taskforce "Main Battle Group" at 12364
        ship "Cool ship 2" using archetype ship_hull_heavy_1 for GER
            category battleship # Omitting 'using design' treats this as a legacy ship
            pride_of_the_fleet = yes 
```

---

### 5. Production Lines
Initial industrial production lines are declared with assigned factory counts, build progress, and efficiency levels.

```
add production infantry_equipment_0 for GER
    requested_factories = 5
    progress = 0.20
    efficiency = 100
```

---

### 6. Key Advantages
- **Unified Deployment File:** Manage land divisions, naval fleets, airwings, and active production lines in a single structured DSL file.
- **MtG & Legacy Compatibility:** Seamlessly declare designer ships with dynamic design references or legacy naval ships by conditionally toggling `using design`.
- **Coordinate Matrix Syntax:** Define complex multi-column division templates using simple grid coordinates (`x` and `y`).