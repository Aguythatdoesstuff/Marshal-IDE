using System;
using System.Collections.Generic;
using System.Linq;

namespace Compiler
{
    // Lightweight models to hold parsed GUI data. Parser is best-effort and
    // deliberately permissive: validation is the validator's responsibility.
    public class DefineBranch
    {
        public List<RawLine> ConditionRaw = new List<RawLine>(); // raw lines that make up the if / else if condition
        public bool IsElse = false;
        // Single string extracted from the then block (the value inside the text "..." or sprite "..." line)
        public string ThenBlock = null;
    }

    public class Define
    {
        public DefineType Type; // sprite or text
        public string Id;
        public List<DefineBranch> Branches = new List<DefineBranch>();
    }

    public abstract class GuiElement
    {
        public (int x, int y)? MaxSize;
        public GuiElementType Type;
        public string Id; // optional (only set when quoted id present after header)
        public string Sprite;
        public string Text;
        public string Font;
        // When true this element's main field (text or sprite) came from an unquoted property
        public bool IsProperty = false;
        public (int x, int y)? Position;
        public (int x, int y)? Size; // pixel size
        public bool SizeIsPercent = false;
        public int? SizePercent = null; // when size specified as percent (element-level)
        public bool IsTextScriptedLocalisationId = false;
        public bool SpriteIsScriptedId = false;
        // optional id that links this element to a Define entry (preserve original define id)
        public string DefinesId;
        public List<RawLine> OnClickRaw = new List<RawLine>();
        public List<RawLine> Raw = new List<RawLine>(); // raw aggregated inner lines for debugging

        public class ProgressBarElement : GuiElement
        {
            public Orientation Orientation; // horizontal | vertical
            public int Steps;
            public (double r, double g, double b)? ProgressedColor;
            public (double r, double g, double b)? UnprogressedColor;
            public string VarName;
            public string ProgressedSprite;
            public string UnprogressedSprite;
        }
    }
    public class TextElement : GuiElement { }
    public class ButtonElement : GuiElement { }
    public class IconElement : GuiElement { }

    public enum DefineType
    {
        Sprite,
        Text
    }

    public enum GuiElementType
    {
        Text,
        Button,
        Icon,
        ProgressBar
    }

    public enum Orientation
    {
        Horizontal,
        Vertical
    }

    public class Window
    {
        public bool Draggable;
        public string Id;
        public (int x, int y)? Size;
        public bool SizeIsPercent;
        public (int x, int y)? Position;
        public string Sprite;
        public List<RawLine> VisibleRaw = new List<RawLine>();
        public List<GuiElement> Elements = new List<GuiElement>();
        // Collected properties for unquoted scripted sprite/text used inside elements
        public List<WindowProperty> Properties = new List<WindowProperty>();
    }

    public class WindowProperty
    {
        // element type this property belongs to (Icon or ProgressBar)
        public GuiElementType Type;
        // generated or derived id for this property (e.g. "text_get_random_image_loc")
        public string Id;
        // parent element id (the element this property belongs to)
        public string ParentId;
        // the raw value following the keyword (unquoted value)
        public string Value;
    }

    public class ScriptedGUI
    {
        public List<Define> Defines = new List<Define>();
        public List<Window> Windows = new List<Window>();
        // source file name for this parsed snapshot
        public string SourceFileName { get; set; } = string.Empty;
    }

    public class ScriptedGUIParser : BaseParser
    {
        public ScriptedGUI Result { get; private set; } = new ScriptedGUI();
        // Expose the last parsed file snapshot similar to other parsers
        public ScriptedGUI LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            Console.WriteLine($"[PARSER] ScriptedGUIParser starting for {fileName} ({preprocessedLines.Count} lines)");

            // reset result and record source file name so compilers can access it
            Result = new ScriptedGUI();
            Result.SourceFileName = fileName;

            Window currentWindow = null;
            GuiElement currentElement = null;
            Define currentDefine = null;

            bool inOnClick = false; int onClickBaseDepth = 0;
            bool inVisible = false; int visibleBaseDepth = 0;

