# Your First Decision

Decisions in Marshal Script are organized into categories. By using a tab-based structure, you can define complex interactions—like triggering events or managing custom interfaces—without the "bracket soup" of standard HOI4 files.

---

### 1. Decision Categories
A category groups your decisions together in the player's decision menu. Every decision must live inside a category block. 

Categories support custom assets:
* **`sprite`**: The small icon displayed on the left side of the category name. The `GFX_` prefix is **optional** here.
* **`picture sprite`**: The larger graphic displayed at the top of the decision category. This **requires** the `GFX_` prefix to load properly.

```
category trigger_event_category
    name "trigger event"
    desc "trigger event"

    # Small icon (GFX_ prefix optional)
    sprite "RAJ_decision_investment_mils_icon" 

    # Large header picture (GFX_ prefix required)
    picture sprite "GFX_RAJ_decision_investment_mils_icon"

    priority 1000
    allowed
        always = yes
```
### 2. Creating a Decision
Inside a category, you define individual decisions. Properties such as cost, conditions like available, and execution logic under on click are indented directly under the decision header.

```
category trigger_event_category
    name "trigger event"
    desc "trigger event"

    sprite "RAJ_decision_investment_mils_icon"
    picture sprite "GFX_RAJ_decision_investment_mils_icon"

    priority 1000
    allowed
        always = yes

    decision trigger_event
        name "trigger event"
        desc "trigger event"
        sprite "RAJ_decision_investment_civs_icon"
        cost 69

        available
            not
                country = GER

        on click
            trigger_event = yes
```


3. Key Features & Improvements
* Implicit Localization: Provide text directly in quotes for name and desc. Marshal automatically generates the organized .yml localization files and keys for you.

* Dynamic Asset Handling: Simple sprite definitions handle pathing automatically without strict prefix requirements on category icons.

* Tab-Based Hierarchy: All properties, conditions (available), and logic (on click) are scoped via indentation, completely removing standard curly brace requirements.

* Direct Pass-Through: Standard HOI4 triggers and effects work natively within these blocks; the transpiler strips unnecessary wrappers while compiling to valid game syntax.