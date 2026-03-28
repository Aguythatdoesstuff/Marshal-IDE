# Your First Decision

Decisions in Marshal Script are organized into categories. By using a tab-based structure and modernized logic flow, you can define complex interactions—like UI toggles—without the "bracket soup" of standard HOI4 files.

---

### 1. Decision Categories
A category groups your decisions together in the player's decision menu. Every decision must live inside a category block.

```marshal
category internal_politics
    name "Internal Politics"
    desc "Manage the political landscape of the nation."
    icon "GFX_decision_category_politics"
    
```
### 2. Creating a Decision
Inside a category, you define individual decisions. Marshal Script introduces a much more readable if/then/else syntax for effects, which is a massive upgrade over the vanilla limit system.

Example: A GUI Toggle Decision
This example shows how to use a decision to toggle a custom interface on and off.
```
category internal_politics
    decision open_party_manager
        name "Manage Coalition"
        desc "Open the interface to manage ruling and opposition parties."
        icon "GFX_decision_politics"
        
        visible
            always = yes

        available
            always = yes

        # Decision Settings
        fire_only_once = no
        cost = 0
        
        on click
            if
                has_country_flag = open_party_manager
                then
                    clr_country_flag = open_party_manager
            else
                set_country_flag = open_party_manager
                # Initialize arrays or logic via a scripted effect
                init_party_arrays
        
```

### 3. Key Improvements
 - Modern Logic Flow: Instead of nesting multiple if = { limit = { ... } } blocks, Marshal uses a clean if/then/else structure.

 - Implicit Localization: Like Events and Focuses, the name and desc strings are automatically turned into localization entries by the compiler.

 - Tab-Based Hierarchy: All properties of a decision (visible, available, effects) are indented, making it clear where one decision ends and the next begins.

 - Direct Pass-Through: Standard HOI4 triggers and effects work perfectly within these blocks; the DSL simply removes the unnecessary wrappers.

### 4. Why Use Marshal for Decisions?
In vanilla HOI4, creating a simple "toggle" for a scripted GUI requires repetitive, inverted logic blocks. Marshal Script allows you to express the intent of the code ("If it's open, close it; otherwise, open it") in a way that is easy to read and maintain.
