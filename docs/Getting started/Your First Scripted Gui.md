# Advanced Scripted GUI

The Marshal GUI DSL is by far the most powerful feature of the transpiler. In vanilla HOI4, creating an interactive minigame or a progress bar requires manually editing `.gui` coordinate files, creating dozens of `spriteTypes`, and writing convoluted `scripted_effects`. 

Marshal collapses all of that into a single, logical block where UI structure and game logic live side-by-side.

---

### 1. Windows: Draggable vs. Static
Creating a window is as simple as declaring its type.
* **Draggable Window**: Can be moved by the player during gameplay.
* **Window**: A standard static window.

```marshal
# A basic draggable window
draggable window "my_cool_window_id"
    size x1573 y900
    position x130 y85     
    sprite "GFX_tiled_window"
    visible
        always = yes
```
### 2. Progress Bars (The Easy Way)
Vanilla progress bars require you to make full and empty .dds files and define them in .gui. Marshal lets you draw them directly using RGB colors and a defined number of steps.

Here is how you define a progress bar linked to a variable (time_left):

```
# A background frame
icon
    sprite "GFX_pol_goal_progress_frame"
    position x233 y75 
    size 100%

# The actual dynamic bar
horizontal bar with 70 steps
    unprogressed color 0.0 0.0 0.3 # Dark Blue
    progressed color 0.0 0.0 0.7   # Bright Blue
    var "time_left"
    position x235 y76   
    size x235 y4
```
    
    
### 3. Reactive Buttons (Minigame Logic)
To make a button "reactive" (e.g., turning green when clicked), we use root-level define blocks combined with our window.

Step A: Define the Dynamic States
First, define the text and sprites outside the window. They will check a variable to see if the button is enabled.


```
# --- DB Core Button Logic ---
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
            text "§GDB Core§"
    else
        text "§RDB Core§"

define text enable_or_disable_button1
    if
        check_variable = { button1_enabled = 1 }
        then
            text "§GEnabled§"
    else
        text "§REnable§"
```
        
Step B: Build the Button in the GUI
Inside your window, stack the text, icon, and button on top of each other. When the button is clicked, it changes the variable, which instantly updates the text and sprite!


```
draggable window "minigame_1"
    size x700 y600
    position x430 y270
    sprite "GFX_tiled_window"
    visible
        has_country_flag = open_minigame_1

    # DB Core Element
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
            set_variable = { button1_enabled = 1 }
```

(You can repeat this structure for as many buttons/nodes as your minigame needs!)

### 4. Advanced Logic: Dynamic Pricing & Complex On-Clicks
You can run deep logic directly inside a button's on click block, including checking for resources and setting multiple flags.

In this example from a cyber-warfare mod, the button checks if the player has enough Political Power and Civilian Factories, deducts the cost, applies a timed idea, and opens a minigame.
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
                        # Prevent opening two games at once
                else
                    set_variable = { button_action = 1 }
                    add_political_power = -50
                    add_timed_idea = {
                        idea = "basic_action"
                        days = 60
                    }
                    open_random_minigame = yes
```    
You can pair this with Dynamic Text so the price turns Red if the player can't afford it, and Green if they can:
```
define text price_button_basic_pp
    if
        has_political_power > 49
        then
            text "§G50§"
    else
        text "§R50§"
```
### 5. Dynamic Flavor Text (The Random Yap Generator)
You can use a variable that changes daily (via On Actions) to cycle through random flavor text in your main menu.
```
define text random_yap_bullshit
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
            text "The greater the mass the greater the force of attraction. (Especially your_mom.png)"
    else if
        check_variable = { text = 4 }
        then
            text "SpongeBob grew in size cuz of the patties. 1GB was necessary."
    else if
        check_variable = { text = 5 }
        then
            text "Syntax error: unknown: at line 3628 column 628 in file hwhurjheheobe.bsbkhsh"
    else
        text "If you see this, the 10,000 lines of code are actually working. Somehow."
```
# Inside the GUI:
```
text
    text random_yap_bullshit
    font "hoi_36header"
    position x100 y450
    max size x700 y600

```
### 6. Summary of GUI Best Practices
Stacking Elements: In Marshal, elements are drawn in the order they appear. Put backgrounds first, icons second, and text/buttons on top.

Percentages vs Pixels: size 100% works for icons/sprites, but windows and max size bounds for text should use exact pixel logic (x500 y300).

Safety Fallbacks: Always end your define sprite and define text chains with an else block to prevent the game engine from crashing or throwing a missing GFX error!
