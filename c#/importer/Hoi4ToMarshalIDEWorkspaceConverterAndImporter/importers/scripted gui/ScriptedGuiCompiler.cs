using Microsoft.VisualBasic.FileIO;
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using static importer.InterfaceImporter;

// todo: use the order of what ellement is saved at in what order and compile it in here back into the same order correctly

namespace importer
{
    public class ScriptedGuiCompiler
    {
        private readonly ScriptedLocalisationImporter _locImporter;
        private readonly ScriptedGuiImporter _guiImporter;
        private readonly GFXImporter _gfxImporter;
        private readonly InterfaceImporter _interfaceImporter;

        public ScriptedGuiCompiler(
            ScriptedLocalisationImporter locImporter,
            ScriptedGuiImporter guiImporter,
            GFXImporter gfxImporter,
            InterfaceImporter interfaceImporter)
        {
            _locImporter = locImporter ?? throw new ArgumentNullException(nameof(locImporter));
            _guiImporter = guiImporter ?? throw new ArgumentNullException(nameof(guiImporter));
            _gfxImporter = gfxImporter ?? throw new ArgumentNullException(nameof(gfxImporter));
            _interfaceImporter = interfaceImporter ?? throw new ArgumentNullException(nameof(interfaceImporter));
        }

        public async Task RunCompileAsync(string rootDirectory, ImportContext context)
        {
            // Verify that the primary importer we are iterating over (GuiImporter in this case) has data
            var results = _guiImporter.Results;

            if (results == null || results.Count == 0)
            {
                DebugLogger.Log("ScriptedGuiCompiler", "", LogLevel.Info, "No GUI data found to compile. Aborting.");
                return;
            }

            // Note: If you need to ensure Loc or GFX are also populated before starting:
            if (_locImporter.Results.Count == 0)
            {
                DebugLogger.Log("ScriptedGuiCompiler", "", LogLevel.Warning, "Localisation results are empty. Names may be missing.");
            }

            DebugLogger.Log("ScriptedGuiCompiler", "", LogLevel.Info, $"Starting compilation. Found {results.Count} GUI elements.");

            var scriptedguiFolder = Path.Combine(rootDirectory, "mod", "scripted gui");
            Directory.CreateDirectory(scriptedguiFolder);

            var groups = results.GroupBy(r => r.FileName ?? "unnamed");
            var writeTasks = new List<Task>();
            var outPaths = new List<string>();

            foreach (var group in groups)
            {

                var sourceFileName = group.Key;
                var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
                var outFileName = baseName + ".scriptedgui";
                var outPath = Path.Combine(scriptedguiFolder, outFileName);

                // Simple collision check for file names
                if (File.Exists(outPath))
                {
                    int suffix = 1;
                    while (File.Exists(Path.Combine(scriptedguiFolder, $"{baseName}_{suffix}.scriptedgui")))
                    {
                        suffix++;
                    }
                    outPath = Path.Combine(scriptedguiFolder, $"{baseName}_{suffix}.scriptedgui");
                }

                var lines = new List<string>();
                var bracketedSprites = new HashSet<string>(StringComparer.Ordinal);
                var bracketedTexts = new HashSet<string>(StringComparer.Ordinal);

                // Inside the foreach (var group in groups) loop
                foreach (var item in group)
                {
                    // 1. DATA DETECTION: Look up the UI data by ID
                    var uiData = _interfaceImporter.Results.FirstOrDefault(i => i.Id == item.Id);

                    if (uiData == null)
                    {
                        DebugLogger.Log("ScriptedGuiCompiler", sourceFileName, LogLevel.Warning, $"No interface data found for ID {item.Id}, skipping.");
                        continue;
                    }

                    // If it's a draggable window, use your specific format
                    if (uiData.moveable)
                    {
                        lines.Add($"draggable window {uiData.Id}");
                    }
                    else
                    {
                        lines.Add($"window {uiData.Id}");
                    }
                    lines.Add($"    size x{uiData.sizeX ?? "0"} y{uiData.sizeY ?? "0"}");
                    lines.Add($"    position x{uiData.positionX ?? "0"} y{uiData.positionY ?? "0"}");

                    // Emit any saved raw lines for the main interface container
                    if (uiData.Lines != null)
                    {
                        foreach (var l in uiData.Lines)
                        {
                            var indent = new string('\t', Math.Max(0, l.Depth));
                            lines.Add(indent + l.Content);
                        }
                    }

                    // Find the background/sprite info
                    var bg = uiData.Background?.FirstOrDefault();
                    if (bg != null && !string.IsNullOrWhiteSpace(bg.quadTextureSprite))
                    {
                        // If sprite is in the form [id_here] we should emit without quotes and without resolving
                        if (IsBracketed(bg.quadTextureSprite))
                        {
                            var id = StripBrackets(bg.quadTextureSprite);
                            lines.Add($"    sprite {id}");
                            bracketedSprites.Add(id);
                        }
                        else
                        {
                            lines.Add($"    sprite \"{bg.quadTextureSprite}\"");
                        }
                    }

                    // Emit any saved raw lines for the background
                    if (bg != null && bg.Lines != null)
                    {
                        foreach (var l in bg.Lines)
                        {
                            var indent = new string(' ', Math.Max(0, l.Depth * 4));
                            lines.Add(indent + l.Content);
                        }
                    }

                    // Try to find the matching ScriptedGui for this interface item.
                    // Match by scripted gui Id or by WindowName (some files use window_name to reference the interface id).
                    var guiLogic = _guiImporter.Results.FirstOrDefault(g =>
                        string.Equals(g.Id, item.Id, StringComparison.Ordinal) ||
                        string.Equals(g.WindowName, item.Id, StringComparison.Ordinal)
                    );
                    if (guiLogic != null)
                    {
                        DebugLogger.Log("ScriptedGuiCompiler", sourceFileName, LogLevel.Info, $"Found scripted gui for interface '{item.Id}' -> scriptedGui.Id='{guiLogic.Id}', WindowName='{guiLogic.WindowName}', Effects={guiLogic.Effects?.Count ?? 0}");
                        if (guiLogic.Effects != null && guiLogic.Effects.Any())
                        {
                            DebugLogger.Log("ScriptedGuiCompiler", sourceFileName, LogLevel.Info, $"Effect IDs: {string.Join(", ", guiLogic.Effects.Select(e => e.Id))}");
                        }
                    }
                    if (guiLogic != null && guiLogic.VisibleLines != null && guiLogic.VisibleLines.Any())
                    {
                        lines.Add("    visible");
                        foreach (var vLine in guiLogic.VisibleLines)
                        {
                            var indent = new string('\t', Math.Max(0, vLine.Depth));
                            lines.Add(indent + vLine.Content);
                        }
                    }

                    // Collect all non-background UI elements and emit them in the original import order using orderNumber
                    var elements = new List<(int Order, string Kind, object Obj)>();
                    int ParseOrderOrMax(string s)
                    {
                        if (string.IsNullOrWhiteSpace(s)) return int.MaxValue;
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
                        return int.MaxValue;
                    }

                    if (uiData.instantTextBoxType != null)
                    {
                        foreach (var text in uiData.instantTextBoxType)
                        {
                            elements.Add((ParseOrderOrMax(text.orderNumber), "instantTextBoxType", text));
                        }
                    }
                    if (uiData.buttonType != null)
                    {
                        foreach (var btn in uiData.buttonType)
                        {
                            elements.Add((ParseOrderOrMax(btn.orderNumber), "buttonType", btn));
                        }
                    }
                    if (uiData.iconType != null)
                    {
                        foreach (var ic in uiData.iconType)
                        {
                            elements.Add((ParseOrderOrMax(ic.orderNumber), "iconType", ic));
                        }
                    }

                    // sort by Order ascending (elements without order go to end)
                    elements.Sort((a, b) => a.Order.CompareTo(b.Order));

                    foreach (var elem in elements)
                    {
                        if (elem.Kind == "instantTextBoxType")
                        {
                            var text = (instantTextBoxType)elem.Obj;
                            lines.Add($"    text \"{text.Id}\"");
                            if (IsBracketed(text.textLoc))
                            {
                                var id = StripBrackets(text.textLoc);
                                lines.Add($"        text {id}");
                                bracketedTexts.Add(id);
                            }
                            else
                            {
                                string resolvedText = ResolveLoc(context, text.textLoc);
                                if (!string.IsNullOrWhiteSpace(resolvedText))
                                    lines.Add($"        text \"{resolvedText}\"");
                            }
                            if (!string.IsNullOrWhiteSpace(text.font))
                                lines.Add($"        font \"{text.font}\"");
                            lines.Add($"        position x{text.positionX} y{text.positionY}");
                            if (!string.IsNullOrWhiteSpace(text.pdx_tooltip))
                            {
                                if (IsBracketed(text.pdx_tooltip))
                                {
                                    var tid = StripBrackets(text.pdx_tooltip);
                                    lines.Add($"        static tooltip {tid}");
                                    bracketedTexts.Add(tid);
                                }
                                else
                                {
                                    var resolvedTooltip = ResolveLoc(context, text.pdx_tooltip);
                                    if (!string.IsNullOrWhiteSpace(resolvedTooltip))
                                        lines.Add($"        static tooltip \"{resolvedTooltip}\"");
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(text.maxWidth) || !string.IsNullOrWhiteSpace(text.maxHeight))
                                lines.Add($"        max size x{text.maxWidth} y{text.maxHeight}");

                            if (text.Lines != null)
                            {
                                foreach (var l in text.Lines)
                                {
                                    var indent = new string('\t', Math.Max(0, l.Depth));
                                    lines.Add(indent + l.Content);
                                }
                            }
                            lines.Add(string.Empty);
                        }
                        else if (elem.Kind == "buttonType")
                        {
                            var btn = (buttonType)elem.Obj;
                            lines.Add($"    button \"{btn.Id}\"");
                            if (IsBracketed(btn.textLoc))
                            {
                                var id = StripBrackets(btn.textLoc);
                                lines.Add($"        text {id}");
                                bracketedTexts.Add(id);
                            }
                            else
                            {
                                string resolvedText = ResolveLoc(context, btn.textLoc);
                                if (!string.IsNullOrWhiteSpace(resolvedText))
                                    lines.Add($"        text \"{resolvedText}\"");
                            }
                            if (!string.IsNullOrWhiteSpace(btn.font))
                                lines.Add($"        font \"{btn.font}\"");
                            if (!string.IsNullOrWhiteSpace(btn.spriteType))
                            {
                                if (IsBracketed(btn.spriteType))
                                {
                                    var id = StripBrackets(btn.spriteType);
                                    lines.Add($"        sprite {id}");
                                    bracketedSprites.Add(id);
                                }
                                else
                                    lines.Add($"        sprite \"{btn.spriteType}\"");
                            }
                            if (!string.IsNullOrWhiteSpace(btn.scale))
                                lines.Add($"        size {FormatScale(btn.scale)}%");
                            if (!string.IsNullOrWhiteSpace(btn.pdx_tooltip))
                            {
                                if (IsBracketed(btn.pdx_tooltip))
                                {
                                    var id = StripBrackets(btn.pdx_tooltip);
                                    lines.Add($"        static tooltip {id}");
                                    bracketedTexts.Add(id);
                                }
                                else
                                {
                                    var resolvedTooltip = ResolveLoc(context, btn.pdx_tooltip);
                                    if (!string.IsNullOrWhiteSpace(resolvedTooltip))
                                        lines.Add($"        static tooltip \"{resolvedTooltip}\"");
                                }
                            }
                            lines.Add($"        position x{btn.positionX ?? "0"} y{btn.positionY ?? "0"}");

                            if (btn.Lines != null)
                            {
                                foreach (var l in btn.Lines)
                                {
                                    var indent = new string('\t', Math.Max(0, l.Depth));
                                    lines.Add(indent + l.Content);
                                }
                            }

                            var expectedEffectId = btn.Id + "_click";
                            var hasClickLogic = guiLogic != null && (guiLogic.Effects != null && guiLogic.Effects.Any(e => string.Equals(e.Id, expectedEffectId, StringComparison.Ordinal)));
                            if (hasClickLogic)
                            {
                                lines.Add($"        on click");
                                var collected = new List<ScriptedGuiImporter.ScriptedLine>();
                                if (guiLogic?.Lines != null) collected.AddRange(guiLogic.Lines.Where(x => x?.Content != null));
                                if (guiLogic?.Effects != null)
                                {
                                    foreach (var ef in guiLogic.Effects.Where(e => string.Equals(e?.Id, expectedEffectId, StringComparison.Ordinal)))
                                    {
                                        if (ef.Lines != null) collected.AddRange(ef.Lines.Where(x => x?.Content != null));
                                    }
                                }

                                foreach (var l in collected)
                                {
                                    var indent = new string('\t', Math.Max(0, l.Depth));
                                    if (l.Content == "}" && (l.Depth == 0 || l.Depth == 2 || l.Depth == 1))
                                        continue;
                                    lines.Add(indent + l.Content);
                                }
                            }

                            lines.Add(string.Empty);
                        }
                        else if (elem.Kind == "iconType")
                        {
                            var ic = (iconType)elem.Obj;
                            var gfx = _gfxImporter.Results.FirstOrDefault(g => g.Id == ic.spriteType);
                            if (gfx != null)
                            {
                                if (gfx.IsHorizontal)
                                    lines.Add($"    horizontal bar with {gfx.steps} steps \"{ic.Id}\"");
                                else
                                    lines.Add($"    vertical bar with {gfx.steps} steps \"{ic.Id}\"");
                                if (!string.IsNullOrWhiteSpace(gfx.sizeX) || !string.IsNullOrWhiteSpace(gfx.sizeY))
                                    lines.Add($"        size x{gfx.sizeX} y{gfx.sizeY}");
                                lines.Add($"        position x{ic.positionX ?? "0"} y{ic.positionY ?? "0"}");
                                if (!string.IsNullOrWhiteSpace(gfx.textureFile1))
                                    lines.Add($"        full bar sprite \"{gfx.textureFile1}\"");
                                if (!string.IsNullOrWhiteSpace(gfx.textureFile2))
                                    lines.Add($"        empty bar sprite \"{gfx.textureFile2}\"");
                                if (!string.IsNullOrWhiteSpace(gfx.color))
                                    lines.Add($"        progressed color  {gfx.color}");
                                if (!string.IsNullOrWhiteSpace(gfx.colortwo))
                                    lines.Add($"        unprogressed color  {gfx.colortwo}");

                                var matchingProp = _guiImporter.Results.SelectMany(sg => sg.Properties).FirstOrDefault(p => p.Id == ic.Id);
                                if (matchingProp != null)
                                {
                                    if (!string.IsNullOrWhiteSpace(matchingProp.Frame))
                                    {
                                        lines.Add($"        var \"{matchingProp.Frame}\"");
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(ic.pdx_tooltip))
                                {
                                    if (IsBracketed(ic.pdx_tooltip))
                                        lines.Add($"        static tooltip {StripBrackets(ic.pdx_tooltip)}");
                                    else
                                    {
                                        var resolvedTooltip = ResolveLoc(context, ic.pdx_tooltip);
                                        if (!string.IsNullOrWhiteSpace(resolvedTooltip))
                                            lines.Add($"        static tooltip \"{resolvedTooltip}\"");
                                    }
                                }

                                if (ic.Lines != null)
                                {
                                    foreach (var l in ic.Lines)
                                    {
                                        var indent = new string('\t', Math.Max(0, l.Depth));
                                        lines.Add(indent + l.Content);
                                    }
                                }
                                lines.Add(string.Empty);
                                continue;
                            }

                            lines.Add($"    icon \"{ic.Id}\"");
                            if (!string.IsNullOrWhiteSpace(ic.spriteType))
                            {
                                if (IsBracketed(ic.spriteType))
                                    lines.Add($"        sprite {StripBrackets(ic.spriteType)}");
                                else
                                    lines.Add($"        sprite \"{ic.spriteType}\"");
                            }
                            if (!string.IsNullOrWhiteSpace(ic.sizeX) || !string.IsNullOrWhiteSpace(ic.sizeY))
                                lines.Add($"        size x{ic.sizeX} y{ic.sizeY}");
                            if (!string.IsNullOrWhiteSpace(ic.scale))
                                lines.Add($"        size {FormatScale(ic.scale)}%");
                            if (!string.IsNullOrWhiteSpace(ic.pdx_tooltip))
                            {
                                if (IsBracketed(ic.pdx_tooltip))
                                    lines.Add($"        static tooltip {StripBrackets(ic.pdx_tooltip)}");
                                else
                                {
                                    var resolvedTooltip = ResolveLoc(context, ic.pdx_tooltip);
                                    if (!string.IsNullOrWhiteSpace(resolvedTooltip))
                                        lines.Add($"        static tooltip \"{resolvedTooltip}\"");
                                }
                            }

                            lines.Add($"        position x{ic.positionX ?? "0"} y{ic.positionY ?? "0"}");

                            if (ic.Lines != null)
                            {
                                foreach (var l in ic.Lines)
                                {
                                    var indent = new string('\t', Math.Max(0, l.Depth));
                                    lines.Add(indent + l.Content);
                                }
                            }

                            lines.Add(string.Empty);
                        }
                    }



                    lines.Add(string.Empty);
                }


                // Build define blocks for bracketed sprites and texts using scripted localisation data when available.
                // Sprites and texts are handled similarly: find scripted localisation options for the bracketed id
                // and emit condition headers, the raw lines saved inside the option (these are the lines that appear
                // above the 'then' in your example), and then emit the resolved value (sprite or text).

                // Helper local function to emit options
                void EmitOptions(string defineKind, string id, Func<string, string> valueResolver)
                {
                    // Start define block
                    lines.Add($"define {defineKind} {id}");

                    // Find the scripted localisation whose defined name matches this define id (defined_text name)
                    // and collect its options. The importer stores the defined_text name in ScriptedLocalisation.Id
                    var matches = _locImporter.Results
                        .Where(sl => string.Equals(sl.Id, id, StringComparison.Ordinal))
                        .SelectMany(sl => sl.Options)
                        .ToList();

                    // Fallback: no scripted options found -> emit an 'else' block containing the resolved value
                    // (emit as an else so the output remains syntactically valid for the consumer)
                    if (matches.Count == 0)
                    {
                        var v = valueResolver(id);
                        // emit else header
                        lines.Add("    else");
                        if (defineKind == "text")
                            lines.Add("        text \"" + v + "\"");
                        else
                            lines.Add("        sprite " + v);
                        lines.Add(string.Empty);
                        return;
                    }

                    // Emit each option as an if / else if / else block using the raw lines saved in the localisation importer
                    foreach (var opt in matches)
                    {
                        string header = opt.Condition switch
                        {
                            ScriptedLocalisationImporter.ConditionKind.If => "    if",
                            ScriptedLocalisationImporter.ConditionKind.ElseIf => "    else if",
                            _ => "    else"
                        };

                        lines.Add(header);

                        // Emit the raw condition lines (these are the saved trigger/condition lines). Use spaces (4 per depth)
                        if (opt.Lines != null)
                        {
                            foreach (var rl in opt.Lines)
                            {
                                int depth = Math.Max(0, rl.Depth);
                                var indentSpaces = new string(' ', depth * 4);

                                // If this raw line is a 'trigger' wrapper, extract and emit its inner content
                                if (rl.Content.StartsWith("trigger", StringComparison.OrdinalIgnoreCase))
                                {
                                    int firstBrace = rl.Content.IndexOf('{');
                                    int lastBrace = rl.Content.LastIndexOf('}');
                                    if (firstBrace >= 0 && lastBrace > firstBrace)
                                    {
                                        var inner = rl.Content.Substring(firstBrace + 1, lastBrace - firstBrace - 1).Trim();
                                        if (!string.IsNullOrEmpty(inner))
                                        {
                                            // inner may contain its own assignments; emit directly
                                            lines.Add(indentSpaces + inner);
                                            continue;
                                        }
                                    }
                                    // fallback: emit the raw trigger line if we couldn't parse inner
                                    lines.Add(indentSpaces + rl.Content);
                                    continue;
                                }

                                // Skip stray closing braces saved in localisation options
                                if (rl.Content.Trim() == "}")
                                    continue;

                                lines.Add(indentSpaces + rl.Content);
                            }
                        }

                        // For If/ElseIf emit a 'then' block and the resolved value inside it. For Else emit the value under the else.
                        if (opt.Condition == ScriptedLocalisationImporter.ConditionKind.If || opt.Condition == ScriptedLocalisationImporter.ConditionKind.ElseIf)
                        {
                            // 'then' should be indented to the same level as the condition lines (depth 2 -> 8 spaces)
                            lines.Add(new string(' ', 8) + "then");

                            // Resolve using the option's NameLocKey which references the actual localisation entry
                            var v = valueResolver(opt.NameLocKey);
                            if (defineKind == "text")
                                lines.Add(new string(' ', 12) + $"text \"{v}\"");
                            else
                                lines.Add(new string(' ', 12) + $"sprite {v}");
                        }
                        else
                        {
                            var v = valueResolver(opt.NameLocKey);
                            if (defineKind == "text")
                                lines.Add(new string(' ', 8) + $"text \"{v}\"");
                            else
                                lines.Add(new string(' ', 8) + $"sprite {v}");
                        }
                    }

                    lines.Add(string.Empty);
                }

                // Emit sprite defines
                foreach (var id in bracketedSprites)
                {
                    EmitOptions("sprite", id, k => ResolveLoc(context, k));
                }

                // Emit text defines
                foreach (var id in bracketedTexts)
                {
                    EmitOptions("text", id, k => ResolveLoc(context, k));
                }

                writeTasks.Add(File.WriteAllLinesAsync(outPath, lines, Encoding.UTF8));
                outPaths.Add(outPath);
            }

            await Task.WhenAll(writeTasks);
            DebugLogger.Log("ScriptedGuiCompiler", "", LogLevel.Info, $"Successfully wrote {outPaths.Count} files.");

        }

        private string ResolveLoc(ImportContext context, string locKey)
        {
            if (string.IsNullOrWhiteSpace(locKey)) return "";
            string language = "english";

            if (context.Languages.TryGetValue(language, out var engDict))
            {
                if (engDict.TryGetValue(locKey, out var translated))
                {
                    return translated.Trim('"');
                }
            }
            return locKey;
        }

        private static string FormatScale(string scale)
        {
            if (string.IsNullOrWhiteSpace(scale)) return "0";
            var raw = scale.Trim();
            bool hasPercent = raw.EndsWith("%", StringComparison.Ordinal);
            raw = raw.TrimEnd('%');
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                double percent = hasPercent ? val : val * 100.0;
                // format without decimals when whole number, otherwise up to 2 decimals
                if (Math.Abs(percent - Math.Round(percent)) < 0.000001)
                    return ((int)Math.Round(percent)).ToString(CultureInfo.InvariantCulture);
                return percent.ToString("0.##", CultureInfo.InvariantCulture);
            }
            return scale;
        }

        private static bool IsBracketed(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.Length >= 2 && s.StartsWith("[") && s.EndsWith("]");
        }

        private static string StripBrackets(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = s.Trim();
            if (s.Length >= 2 && s.StartsWith("[") && s.EndsWith("]"))
                return s.Substring(1, s.Length - 2);
            return s;
        }
    }
}