            // For define parsing
            DefineBranch currentBranch = null;
            bool inDefine = false; int defineBaseDepth = 0;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                Console.WriteLine($"[PARSER] Line {pl.LineNumber} Depth {pl.Depth}: {pl.TrimmedLine}");

                // If currently collecting an on click block for an element
                if (inOnClick)
                {
                    if (pl.Depth > onClickBaseDepth)
                    {
                        currentElement.OnClickRaw.Add(new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth });
                        continue;
                    }
                    else
                    {
                        inOnClick = false;
                        // reprocess this line in outer context
                        i--;
                        continue;
                    }
                }

                if (inVisible)
                {
                    if (pl.Depth > visibleBaseDepth)
                    {
                        currentWindow.VisibleRaw.Add(new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth });
                        continue;
                    }
                    else
                    {
                        inVisible = false;
                        i--;
                        continue;
                    }
                }

                if (inDefine)
                {
                    if (pl.Depth > defineBaseDepth)
                    {
                        // handle if / else if / else / then and collect raw content
                        string t = pl.TrimmedLine;
                        if (t.Equals("if", StringComparison.OrdinalIgnoreCase) || t.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
                        {
                            currentBranch = new DefineBranch();
                            currentDefine.Branches.Add(currentBranch);
                            currentBranch.ConditionRaw = new List<RawLine>();
                            currentBranch.IsElse = false;
                            continue;
                        }
                        if (t.StartsWith("else if", StringComparison.OrdinalIgnoreCase))
                        {
                            currentBranch = new DefineBranch();
                            currentDefine.Branches.Add(currentBranch);
                            currentBranch.ConditionRaw = new List<RawLine>();
                            currentBranch.IsElse = false;
                            continue;
                        }
                        if (t.Equals("else", StringComparison.OrdinalIgnoreCase))
                        {
                            currentBranch = new DefineBranch();
                            currentBranch.IsElse = true;
                            currentDefine.Branches.Add(currentBranch);
                            currentBranch.ConditionRaw = new List<RawLine>();
                            continue;
                        }
                        if (t.Equals("then", StringComparison.OrdinalIgnoreCase))
                        {
                            // subsequent deeper lines belong to then content; we'll collect them
                            // mark by setting a placeholder if needed
                            continue;
                        }

                        // if we have a branch and line is deeper treat as condition or then depending
                        if (currentBranch != null)
                        {
                            // Heuristic: if currentBranch.ConditionRaw is empty and line starts at defineBaseDepth+1
                            // we append to ConditionRaw until we see 'then' keyword (we don't detect here),
                            // but simplest is to append everything into ConditionRaw unless it looks like a 'sprite' or 'text' which we treat as ThenRaw.
                            if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase) || t.StartsWith("text", StringComparison.OrdinalIgnoreCase))
                            {
                                // capture the single value inside the sprite/text line for easy access (e.g., text "Hello")
                                string rem;
                                string val = null;
                                if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                                {
                                    rem = t.Substring("sprite".Length).Trim();
                                }
                                else
                                {
                                    rem = t.Substring("text".Length).Trim();
                                }
                                if (rem.StartsWith("\""))
                                {
                                    val = BaseParser.GetQuotedContent(rem);
                                }
                                else
                                {
                                    val = rem;
                                }
                                currentBranch.ThenBlock = val;
                            }
                            else
                            {
                                currentBranch.ConditionRaw.Add(new RawLine { trimmedLine = t, depth = pl.Depth });
                            }
                            continue;
                        }

                        // otherwise just append to a generic define debug raw (fallback)
                        var fb = new DefineBranch();
                        fb.ConditionRaw = new List<RawLine> { new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth } };
                        currentDefine.Branches.Add(fb);
                        continue;
                    }
                    else
                    {
                        inDefine = false;
                        currentBranch = null;
                        currentDefine = null;
                        i--;
                        continue;
                    }
                }

                // Root-level constructs
                if (pl.Depth == 0)
                {
                    currentElement = null;
                    // define
                    if (pl.TrimmedLine.StartsWith("define ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        currentDefine = new Define();
                        if (parts.Length >= 2)
                        {
                            // define sprite NAME  OR define text NAME
                            if (parts.Length >= 3)
                            {
                                var t = parts[1].ToLowerInvariant();
                                currentDefine.Type = t == "sprite" ? DefineType.Sprite : DefineType.Text;
                                currentDefine.Id = BaseParser.GetQuotedContent(pl.TrimmedLine.Substring(pl.TrimmedLine.IndexOf(parts[2]))).Trim();
                            }
                            else
                            {
                                var t = parts[1].ToLowerInvariant();
                                currentDefine.Type = t == "sprite" ? DefineType.Sprite : DefineType.Text;
                            }
                        }
                        Result.Defines.Add(currentDefine);
                        Console.WriteLine($"[PARSER] Started define: type={currentDefine.Type} id={currentDefine.Id}");
                        inDefine = true; defineBaseDepth = pl.Depth;
                        continue;
                    }

                    // window
                    if (pl.TrimmedLine.StartsWith("draggable window", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("window", StringComparison.OrdinalIgnoreCase))
                    {
                        currentWindow = new Window();
                        currentWindow.Draggable = pl.TrimmedLine.StartsWith("draggable window", StringComparison.OrdinalIgnoreCase);
                        // extract quoted id if present
                        int q = pl.TrimmedLine.IndexOf('"');
                        if (q >= 0)
                        {
                            currentWindow.Id = BaseParser.GetQuotedContent(pl.TrimmedLine.Substring(q));
                        }
                        else
                        {
                            // no quoted id -> try unquoted token after keyword
                            var header = currentWindow.Draggable ? "draggable window" : "window";
                            var rem = pl.TrimmedLine.Length > header.Length ? pl.TrimmedLine.Substring(header.Length).Trim() : string.Empty;
                            currentWindow.Id = string.IsNullOrEmpty(rem) ? null : rem;
                        }

                        Result.Windows.Add(currentWindow);
                        Console.WriteLine($"[PARSER] Started window: draggable={currentWindow.Draggable} id={currentWindow.Id}");
                        continue;
                    }
                }

                // Inside a window: depth 1 entries belong to the window
                if (currentWindow != null && pl.Depth >= 1)
                {
                    // Start of a child element
                    if (pl.Depth == 1 && (pl.TrimmedLine.Equals("text", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("text ", StringComparison.OrdinalIgnoreCase)))
                    {
                        var el = new TextElement() { Type = GuiElementType.Text };
                        // element ID may be provided on the header line (quoted) OR as an unquoted token after the keyword
                        var headerRem = pl.TrimmedLine.Length > 4 ? pl.TrimmedLine.Substring(4).Trim() : string.Empty;
                        if (!string.IsNullOrEmpty(headerRem))
                        {
                            if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                            else el.Id = headerRem;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        // ensure element has an id even when not provided
                        if (string.IsNullOrEmpty(el.Id) && currentWindow != null)
                        {
                            var elemTypeName = el.Type.ToString().ToLowerInvariant();
                            var count = currentWindow.Elements.Count(e => e.Type == el.Type);
                            var winId = string.IsNullOrEmpty(currentWindow.Id) ? "window" : currentWindow.Id.Replace(' ', '_');
                            el.Id = $"{winId}_{elemTypeName}_{count}";
                        }
                        Console.WriteLine($"[PARSER] Added Text element id={el.Id}");
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("button", StringComparison.OrdinalIgnoreCase))
                    {
                        var el = new ButtonElement() { Type = GuiElementType.Button };
                        var headerRem = pl.TrimmedLine.Length > 6 ? pl.TrimmedLine.Substring(6).Trim() : string.Empty;
                        if (!string.IsNullOrEmpty(headerRem))
                        {
                            if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                            else el.Id = headerRem;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        if (string.IsNullOrEmpty(el.Id) && currentWindow != null)
                        {
                            var elemTypeName = el.Type.ToString().ToLowerInvariant();
                            var count = currentWindow.Elements.Count(e => e.Type == el.Type);
                            var winId = string.IsNullOrEmpty(currentWindow.Id) ? "window" : currentWindow.Id.Replace(' ', '_');
                            el.Id = $"{winId}_{elemTypeName}_{count}";
                        }
                        Console.WriteLine($"[PARSER] Added Button element id={el.Id}");
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("icon", StringComparison.OrdinalIgnoreCase))
                    {
                        var el = new IconElement() { Type = GuiElementType.Icon };
                        var headerRem = pl.TrimmedLine.Length > 4 ? pl.TrimmedLine.Substring(4).Trim() : string.Empty;
                        if (!string.IsNullOrEmpty(headerRem))
                        {
                            if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                            else el.Id = headerRem;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        if (string.IsNullOrEmpty(el.Id) && currentWindow != null)
                        {
                            var elemTypeName = el.Type.ToString().ToLowerInvariant();
                            var count = currentWindow.Elements.Count(e => e.Type == el.Type);
                            var winId = string.IsNullOrEmpty(currentWindow.Id) ? "window" : currentWindow.Id.Replace(' ', '_');
                            el.Id = $"{winId}_{elemTypeName}_{count}";
                        }
                        Console.WriteLine($"[PARSER] Added Icon element id={el.Id}");
                        continue;
                    }

                    var barMatch = pl.TrimmedLine;
                    if (pl.Depth == 1 && (pl.TrimmedLine.StartsWith("horizontal bar", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("vertical bar", StringComparison.OrdinalIgnoreCase)))
                    {
                        // parse steps if present
                        var parts = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var el = new GuiElement.ProgressBarElement() { Type = GuiElementType.ProgressBar };
                        el.Orientation = parts[0].Equals("horizontal", StringComparison.OrdinalIgnoreCase) ? Orientation.Horizontal : Orientation.Vertical;
                        int idxWith = Array.FindIndex(parts, p => p.Equals("with", StringComparison.OrdinalIgnoreCase));
                        if (idxWith >= 0 && idxWith + 1 < parts.Length && int.TryParse(parts[idxWith + 1], out var steps))
                        {
                            el.Steps = steps;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        // generate id for bar element if missing: <window>_bar_<count>
                        if (string.IsNullOrEmpty(el.Id) && currentWindow != null)
                        {
                            var elemTypeName = "bar";
                            var count = currentWindow.Elements.Count(e => e.Type == el.Type);
                            var winId = string.IsNullOrEmpty(currentWindow.Id) ? "window" : currentWindow.Id.Replace(' ', '_');
                            el.Id = $"{winId}_{elemTypeName}_{count}";
                            Console.WriteLine($"[PARSER] Generated element id={el.Id}");
                        }
                        Console.WriteLine($"[PARSER] Added {el.Orientation} bar steps={el.Steps} id={el.Id}");
                        continue;
                    }

                    // window-level properties
                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("size ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pl.TrimmedLine.EndsWith("%"))
                        {
                            currentWindow.SizeIsPercent = true;
                            // optionally capture percent value (not parsed as int here)
                        }
                        else
                        {
                            var toks = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length >= 3)
                            {
                                int.TryParse(toks[1].TrimStart('x'), out var sx);
                                int.TryParse(toks[2].TrimStart('y'), out var sy);
                                currentWindow.Size = (sx, sy);
                            }
                        }
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("position ", StringComparison.OrdinalIgnoreCase))
                    {
                        var toks = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (toks.Length >= 3)
                        {
                            int.TryParse(toks[1].TrimStart('x'), out var px);
                            int.TryParse(toks[2].TrimStart('y'), out var py);
                            currentWindow.Position = (px, py);
                        }
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                    {
                        currentWindow.Sprite = BaseParser.GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.Equals("visible", StringComparison.OrdinalIgnoreCase))
                    {
                        inVisible = true; visibleBaseDepth = pl.Depth;
                        currentWindow.VisibleRaw = new List<RawLine>();
                        continue;
                    }

                    // element sub-properties (depth > 1)
                    if (currentElement != null && pl.Depth >= 2)
                    {
                        var t = pl.TrimmedLine;
                        currentElement.Raw.Add(new RawLine { trimmedLine = t, depth = pl.Depth });
                        if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                        {
                            var rem = t.Substring("sprite".Length).Trim();
                            if (rem.StartsWith("\""))
                            {
                                currentElement.Sprite = BaseParser.GetQuotedContent(rem);
                                currentElement.SpriteIsScriptedId = false;
                            }
                            else
                            {
                                currentElement.Sprite = rem;
                                currentElement.SpriteIsScriptedId = true;
                                // preserve the id that may link to a Define (don't overwrite element main Id)
                                currentElement.DefinesId = rem;
                                // mark element's main field as coming from an unquoted property
                                currentElement.IsProperty = true;
                                // If element has no id, generate one: <windowId>_<elementType>_<count>
                                if (string.IsNullOrEmpty(currentElement.Id) && currentWindow != null)
                                {
                                    var elemTypeName = currentElement.Type.ToString().ToLowerInvariant();
                                    var count = currentWindow.Elements.Count(e => e.Type == currentElement.Type);
                                    var winId = string.IsNullOrEmpty(currentWindow.Id) ? "window" : currentWindow.Id.Replace(' ', '_');
                                    currentElement.Id = $"{winId}_{elemTypeName}_{count}";
                                    Console.WriteLine($"[PARSER] Generated element id={currentElement.Id}");
                                }
                                // Only record a window property for Icon elements (images)
                                if (currentWindow != null && currentElement.Type == GuiElementType.Icon)
                                {
                                    var prop = new WindowProperty();
                                    prop.Type = currentElement.Type;
                                    var propCount = currentWindow.Properties.Count(p => p.Type == prop.Type) + 1;
                                    var baseName = rem.Replace(' ', '_');
                                    // Use the provided sprite value as the property id (sanitized) without adding suffixes
                                    prop.Id = baseName;
                                    prop.ParentId = currentElement.Id;
                                    prop.Value = rem;
                                    currentWindow.Properties.Add(prop);
                                    Console.WriteLine($"[PARSER] Added window property type={prop.Type} id={prop.Id} parent={prop.ParentId} value={prop.Value}");
                                }
                            }
                            continue;
                        }
                        if (t.StartsWith("text ", StringComparison.OrdinalIgnoreCase))
                        {
                            var rem = t.Substring("text".Length).Trim();
                            if (rem.StartsWith("\""))
                            {
                                currentElement.Text = BaseParser.GetQuotedContent(rem);
                                if (currentElement is TextElement te) te.IsTextScriptedLocalisationId = false;
                            }
                            else
                            {
                                // For unquoted text, treat as element-local property but DO NOT create a window property.
                                currentElement.Text = rem;
                                if (currentElement is TextElement te) te.IsTextScriptedLocalisationId = true;
                                // preserve the id that may link to a Define (don't overwrite element main Id)
                                currentElement.DefinesId = rem;
                                currentElement.IsProperty = true;
                                if (string.IsNullOrEmpty(currentElement.Id) && currentWindow != null)
                                {
                                    var elemTypeName = currentElement.Type.ToString().ToLowerInvariant();
                                    var count = currentWindow.Elements.Count(e => e.Type == currentElement.Type);
                                    var winId = string.IsNullOrEmpty(currentWindow.Id) ? "window" : currentWindow.Id.Replace(' ', '_');
                                    currentElement.Id = $"{winId}_{elemTypeName}_{count}";
                                    Console.WriteLine($"[PARSER] Generated element id={currentElement.Id}");
                                }
                            }
                            continue;
                        }
                        if (t.StartsWith("font", StringComparison.OrdinalIgnoreCase))
                        {
                            currentElement.Font = BaseParser.GetQuotedContent(t.Substring("font".Length).Trim());
                            continue;
                        }
                        if (t.StartsWith("position", StringComparison.OrdinalIgnoreCase))
                        {
                            var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length >= 3)
                            {
                                int.TryParse(toks[1].TrimStart('x'), out var px);
                                int.TryParse(toks[2].TrimStart('y'), out var py);
                                currentElement.Position = (px, py);
                            }
                            continue;
                        }
                        if (t.StartsWith("max size", StringComparison.OrdinalIgnoreCase))
                        {
                            var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length >= 4)
                            {
                                int.TryParse(toks[2].TrimStart('x'), out var sx);
                                int.TryParse(toks[3].TrimStart('y'), out var sy);
                                currentElement.MaxSize = (sx, sy);
                            }
                            continue;
                        }
                        if (t.StartsWith("size ", StringComparison.OrdinalIgnoreCase))
                        {
                            var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length == 2 && toks[1].EndsWith("%"))
                            {
                                currentElement.SizeIsPercent = true;
                                if (int.TryParse(toks[1].TrimEnd('%'), out var p)) currentElement.SizePercent = p;
                            }
                            else if (toks.Length >= 3)
                            {
                                int.TryParse(toks[1].TrimStart('x'), out var sx);
                                int.TryParse(toks[2].TrimStart('y'), out var sy);
                                currentElement.Size = (sx, sy);
                            }
                            continue;
                        }

                        if (t.StartsWith("on click", StringComparison.OrdinalIgnoreCase))
                        {
                            inOnClick = true; onClickBaseDepth = pl.Depth;
                            currentElement.OnClickRaw = new List<RawLine>();
                            continue;
                        }

                        // progress bar specific properties
                        if (currentElement is GuiElement.ProgressBarElement pbe)
                        {
                            if (t.StartsWith("var ", StringComparison.OrdinalIgnoreCase))
                            {
                                pbe.VarName = BaseParser.GetQuotedContent(t.Substring("var".Length).Trim());
                                // create a window property for the progress bar using the var value as the property Id
                                if (currentWindow != null)
                                {
                                    // ensure progress bar element has an id
                                    if (string.IsNullOrEmpty(pbe.Id))
                                    {
                                        var winId = string.IsNullOrEmpty(currentWindow.Id) ? "window" : currentWindow.Id.Replace(' ', '_');
                                        var count = currentWindow.Elements.Count(e => e.Type == pbe.Type);
                                        pbe.Id = $"{winId}_bar_{count}";
                                        Console.WriteLine($"[PARSER] Generated element id={pbe.Id}");
                                    }
                                    var prop = new WindowProperty();
                                    prop.Type = GuiElementType.ProgressBar;
                                    // save var value as property id
                                    prop.Id = pbe.VarName;
                                    prop.ParentId = pbe.Id;
                                    prop.Value = pbe.VarName;
                                    currentWindow.Properties.Add(prop);
                                    Console.WriteLine($"[PARSER] Added bar property id={prop.Id} parent={prop.ParentId}");
                                }
                                continue;
                            }
                            if (t.StartsWith("progressed color", StringComparison.OrdinalIgnoreCase) || t.StartsWith("unprogressed color", StringComparison.OrdinalIgnoreCase))
                            {
                                var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (toks.Length >= 5)
                                {
                                    // accept comma or dot as decimal separator by normalizing
                                    var sa = toks[toks.Length - 3].Replace(',', '.');
                                    var sb = toks[toks.Length - 2].Replace(',', '.');
                                    var sc = toks[toks.Length - 1].Replace(',', '.');
                                    double.TryParse(sa, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a);
                                    double.TryParse(sb, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b);
                                    double.TryParse(sc, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c);
                                    if (toks[0].StartsWith("progressed", StringComparison.OrdinalIgnoreCase)) pbe.ProgressedColor = (a, b, c);
                                    else pbe.UnprogressedColor = (a, b, c);
                                }
                                continue;
                            }
                            if (t.StartsWith("progressed sprite", StringComparison.OrdinalIgnoreCase))
                            {
                                var rem = t.Substring("progressed sprite".Length).Trim();
                                pbe.ProgressedSprite = BaseParser.GetQuotedContent(rem);
                                continue;
                            }
                            if (t.StartsWith("unprogressed sprite", StringComparison.OrdinalIgnoreCase))
                            {
                                var rem = t.Substring("unprogressed sprite".Length).Trim();
                                pbe.UnprogressedSprite = BaseParser.GetQuotedContent(rem);
                                continue;
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"[PARSER] Completed parse: {Result.Windows.Count} windows, {Result.Defines.Count} defines");

            // Save parsed snapshot for external consumers
            LastParsedFile = Result;
        }
    }
}
