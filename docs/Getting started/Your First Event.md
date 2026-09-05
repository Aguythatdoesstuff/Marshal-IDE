# Your First Event

Events are the narrative heartbeat of any Hearts of Iron IV mod. In **Marshal Script**, we’ve stripped away the "bracket-counting" headaches and manual localization files, replacing them with a clean, **tab-based** structure.

---

### 1. The Structure
Marshal Script uses indentation (tabs) to define the hierarchy of your code. Unlike standard HOI4 scripting, there are no Python-style colons or mandatory curly brace wrappers—the compiler handles the heavy lifting.

> [!TIP]
> While vanilla HOI4 requires brackets `{}` for almost everything, Marshal uses clean tab indentation to handle scoping. Standard inline Paradox blocks (like triggering a `news_event = { id = news_event.1 }`) can still be written directly.

---

### 2. Country & News Events Example

Here is the exact structure used in the default `example_event.event` template:

```
country event country_event.1
    name "The Stability Death Spiral"
    desc "The government is losing control. Protests have turned into riots, and the military is divided. We must act before the nation tears itself apart."
    sprite "GFX_report_event_001"

    option "Suppress the Dissidents"

    option "Let the Flames Rise"
        news_event = { id = news_event.1 }
        add_stability = -0.50

    option "Join the Revolution"

    option "Orchestrate a Coup"
        add_stability = -0.50

news event news_event.1
    name "The Stability Death Spiral"
    desc "A country lost control into a death spiral ending up in civilwar!"
    sprite "GFX_report_event_FIN_continuation_war"
    major = yes

    option "Suppress the Dissidents"
```

###3. Key Improvements
* Implicit Localization: You never need to open a .yml file. Just type your text in quotes for name, desc, or option titles, and Marshal generates the localization keys automatically.

* Automatic Namespaces: The compiler tracks and manages event namespaces globally. You can group your events in one file or spread them across multiple files; Marshal compiles them cleanly.

* Instant GFX Integration: Use the sprite keyword to reference your event pictures. If you dragged .dds assets into the IDE, simply reference the image and go.

* Direct Pass-Through: Standard HOI4 triggers and effects (like add_stability = -0.50 or major = yes) work natively within these blocks without requiring manual bracket wrappers.

### 4. Seeing the Results
When you save, Marshal transpiles this clean, tabbed code into standard, bracket-heavy .txt files and places them directly into your designated output path. You write the clean version; the game receives the compatible version.