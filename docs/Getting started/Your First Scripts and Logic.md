# Scripts and Logic

The **Scripts** category is the "engine room" of your mod. It combines Game Rules, On Actions, Scripted Effects, and Scripted Triggers into a single, unified syntax. Instead of hunting through four different folders in vanilla HOI4, you can manage your global logic right here.

---

### 1. Scripted Effects
Scripted effects are reusable blocks of code. Once defined, you can call them from events, decisions, or focuses just by typing their id. (example of how to call a scripted effect: click_me_button_ad_effects = yes)

```marshal
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
On Actions allow you to "hook" your code into the game engine's heartbeat (e.g., every day, every month, or when a state is occupied).
```
on action
    on_monthly = {
        effect = {
            # Loop through everyone once per month
            every_country = {
                limit = {
                    has_stability < 0.05
                    has_civil_war = no
                }
                
                # Use Marshal's if/then/else or standard vanilla limits
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
```

### 3. Scripted Triggers
Triggers are boolean (true/false) checks used to simplify complex requirements(used inside if else statements).
```
scripted trigger is_germany
    tag = GER
```

### 4. Game Rules
Game rules allow players to customize their experience in the pre-game lobby. Marshal simplifies the multi-file process of defining the rule, the group, and the localization into a single block.

#### Defining the Rule
In your Marshal file, you define the visual toggle for the lobby. Note that the rule block itself does **not** contain the logic for what happens in-game; it only defines the available choices.

```
game rule enable_france
    name "Enable France"
    group "Custom Rules"
  
    default option "enable"
    
    option "disable"
```
### Implementing the Logic
Because HoI4 treats game rules as global flags, you must check the state of the rule within an on_action (like on_startup), a national focus, or an event. You do this using the has_game_rule trigger.

Example: Applying the rule on game start
In your logic files, you would check the player's selection like this:
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
                    # The actual logic goes here
                    FRA = { set_cosmetic_tag = FRA_DISABLED_TAG }
        }
    }
```

### 5. Why Use Marshal for Scripts?
Centralized Logic: Keep your global triggers and effects organized without jumping between deep folder structures.

Simplified Game Rules: Defining a game rule in vanilla usually requires a rule file and a localization file. Marshal handles the strings and the functional logic in one block.
