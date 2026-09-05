# National Spirits and Ideas

In **Marshal Script**, National Spirits (Ideas) are defined as modular, individual blocks. You no longer need to manage massive nested files or separate localization entries—everything from modifiers to descriptions is handled in one clean, unified place.

---

### 1. The Structure
Ideas are declared using category keywords (such as `country idea`) followed by a unique ID. Like everything in Marshal, scoping and properties rely on a clean **tab-based** hierarchy.

> [!NOTE]
> Idea categories are fully dynamic. Marshal supports `hidden_ideas` / `hidden_idea` out of the box and lets you define custom categories without being blocked by strict validation checks.

---

### 2. Example: National Spirits

Here is the exact structure used in the `example_ideas.idea` template:

```
country idea leader_dumbass
    name "Leader is DUMBASS"
    desc "OUR 'GREAT' LEADER WAS CAUGHT BEING SCAMMED!!!"
    sprite "GFX_AFG_parliament_building"
    modifier = {
        stability_factor = -0.2
        stability_weekly = -0.01
        political_power_cost = 1
    }
```
Additional Example with Modifiers & Triggers

```
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

country idea command_distraction
    name "Spam-Filtered Command"
    desc "Our generals are too busy deleting spam to coordinate the front lines."
    sprite "GFX_command_distraction"
    
    allowed = {
        always = yes
    }
    
    modifier = {
        army_speed_factor = -0.05
        planning_speed = -0.1
    }
```

## 3. Key Syntax Features
Implicit Localization: Provide strings directly inside quotes for name and desc. Marshal automatically generates the organized .yml files and localization keys.

Instant GFX Integration: The sprite field links directly to your interface graphics. Reference your .dds asset directly with GFX_ prefixes.

Modifier Pass-Through: The modifier = { ... } block accepts any standard Hearts of Iron IV modifier, offering 100% native compatibility with vanilla game mechanics.

Zero Wrapper Bloat: Vanilla HOI4 requires nesting ideas deep inside ideas = { country = { ... } }. In Marshal, you declare the idea directly, and the transpiler automatically wraps and organizes the compiled files into the proper common/ideas/ directory.

## 4. Compiling
When you save your file, Marshal automatically transpiles these blocks into standard Paradox format, placing them directly into your designated target mod directory.