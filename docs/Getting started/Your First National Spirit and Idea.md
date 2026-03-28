# National Spirits and Ideas

In Marshal Script, National Spirits (Ideas) are defined as individual blocks. You no longer need to manage massive nested files or separate localization entries—everything from the modifiers to the description is handled in one place.

---

### 1. The Idea Structure
Ideas are defined using the `country idea` keyword followed by a unique ID. Like everything in Marshal, it uses a **tab-based** hierarchy.

### 2. Example: Economic and Political Spirits
Create a file in your `ideas/` folder and try this structure:

```marshal
country idea won_war_on_ads
    name "Dedicated Server Allocation"
    desc "Diverting industrial computing power to maintain localized network security."
    sprite "GFX_won_war_on_ads"
    
    modifier = {
        production_factory_efficiency_gain_factor = 0.10
        research_speed_factor = 0.07
        stability_factor = 0.05
        political_power_factor = 0.20
    }

country idea leader_dumbass
    name "Leader is DUMBASS"
    desc "Our leader was caught being scammed by a pop-up ad! The nation is humiliated."
    sprite "GFX_leader_dumbass"
    
    modifier = {
        stability_factor = -0.2
        stability_weekly = -0.01
        political_power_cost = 1
    }
```
### 3. Key Syntax Features
Implicit Localization: You provide the name and desc directly as strings. Marshal generates the .yml files and localization keys automatically.

Automatic GFX: The sprite field links directly to your interface files. If you used the Instant GFX Importer, simply use the GFX name you assigned to your image.

Modifier Pass-Through: The modifier = { ... } block accepts any standard Hearts of Iron IV modifier. Marshal passes these through to the compiled code, allowing for 100% compatibility with vanilla mechanics.

No Wrapper Bloat: In vanilla, you have to wrap ideas in ideas = { country = { ... } }. In Marshal, you just declare the idea and the compiler organizes the folder structure for you.

### 4. Advanced Usage: Hidden or Triggered Ideas
Because Marshal Script supports the standard HOI4 logic within its tabbed structure, you can add triggers directly to your ideas:
```
country idea command_distraction
    name "Spam-Filtered Command"
    desc "Our generals are too busy deleting spam to coordinate the front lines."
    sprite "GFX_command_distraction"
    
    # Standard triggers pass through
    allowed = {
        always = yes
    }
    
    modifier = {
        army_speed_factor = -0.05
        planning_speed = -0.1
    }
    
```

### 5. Transpiler Intelligence & Custom Types
The Marshal transpiler is designed to be "type-agnostic." On paper, you can type anything before the idea or event keywords, and the transpiler will attempt to build it.

Example:

Code snippet

femboy idea cool_id
    name "Technical Flexibility"
    modifier = { stability_factor = 0.05 }

femboy event cool_event.1
    title "The Transpiler is too smart"
    desc "This will actually compile."
[!WARNING]
While the code above will compile perfectly into a validly structured .txt file, Hearts of Iron IV will have a stroke trying to understand what a femboy = { ... } idea type block is. The game engine only looks for specific Paradox headers (like country, industrial_manufacturer, etc.). Only use custom prefixes if you are 100% sure the game engine (or a specific mod dependency) is looking for that specific wrapper.

### 6. Compiling
When you save your file, Marshal transpiles these blocks into the standard common/ideas/ format. It automatically groups country ideas into the correct category wrappers so the game engine can read them instantly.
