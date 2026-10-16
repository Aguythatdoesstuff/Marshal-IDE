# Your First History Template

The **History DSL** simplifies country definition, dynamic localization, party naming, and starting game state in your mod. Instead of manually maintaining multiple localization files and separate history entries, the transpiler generates the required `.txt` history files and `.yml` localization entries automatically.

---

### 1. Illegal Country Tags
When defining country tags, avoid using reserved keywords or engine triggers as tag names. Using these reserved names can cause severe logic breakage, invalid state triggers, or broken custom map modes, wich is why the transpiler strictly blocks these tags during country definition in DSL.

| Reserved Tag | Reason to Avoid |
| :--- | :--- |
| `NOT` | Flow control trigger (evaluates true when enclosed conditions are false). |
| `AND` | Flow control trigger (evaluates true when all enclosed conditions are true). |
| `TAG` | Standard trigger checking the currently selected country. |
| `OOB` | History file argument specifying order of battle files (division locations and amounts). |
| `LOG` | Logging effect/trigger writing to console or `/Hearts of Iron IV/logs/game.log`. |
| `NUM` | Array counter tag. Using this breaks resistance system scripted triggers. |
| `RED` | Custom map mode RGB channel variable. Using this zeroes red map values. |
| *Numeric* | Fully numeric tags (e.g., `123`) cause parser confusion with state IDs. |

---

### 2. Country Declaration & Dynamic Localisation
Country scope setup starts with basic naming, dynamic naming rules, and party names. Dynamic names follow the rule structure `[Key Prefix] + [Joined Condition Tokens] + [Tail Suffix]` and are transpiled directly into localized keys.

```
country "Scotland" with tag POL
    # Base names
    name def "Scotland"
    name adj "Scottish"

    # Dynamic ideology variants (outputs: TAG_ideology, TAG_ideology_DEF, TAG_ideology_ADJ)
    name if ideology democratic "Republic of Scotland"
    name if ideology democratic def "the Republic of Scotland"
    name if ideology democratic adj "Scottish"

    # Sub-ideology handling (sub-ideology acts as an alias for ideology)
    name if sub-ideology liberalism "Federal Republic of Scotland"

    # Subject & Autonomy variants
    name if is subject "$OVERLORDADJ$ Scotland"
    name if is subject of IRE "Alba"
    name if autonomy is dominion "Dominion of Scotland"
    
    # Combined condition chain: [overlord] + [ideology] + [autonomy]
    name if subject of IRE and ideology democratic and autonomy integrated_puppet "Most Northern Ireland"

    # Generic subject prefix rule for subjects of Scotland (outputs: COUNTRY_TAG_autonomy_...)
    name if overlord of generic and autonomy dominion "Comhairle nan $NONIDEOLOGY$"

    # Political party names
    party name communism short "SCP"
    party name communism long "Scottish Communist Party"
    party name neutrality "True Scotsman"
```

---

### 3. Core Properties & Map Colors
Set the country's capital state and map colors directly within the block scope.

```
    capital 121
    map color 2 10 222 # RGB color for the political map mode
    ui color 255 255 155 # Coloring for common/countries/colors.txt (transpiled to: color_ui = rgb { 255 255 155 })
```

---

### 4. Pass-Through HOI4 Logic
Everything else inside the country block is treated as standard HOI4 scripted history logic and passed directly into the transpiled country history file (`/history/countries/`).

```
    if = {
        limit = { has_dlc = "Man the Guns" }
        set_naval_oob = "SCO_1936_naval_mtg"
    }
    if = {
        limit = { NOT = { has_dlc = "Man the Guns" } }
        set_naval_oob = "SCO_1936_naval_legacy"
    }
    if = {
        limit = { has_dlc = "By Blood Alone" }
        set_air_oob = "SCO_1936_air_bba"
    }
    if = {
        limit = { NOT = { has_dlc = "By Blood Alone" } }
        set_air_oob = "SCO_1936_air_legacy"
    }

    add_ideas = {
        henschel
        GER_autarky_idea
        war_economy
        extensive_conscription
    }
    
    set_technology = {
        infantry_weapons = 1
        infantry_weapons1 = 1
        gw_artillery = 1
        interwar_antiair = 1
        fuel_silos = 1
        basic_train = 1
    }
```

---

### 5. Key Advantages
- **Single-Source Country Setup:** Manage country properties, conditional names, party names, and starting history scripts in one place.
- **Automated Localisation Keys:** Dynamic name logic automatically generates the correct localization tokens (`TAG_ideology`, `TAG_autonomy_...`, etc.) without writing manual YML files.
- **Transparent Pass-Through:** Native HOI4 triggers, ideas, DLC checks, and technology blocks remain fully functional within the DSL scope.