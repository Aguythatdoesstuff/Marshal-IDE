# Advanced Scripted GUI

The Marshal GUI DSL simplifies interface creation in HOI4. Instead of manually editing `.gui` coordinate files, creating dozens of `spriteTypes`, and writing complex `scripted_effects`, Marshal combines UI structure and game logic into unified blocks within `.scriptedgui` files.

---

### 1. Windows: Draggable vs. Static
Define interface containers directly by declaring their window type:
* **Draggable Window**: Allows players to reposition the frame during gameplay.
* **Window**: Standard static interface frame.

```
# Basic draggable window
draggable window my_cool_window_id
    size x1573 y900
    position x130 y85     
    sprite "GFX_tiled_window"
    visible
        always = yes
```
### 2. Progress Bars
Instead of creating full and empty .dds textures, define progress bars directly using RGB color values and step counts linked to game variables:

```
# Background frame
icon
    sprite "GFX_pol_goal_progress_frame"
    position x233 y75 
    size 100%

# Dynamic progress bar
horizontal bar with 70 steps
    unprogressed color 0.0 0.0 0.3 # Dark Blue
    progressed color 0.0 0.0 0.7   # Bright Blue
    var "time_left"
    position x235 y76   
    size x235 y4
```

### 3. Reactive UI Elements
Create dynamic UI components (such as buttons that change color or state when clicked) by pairing root-level define blocks with window elements.

### Step A: Define Dynamic States
Declare sprites and text logic outside the window block to evaluate variables dynamically:

```
define sprite button1_gfx
    if
        check_variable = { button1_enabled = 1 }
        then
            sprite GFX_green_background_button
    else
        sprite GFX_red_background_button

define text button1_text
    if
        check_variable = { button1_enabled = 1 }
        then
            text "§GGreen§"
    else
        text "§RRed§"

define text enable_or_disable_button1
    if
        check_variable = { button1_enabled = 1 }
        then
            text "§GEnabled§"
    else
        text "§REnable§"
```

### Step B: Assemble the GUI Element
Stack text, icons, and buttons within the window block. Clicking the button updates the underlying variable, triggering instant visual updates:

```
draggable window minigame_1
    size x700 y600
    position x430 y270
    sprite "GFX_tiled_window"
    visible
        has_country_flag = open_minigame_1

    text
        text button1_text
        font "hoi_20b"
        position x127 y175
        
    icon
        sprite button1_gfx
        position x110 y200 
        
    button
        text enable_or_disable_button1
        font "hoi_16mbs"
        sprite "GFX_button_94x31"
        position x110 y200 
        on click
            if
                check_variable = { button1_enabled = 1 }
                then
                    set_variable = { button1_enabled = 0 }
            else
                set_variable = { button1_enabled = 1 }
```
### 4. Direct Logic and Conditional Execution
Execute resource checks, modifier assignments, or custom effects directly within a button's on click block:
```
button
    sprite "GFX_button_123x34"
    position x50 y200
    text "Fix Vulnerability"
    font "hoi_16mbs"
    on click        
        if
            has_political_power > 49
            num_of_civilian_factories > 1
            then 
                if
                    has_country_flag = has_minigame
                    then
                        # Prevent duplicate minigames
                else
                    set_variable = { button_action = 1 }
                    add_political_power = -50
                    add_timed_idea = {
                        idea = "basic_action"
                        days = 60
                    }
                    open_random_minigame = yes
```
Dynamic text checks can also reflect resource requirements visually:
```
define text price_button_basic_pp
    if
        has_political_power > 49
        num_of_civilian_factories > 1
        then
            text "§G50§" # Green when affordable
    else
        text "§R50§" # Red when unaffordable
```
### 5. Dynamic Flavor Text
Cycle through string arrays or state checks dynamically using variable evaluation:
```
define text random_yap
    if
        check_variable = { text = 1 }
        then
            text "Just Rewrite it in rust!"
    else if
        check_variable = { text = 2 }
        then
            text "I use ARCH BTW!"
    else if
        check_variable = { text = 3 }
        then
            text "The greater the mass the greater the force of attraction."
    else
        text "System online."
```
Display the dynamic text within the interface block:
```
text
    text random_yap
    font "hoi_36header"
    position x100 y450
    max size x700 y600
```
### 6. Best Practices
Layering & Render Order: Elements render in sequential order. Declare background frames first, icons second, and interactive buttons or text labels on top.

Sizing Units: Use percentage constraints (size 100%) for standard icons or full-frame overlays, and exact pixel coordinates (x500 y300) for window dimensions and text bounds.

Fallback Safety: Include else blocks in define sprite and define text declarations to handle missing states safely and avoid rendering errors.