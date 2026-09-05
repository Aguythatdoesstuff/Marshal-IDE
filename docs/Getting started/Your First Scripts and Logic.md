# Scripts and Logic

The **Scripts** category is the "engine room" of your mod. It consolidates Game Rules, On Actions, Scripted Effects, and Scripted Triggers into a single, unified workflow. Instead of navigating separate directories in vanilla HOI4, you can manage your global logic directly within `.script` files.

---

### 1. Scripted Effects
Scripted effects are reusable blocks of execution logic. Once defined, you can invoke them directly inside events, decisions, focuses, or `on_action` blocks simply by referencing their ID (e.g., `trigger_event = yes`).

```
scripted effect trigger_event
    random_list = {
        50 = { 
            country_event = { id = country_event.1 }
        }
        50 = { 
            news_event = { id = news_event.1 }
        }
    }

scripted effect click_me_button_ad_effects
    clr_country_flag = open_ad1
    random_list = {
        1 = { country_event = { id = response.1 } }
        1 = { country_event = { id = response.2 } }
        1 = { country_event = { id = response.3 } }
        1 = { country_event = { id = response.4 } }
        1 = { country_event = { id = response.5 } }
    }
```

### 2. On Actions
On Actions allow you to hook code directly into engine lifecycle events (e.g., daily ticks, monthly ticks, or initial game startup).
```
on action
    on_monthly = {
        effect = {
            # Execute monthly logic across nations
            every_country = {
                limit = {
                    has_stability < 0.05
                    has_civil_war = no
                }
                
                if = {
                    limit = { is_ai = yes }
                    random_civil_war = yes
                    news_event = { id = stability_spiral.2 }
                }
                
                if = {
                    limit = { is_ai = no }
                    country_event = { id = stability_spiral.1 }
                }
            }
        }
    }

    on_daily = {
        effect = {
            # Execute daily ticks
        }
    }

    on_startup = {
        effect = {
            trigger_event = yes
        }
    }
```
### 3. Scripted Triggers
Scripted triggers represent reusable boolean condition checks, ideal for simplifying multi-step requirement blocks.
```
scripted trigger is_germany
    tag = GER
```

### 4. Game Rules
Game rules allow players to toggle custom options in the pre-game lobby. Marshal simplifies the setup process by organizing the rule parameters and localization into a single declaration.

Defining the Lobby Rule (example_game_rules.script)
The rule declaration defines the toggle interface, default settings, and available options:
```
game rule enable_france
    name "enable fr*nce"
    group "misc"
    default option "enable"
    option "disable"
```

Implementing the Logic
Check the player's selected rule choice inside logic routines (such as an on_startup action) using the has_game_rule trigger:
```
on action
    on_startup = {
        effect = {
            if
                has_game_rule = { 
                    rule = enable_france 
                    option = disable 
                } 
                then
                    FRA = { set_cosmetic_tag = FRA_DISABLED_TAG }
        }
    }
```

### 5. Key Advantages
Centralized Logic: Manage global triggers, effects, on-actions, and lobby rules inside clean .script source files.

Implicit Localization: Game rule titles and option labels inside quotes are automatically transpiled into properly formatted .yml localization keys.

Direct Pass-Through: Standard Paradox triggers and effects function natively alongside Marshal Script's tabbed syntax structures.