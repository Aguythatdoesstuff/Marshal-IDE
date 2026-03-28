# Your First Event

Events are the narrative heartbeat of any Hearts of Iron IV mod. In **Marshal Script**, we’ve stripped away the "bracket-counting" headaches and manual localization files, replacing them with a clean, **tab-based** structure.

---

### 1. The Structure
Marshal Script uses indentation (tabs) to define the hierarchy of your code. Unlike standard HOI4 scripting, there are no Python-style colons or mandatory wrappers—the compiler handles the heavy lifting.

> [!TIP]
> While vanilla HOI4 requires brackets `{}` for almost everything, Marshal uses them only when you want to pass through specific vanilla blocks (like `ai_chance`) or for multi-line comments.

---

### 2. Example: Country Event
Inside your `events/` folder, create a new file. The IDE will automatically handle the categorization.

```marshal
country event stability_spiral.1
    title "The Stability Death Spiral"
    desc "The government is losing control. Protests have turned into riots."
    sprite "GFX_report_event_civil_war"

    option "Suppress the Dissidents"
        # Standard HOI4 effects pass through directly
        add_political_power = -50

    option "Orchestrate a Coup"
        add_stability = -0.50
        add_war_support = -0.20
```
### 3. Example: News Event
News events use the exact same logic, just with a different header:

```marshal
news event world_news.1
    title "Global Economic Collapse"
    desc "Markets across the globe have plummeted..."
    sprite "GFX_news_event_market"

    option "A dark day for humanity"
```
### 4. Why This is Better Than Vanilla
 - No Localization Files: You never need to open a .yml file. Just type your text in quotes for title, desc, or option, and Marshal generates the localization keys automatically.

 - Automatic Namespaces: The compiler handles event namespaces globally. You can put all your events in one file or spread them across many; Marshal ensures the game reads them correctly.

 - Clean Comments: Use # for single-line comments or #{ ... #} for multi-line blocks.

 - Instant GFX: Use the sprite keyword to reference your images. If you used the Instant GFX Importer, just type the name and go.

### 5. Seeing the Results
When you save, Marshal transpiles this clean code into the standard, bracket-heavy .txt files the game requires and places them in your Output Path. You write the clean version; the game gets the compatible version.
