# Marshal Script Syntax

While Marshal Script transpiles into standard Hearts of Iron IV code, it uses a modernized, tab-based syntax designed to be human-readable and easy to maintain. This guide covers the core logic structures used across all file types.

---

### 1. The Power of Tabs
In Marshal, **indentation is structure**. 
* Unlike vanilla HOI4, you don't need to wrap every block in curly brackets `{}`.
* A tab indicates that the following lines belong to the "parent" line above them.
* This eliminates the common "missing bracket" errors that break traditional mods.

### 2. Logic: If, Then, Else
Marshal replaces the clunky `if = { limit = { ... } }` system with a direct logic flow.

**Vanilla Style (Old):**
```hoi4
if = {
    limit = { has_government = democratic }
    add_stability = 0.1
}
else = {
    add_stability = -0.1
}
```
Marshal Style (New):
```
if
    has_government = democratic
    then
        add_stability = 0.1
else
    add_stability = -0.1
```

### 3. Comments
Marshal supports both single-line and block comments to help you document your code.

Single-line: Use # to comment out the rest of the line.

Multi-line: Use #{ to start and #} to end a large block of comments.
```
# This is a single line comment

#{
This is a multi-line comment.
Everything inside here is ignored by the compiler.
Useful for credits or complex logic explanations!
#}
```

### 4. Direct Pass-Through
Marshal is a Domain Specific Language (DSL), not a complete replacement for HOI4 logic.

If you know a standard HOI4 command (like add_political_power = 100 or set_technology = { ... }), you can simply type it into your script. If the compiler doesn't have a specific "Simplified" version of that command, it should and will pass it through to the final build exactly as written.

### 5. String-Based Localization
One of the biggest time-savers in Marshal is the handling of text.

Whenever you see a field like name, title, or desc, you can type your text directly in "quotes".

Marshal automatically generates the unique localization keys and the .yml files for you.

You no longer need to maintain separate localization folders manually.


### 6. Dynamic Text (Scripted Localization)
In standard HOI4, making text change based on conditions requires a mess of scripted_localisation files. In Marshal, you use a define text block at the root level and reference it almost anywhere (like in a Scripted GUI).

The usage:

```
text
    # Reference the ID defined below
    text cool_text_id
    font "my_cool_font"
    position x100 y100
```
The definition:

```
define text cool_text_id
    if
        tag = GER
        then
            text "German Reich"
    else
        text "I am friendly!!"
```
### 7. Dynamic Sprites
Similar to text, you can define icons that change visually based on game state using the define sprite root-level syntax.

The usage:

```
icon
    # Reference the ID defined below
    sprite cool_sprite
    size 200%
    position x102 y200
```
The definition:

```
define sprite cool_sprite
    if
        tag = GER
        then
            text "GFX_german_reich_gfx"
    else
        text "GFX_peaceful"
```
>[!NOTE]
>For icon blocks, sizes are often defined in percentages (e.g., 200%), whereas window blocks use pixel coordinates (e.g., x100 y100) due to Clausewitz engine requirements.
